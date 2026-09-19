using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using ProjectZ.Shared;

namespace ProjectZ.Switch.Install
{
    /// <summary>
    /// Instalador de assets in-game ("a lo ROM").
    ///
    /// El usuario copia su carpeta original de LADXHD v1.0.0 a <c>original/</c> junto al NRO.
    /// Al arrancar sin Content/Data, esto aplica en la consola los parches VCDIFF de bighead
    /// (embebidos en el NRO como <c>switch-assets.zip</c>, con su permiso explícito del
    /// 19-sep-2026) y genera Content/ y Data/ exactamente como lo haría el patcher oficial
    /// en modo Android+OpenGL (solo Content y Data; sin ejecutable ni launcher).
    ///
    /// Réplica de <c>ladxhd_patcher_source_code/Program/Functions.cs</c> (PatchGameFiles +
    /// HandleMultiFilePatches + CopyNewFiles + CleanUp) reducida a lo que necesita Switch:
    ///   1. Cada fichero de original/Content y original/Data con parche de su nombre se
    ///      parchea; sin parche, se copia tal cual.
    ///   2. Cada "master" de <see cref="AssetTargets"/> genera además sus derivados
    ///      (p. ej. eng.lng → esp.lng, deu.lng…) aplicando <c>&lt;derivado&gt;.vcdiff</c>.
    ///   3. Los ficheros con nombre de derivado que hubiera en original/ se ignoran.
    ///   4. dungeon3_1.map(.data) se toman del recurso del patcher, no de original/.
    ///   5. Extras del patcher: Data/Achievements/*. Limpieza de mapas basura.
    ///
    /// Puro .NET: sin MonoGame ni rutas "sdmc:" (regla de oro del port). Se ejecuta en un
    /// hilo aparte y la pantalla del instalador lee el progreso de los campos públicos.
    /// </summary>
    public sealed class AssetInstaller
    {
        /// <summary>Versión de upstream contra la que se compiló este NRO y cuyos parches lleva.</summary>
        public const string ExpectedAssetVersion = "2.0.8";

        public const string OriginalFolderName = "original";
        public const string MarkerFileName = "assets-version.txt";
        public const string BundleResourceName = "switch-assets.zip";

        // ----------------------------------------------------------------- estado (lo lee la UI)

        public enum Stage { Idle, Locating, Loading, Verifying, Patching, Finishing, Done, Failed }

        private volatile Stage _stage = Stage.Idle;
        private volatile string _current = "";
        private volatile string _error = null;
        private volatile string _errorDetail = null;
        private int _done;
        private int _total;
        private readonly StringBuilder _log = new StringBuilder();

        public Stage CurrentStage => _stage;
        public string CurrentItem => _current;
        public int Done => Volatile.Read(ref _done);
        public int Total => Volatile.Read(ref _total);
        public string Error => _error;
        public string ErrorDetail => _errorDetail;
        public bool Finished => _stage == Stage.Done || _stage == Stage.Failed;
        public bool Succeeded => _stage == Stage.Done;
        public string LogText { get { lock (_log) return _log.ToString(); } }

        // ----------------------------------------------------------------- configuración

        private readonly string _baseDir;
        private readonly Func<Stream> _openBundle;
        private readonly Action<string> _logSink;

        /// <param name="baseDir">Carpeta del NRO (AppContext.BaseDirectory). Nunca con prefijo "sdmc:".</param>
        /// <param name="openBundle">Abre el ZIP de parches (recurso embebido o fichero en PC).</param>
        /// <param name="logSink">Opcional: recibe cada línea de log (SwitchDiag en consola, Console en PC).</param>
        public AssetInstaller(string baseDir, Func<Stream> openBundle, Action<string> logSink = null)
        {
            _baseDir = baseDir;
            _openBundle = openBundle;
            _logSink = logSink;
        }

        public string ContentDir => Path.Combine(_baseDir, "Content");
        public string DataDir => Path.Combine(_baseDir, "Data");
        public string OriginalDir => Path.Combine(_baseDir, OriginalFolderName);
        public string MarkerPath => Path.Combine(_baseDir, MarkerFileName);

        // ----------------------------------------------------------------- decisión de arranque

        public enum BootAction
        {
            /// <summary>Content y Data están; arrancar el juego.</summary>
            RunGame,
            /// <summary>Faltan Content/Data (o el marcador dice otra versión): instalar desde original/.</summary>
            Install,
        }

        /// <summary>
        /// Qué hacer al arrancar. Content+Data presentes sin marcador = instalación manual del
        /// día 1 (patcher de Windows): se respeta y no se toca. Con marcador de otra versión
        /// (NRO actualizado) se regeneran los assets desde original/.
        /// </summary>
        public BootAction Decide()
        {
            bool hasContent = Directory.Exists(ContentDir);
            bool hasData = Directory.Exists(DataDir);
            string marker = ReadMarker();

            if (hasContent && hasData && (marker == null || marker == ExpectedAssetVersion))
                return BootAction.RunGame;
            return BootAction.Install;
        }

        public string ReadMarker()
        {
            try
            {
                if (!File.Exists(MarkerPath)) return null;
                return File.ReadAllText(MarkerPath).Trim();
            }
            catch { return null; }
        }

        // ----------------------------------------------------------------- ejecución

        /// <summary>Lanza la instalación en un hilo propio. La UI sondea el estado.</summary>
        public Thread Start()
        {
            var t = new Thread(RunSafe) { IsBackground = true, Name = "AssetInstaller" };
            t.Start();
            return t;
        }

        /// <summary>Versión síncrona que nunca lanza: deja el resultado en el estado (harness de PC).</summary>
        public void RunSync() => RunSafe();

        private void RunSafe()
        {
            try
            {
                Run();
            }
            catch (InstallException e)
            {
                Fail(e.Message, e.Detail);
            }
            catch (Exception e)
            {
                Fail("Error inesperado instalando", e.GetType().Name + ": " + e.Message);
                Log(e.ToString());
            }
        }

        /// <summary>Ejecuta la instalación de forma síncrona (la usa el harness de PC).</summary>
        public void Run()
        {
            _stage = Stage.Locating;
            Log("instalador: base=" + _baseDir);

            string original = LocateOriginal();
            Log("original en " + original);

            _stage = Stage.Loading;
            _current = "switch-assets.zip";
            var bundle = Bundle.Load(_openBundle, Log);
            if (bundle.Version != ExpectedAssetVersion)
                throw new InstallException("El paquete de parches es de otra versión",
                    "NRO espera " + ExpectedAssetVersion + ", el ZIP embebido dice " + bundle.Version);

            // Lista de ficheros de original/ (Content y Data), clave = nombre, como el patcher.
            var files = new List<SourceFile>();
            CollectFiles(original, "Content", files);
            CollectFiles(original, "Data", files);
            if (files.Count == 0)
                throw new InstallException("original/ no contiene Content ni Data",
                    "Copia la carpeta ENTERA de tu Link's Awakening DX HD v1.0.0 a " + OriginalFolderName + "/");
            Log(files.Count + " ficheros en original/");

            var byName = new Dictionary<string, SourceFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
                if (!byName.ContainsKey(f.Name))
                    byName[f.Name] = f;

            _stage = Stage.Verifying;
            Verify(bundle, byName);

            _stage = Stage.Patching;
            Patch(bundle, files);

            _stage = Stage.Finishing;
            WriteExtras(bundle);
            CleanJunkMaps();

            if (!File.Exists(Path.Combine(DataDir, "Maps", "dungeon3.map")))
                throw new InstallException("Falta dungeon3.map tras instalar",
                    "Repórtalo en el hilo de GBAtemp del port");

            File.WriteAllText(MarkerPath, ExpectedAssetVersion + "\n");
            _current = "";
            Log("instalación completa: " + Done + " ficheros");
            _stage = Stage.Done;
        }

        // ----------------------------------------------------------------- pasos

        private string LocateOriginal()
        {
            string dir = OriginalDir;
            if (!Directory.Exists(dir))
                throw new InstallException("No encuentro la carpeta original/",
                    "Copia tu Link's Awakening DX HD v1.0.0 (con Content y Data) a\n" +
                    "switch/zelda-ladxhd/" + OriginalFolderName + "/ y vuelve a arrancar.");

            if (Directory.Exists(Path.Combine(dir, "Content")) && Directory.Exists(Path.Combine(dir, "Data")))
                return dir;

            // El usuario ha copiado la carpeta del juego entera dentro de original/ (una capa
            // de más): si hay exactamente una subcarpeta con Content y Data, es esa.
            string found = null;
            int candidates = 0;
            foreach (var sub in Directory.GetDirectories(dir))
            {
                if (Directory.Exists(Path.Combine(sub, "Content")) && Directory.Exists(Path.Combine(sub, "Data")))
                {
                    candidates++;
                    found = sub;
                }
            }
            if (candidates == 1)
                return found;

            throw new InstallException("original/ no tiene Content y Data",
                "Dentro de " + OriginalFolderName + "/ deben estar las carpetas Content y Data\n" +
                "de tu copia v1.0.0 (o la carpeta del juego entera).");
        }

        private static void CollectFiles(string root, string sub, List<SourceFile> into)
        {
            string dir = Path.Combine(root, sub);
            if (!Directory.Exists(dir)) return;
            foreach (var path in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                string rel = path.Substring(root.Length).TrimStart('/', '\\');
                // Basura de una copia previa parcheada en Windows: no es v1.0.0, no se copia.
                if (rel.Replace('\\', '/').StartsWith("Data/Backup/", StringComparison.OrdinalIgnoreCase))
                    continue;
                into.Add(new SourceFile { Path = path, Relative = rel, Name = System.IO.Path.GetFileName(path) });
            }
        }

        private void Verify(Bundle bundle, Dictionary<string, SourceFile> byName)
        {
            SetTotal(bundle.Manifest.Count);
            var missing = new List<string>();
            var wrong = new List<string>();

            foreach (var entry in bundle.Manifest)
            {
                _current = entry.Name;
                if (!byName.TryGetValue(entry.Name, out var src))
                {
                    missing.Add(entry.Name);
                }
                else
                {
                    long size = new FileInfo(src.Path).Length;
                    if (size != entry.Size || Fnv1a64.OfFile(src.Path) != entry.Hash)
                        wrong.Add(entry.Name);
                }
                Advance();
            }

            if (missing.Count == 0 && wrong.Count == 0)
            {
                Log("verificación OK: " + bundle.Manifest.Count + " ficheros v1.0.0");
                return;
            }

            Log("verificación: faltan " + missing.Count + ", distintos " + wrong.Count);
            var sb = new StringBuilder();
            if (wrong.Count > 0)
            {
                sb.Append("No es una copia limpia de v1.0.0 (¿ya parcheada?). Distintos: ");
                sb.Append(Preview(wrong));
            }
            if (missing.Count > 0)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("Faltan ficheros: ").Append(Preview(missing));
            }
            throw new InstallException("original/ no es Link's Awakening DX HD v1.0.0", sb.ToString());
        }

        private static string Preview(List<string> names)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < names.Count && i < 3; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(names[i]);
            }
            if (names.Count > 3) sb.Append(" y " + (names.Count - 3) + " más");
            return sb.ToString();
        }

        private void Patch(Bundle bundle, List<SourceFile> files)
        {
            var targets = AssetTargets.Get(true);
            var derived = AssetTargets.GetTargetNames(true);

            Directory.CreateDirectory(ContentDir);
            Directory.CreateDirectory(DataDir);

            // Cuenta de escrituras para la barra: fuentes no derivadas + derivados con parche.
            int total = 0;
            foreach (var f in files)
            {
                if (derived.Contains(f.Name)) continue;
                total++;
                if (targets.TryGetValue(f.Name, out var d))
                    foreach (var n in d)
                        if (bundle.Patches.ContainsKey(n)) total++;
            }
            SetTotal(total);

            int patched = 0, copied = 0, generated = 0;
            foreach (var f in files)
            {
                // Los derivados se regeneran desde su master: lo que hubiera en original/ se ignora.
                if (derived.Contains(f.Name))
                    continue;

                _current = f.Relative;
                string dest = Path.Combine(_baseDir, f.Relative);
                string destDir = Path.GetDirectoryName(dest);
                Directory.CreateDirectory(destDir);

                byte[] source = bundle.Overrides.TryGetValue(f.Name, out var over)
                    ? over
                    : File.ReadAllBytes(f.Path);

                if (bundle.Patches.TryGetValue(f.Name, out var delta))
                {
                    WriteFile(dest, Apply(f.Name, source, delta));
                    patched++;
                }
                else
                {
                    WriteFile(dest, source);
                    copied++;
                }
                Advance();

                if (targets.TryGetValue(f.Name, out var derivedNames))
                {
                    foreach (var name in derivedNames)
                    {
                        if (!bundle.Patches.TryGetValue(name, out var dDelta))
                            continue;
                        _current = name + " <- " + f.Name;
                        WriteFile(Path.Combine(destDir, name), Apply(name, source, dDelta));
                        generated++;
                        Advance();
                    }
                }
            }
            Log("parcheados " + patched + ", copiados " + copied + ", generados " + generated);
        }

        private static byte[] Apply(string name, byte[] source, byte[] delta)
        {
            try
            {
                return VcdiffDecoder.Apply(source, delta);
            }
            catch (Exception e)
            {
                throw new InstallException("Fallo aplicando el parche de " + name, e.Message);
            }
        }

        private void WriteExtras(Bundle bundle)
        {
            foreach (var kv in bundle.Extras)
            {
                _current = kv.Key;
                string dest = Path.Combine(_baseDir, kv.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                WriteFile(dest, kv.Value);
            }
            Log("extras: " + bundle.Extras.Count);
        }

        // Réplica de ladxhd_patcher_source_code/Program/CleanUp.cs.
        private static readonly string[] JunkMaps =
        {
            "cave bird.map.data", "dungeon 7_2d.map.data", "dungeon_end.map.data",
            "dungeon3_1.map", "dungeon3_1.map.data", "dungeon3_2.map", "dungeon3_2.map.data",
            "dungeon3_3.map", "dungeon3_3.map.data", "dungeon3_4.map", "dungeon3_4.map.data",
        };
        private static readonly string[] JunkMiniMapData = { "three_1.txt", "three_2.txt", "three_3.txt" };

        private void CleanJunkMaps()
        {
            string maps = Path.Combine(DataDir, "Maps");
            foreach (var n in JunkMaps)
                TryDelete(Path.Combine(maps, n));
            foreach (var n in JunkMiniMapData)
                TryDelete(Path.Combine(DataDir, "Dungeon", n));
            if (Directory.Exists(maps))
                foreach (var p in Directory.GetFiles(maps, "0 test map*"))
                    TryDelete(p);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void WriteFile(string path, byte[] data)
        {
            File.WriteAllBytes(path, data);
        }

        // ----------------------------------------------------------------- progreso / log

        private void SetTotal(int total)
        {
            Volatile.Write(ref _done, 0);
            Volatile.Write(ref _total, total);
        }

        private void Advance() => Interlocked.Increment(ref _done);

        private void Fail(string message, string detail)
        {
            _error = message;
            _errorDetail = detail;
            Log("FALLO: " + message + (detail != null ? " | " + detail.Replace('\n', ' ') : ""));
            _stage = Stage.Failed;
        }

        private void Log(string line)
        {
            lock (_log) _log.Append(line).Append('\n');
            try { _logSink?.Invoke(line); } catch { }
        }

        // ----------------------------------------------------------------- tipos auxiliares

        private struct SourceFile
        {
            public string Path;
            public string Relative;
            public string Name;
        }

        public sealed class InstallException : Exception
        {
            public string Detail { get; }
            public InstallException(string message, string detail) : base(message) { Detail = detail; }
        }

        public struct ManifestEntry
        {
            public string Name;
            public long Size;
            public ulong Hash;
        }

        /// <summary>Contenido de switch-assets.zip cargado en memoria (~25 MB).</summary>
        public sealed class Bundle
        {
            public string Version = "";
            public readonly List<ManifestEntry> Manifest = new List<ManifestEntry>();
            /// <summary>nombre de fichero → .vcdiff</summary>
            public readonly Dictionary<string, byte[]> Patches = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            /// <summary>ruta relativa (Data/…) → bytes a escribir tal cual</summary>
            public readonly Dictionary<string, byte[]> Extras = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            /// <summary>nombre → bytes que sustituyen al fichero de original/ como fuente (dungeon3_1.map).</summary>
            public readonly Dictionary<string, byte[]> Overrides = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            public static Bundle Load(Func<Stream> open, Action<string> log)
            {
                var b = new Bundle();
                Stream s;
                try { s = open(); }
                catch (Exception e) { throw new InstallException("No se puede abrir el paquete de parches embebido", e.Message); }
                if (s == null)
                    throw new InstallException("El NRO no lleva el paquete de parches", BundleResourceName + " no está embebido");

                using (s)
                using (var zip = new ZipArchive(s, ZipArchiveMode.Read, leaveOpen: false))
                {
                    foreach (var e in zip.Entries)
                    {
                        if (e.Name.Length == 0) continue; // directorio
                        string full = e.FullName.Replace('\\', '/');
                        byte[] bytes = ReadEntry(e);

                        if (full == "version.txt")
                            b.Version = Encoding.UTF8.GetString(bytes).Trim();
                        else if (full == "manifest.txt")
                            b.ParseManifest(Encoding.UTF8.GetString(bytes));
                        else if (full.StartsWith("patches/") && full.EndsWith(".vcdiff"))
                            b.Patches[e.Name.Substring(0, e.Name.Length - ".vcdiff".Length)] = bytes;
                        else if (full.StartsWith("extra/"))
                        {
                            string rel = full.Substring("extra/".Length);
                            b.Extras[rel] = bytes;
                            // dungeon3_1.map(.data): el patcher restaura la versión v1.0.0 desde
                            // sus recursos antes de parchear (RestoreOriginalDungeon3Map).
                            if (rel.StartsWith("Data/Maps/dungeon3_1.map", StringComparison.OrdinalIgnoreCase))
                            {
                                b.Overrides[e.Name] = bytes;
                                b.Extras.Remove(rel);
                            }
                        }
                    }
                }
                log?.Invoke("bundle v" + b.Version + ": " + b.Patches.Count + " parches, " + b.Manifest.Count +
                            " entradas de manifiesto, " + b.Extras.Count + " extras");
                if (b.Patches.Count == 0)
                    throw new InstallException("El paquete de parches está vacío", null);
                return b;
            }

            private static byte[] ReadEntry(ZipArchiveEntry e)
            {
                var buf = new byte[e.Length];
                using (var es = e.Open())
                {
                    int off = 0;
                    while (off < buf.Length)
                    {
                        int n = es.Read(buf, off, buf.Length - off);
                        if (n <= 0) throw new InstallException("ZIP truncado en " + e.FullName, null);
                        off += n;
                    }
                }
                return buf;
            }

            private void ParseManifest(string text)
            {
                foreach (var raw in text.Split('\n'))
                {
                    var line = raw.TrimEnd('\r');
                    if (line.Length == 0 || line[0] == '#') continue;
                    var parts = line.Split('\t');
                    if (parts.Length != 3) continue;
                    Manifest.Add(new ManifestEntry
                    {
                        Name = parts[0],
                        Size = long.Parse(parts[1]),
                        Hash = Convert.ToUInt64(parts[2], 16),
                    });
                }
            }
        }

        /// <summary>FNV-1a de 64 bits; tiene que coincidir con tools/assets_bundle.py.</summary>
        public static class Fnv1a64
        {
            public static ulong OfFile(string path)
            {
                ulong h = 0xcbf29ce484222325UL;
                var buf = new byte[1 << 16];
                using (var fs = File.OpenRead(path))
                {
                    int n;
                    while ((n = fs.Read(buf, 0, buf.Length)) > 0)
                    {
                        for (int i = 0; i < n; i++)
                        {
                            h ^= buf[i];
                            h *= 0x100000001b3UL;
                        }
                    }
                }
                return h;
            }
        }
    }
}
