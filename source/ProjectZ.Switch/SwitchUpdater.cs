#if SWITCH
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace ProjectZ.Switch.Update
{
    /// <summary>
    /// Updater in-app: comprueba, descarga, verifica e instala una versión nueva del NRO sin
    /// pasar por el PC. Mismo diseño que el del port de Minish Cap del mismo autor, que lleva
    /// en producción desde su v1.3 y está probado en hardware.
    ///
    /// Las tres propiedades que hacen que esto sea seguro:
    ///
    ///  1. **El manifiesto manda, y es nuestro.** Se lee de nuestro repo por HTTPS y fija
    ///     tamaño y SHA-256 ANTES de bajar un byte. Esquema cerrado: ver SwitchUpdateManifest.
    ///  2. **Nada toca el NRO instalado hasta que hay copia de seguridad verificada.** El orden
    ///     es: descargar a staging → verificar staging → hashear el NRO actual → copiarlo a .bak
    ///     → verificar el .bak → apuntar el diario → copiar staging encima → verificar.
    ///  3. **Cualquier fallo restaura.** Si la copia final sale mal, se vuelve a poner el .bak
    ///     (verificado contra su propio hash) y se avisa.
    ///
    /// Sobrescribir el NRO que se está ejecutando es seguro: hbloader lo lee ENTERO a memoria
    /// al lanzarlo y nuestro NRO no lleva romfs (elf2nro se invoca sin --romfsdir), así que
    /// libnx no mantiene ningún descriptor abierto sobre él. Los bytes de la SD no respaldan
    /// nada vivo. Aun así la instalación pide reiniciar: el proceso en marcha sigue siendo el
    /// viejo.
    ///
    /// REGLA DE ORO DEL PORT: aquí ninguna ruta lleva prefijo "sdmc:". Todas salen de
    /// AppContext.BaseDirectory ("/switch/zelda-ladxhd/"), que es lo único seguro para las APIs
    /// de ficheros de C#. El "sdmc:" se añade en <see cref="SwitchNet.ToNative"/> y solo para
    /// cruzar a C.
    /// </summary>
    public static class SwitchUpdater
    {
        /// <summary>Versión de ESTE port (no la de upstream). Es lo que se compara con el manifiesto.</summary>
        public const string PortVersion = "1.2.0";

        private const string ManifestUrl =
            "https://raw.githubusercontent.com/Alexgg1014/" +
            "The-Legend-of-Zelda-Link-s-Awakening-DX-HD-Alek-s-Ultimate-NX-Edition/main/update_manifest.json";

        private const string CanonicalNroName = "ProjectZ.Switch.nro";
        private const string StagedNroName = "ProjectZ.Switch.new.nro";

        public enum Status
        {
            Idle,               // aún no se ha mirado
            Checking,
            Unknown,            // no hubo red; no se dice nada al jugador
            UpToDate,
            Available,
            Downloading,
            Verifying,
            Installing,
            InstalledRestart,   // listo: hay que reiniciar
            Failed,
        }

        private enum JournalState
        {
            Idle = 0,
            Downloaded = 1,
            Installing = 2,
            Installed = 3,
            RolledBack = 4,
        }

        // ----------------------------------------------------------------- estado (lo lee la UI)

        private static volatile Status _state = Status.Idle;
        private static volatile string _latestVersion;
        private static volatile string _error;
        private static volatile string[] _changelog;
        private static Thread _worker;
        private static readonly object Gate = new object();

        public static Status State => _state;
        public static string LatestVersion => _latestVersion;
        public static string Error => _error;
        public static string[] Changelog => _changelog ?? Array.Empty<string>();

        /// <summary>0..1000 mientras se descarga; 1000 en el resto de fases finales.</summary>
        public static int ProgressPermille =>
            _state == Status.Downloading ? SwitchNet.ProgressPermille : (_state == Status.Idle ? 0 : 1000);

        /// <summary>true si hay un trabajo en curso: la UI deshabilita el botón.</summary>
        public static bool Busy =>
            _state == Status.Checking || _state == Status.Downloading ||
            _state == Status.Verifying || _state == Status.Installing;

        private static SwitchUpdateManifest _manifest;

        // ----------------------------------------------------------------- rutas

        private static string BaseDir
        {
            get
            {
                var dir = AppContext.BaseDirectory;
                if (string.IsNullOrEmpty(dir))
                    dir = "/switch/zelda-ladxhd/";
                return dir;
            }
        }

        private static string UpdateDir => Path.Combine(BaseDir, "update");
        private static string StagedNro => Path.Combine(UpdateDir, StagedNroName);
        private static string JournalFile => Path.Combine(UpdateDir, "update_state.txt");
        private static string ManifestFile => Path.Combine(UpdateDir, "update_manifest.json");
        private static string LogFile => Path.Combine(UpdateDir, "update.log");

        private static string _currentNro;

        /// <summary>
        /// El NRO a reemplazar es el que lanzó hbmenu, no un nombre fijo: la gente renombra el
        /// fichero (en Minish Cap, actualizar sobre un NRO renombrado fallaba con "NRO actual no
        /// encontrado"). argv[0] del hbloader es la ruta "sdmc:" del NRO lanzado; se valida a
        /// fondo y, si no sirve, se cae al nombre canónico.
        /// </summary>
        private static string CurrentNro
        {
            get
            {
                if (_currentNro != null)
                    return _currentNro;
                string canonical = Path.Combine(BaseDir, CanonicalNroName);
                string resolved = canonical;
                try
                {
                    var args = Environment.GetCommandLineArgs();
                    if (args != null && args.Length > 0)
                    {
                        string a0 = args[0];
                        if (!string.IsNullOrEmpty(a0))
                        {
                            // Normalizar a ruta de C#: fuera el "sdmc:" ANTES de tocar File.*.
                            if (a0.StartsWith("sdmc:", StringComparison.OrdinalIgnoreCase))
                                a0 = a0.Substring(5);
                            a0 = a0.Replace('\\', '/');
                            if (a0.StartsWith("/switch/", StringComparison.Ordinal) &&
                                a0.EndsWith(".nro", StringComparison.OrdinalIgnoreCase) &&
                                a0.IndexOf("..", StringComparison.Ordinal) < 0 &&
                                File.Exists(a0))
                            {
                                resolved = a0;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    resolved = canonical;
                }
                _currentNro = resolved;
                Log("nro objetivo = " + resolved);
                return _currentNro;
            }
        }

        private static string BackupNro => CurrentNro.Substring(0, CurrentNro.Length - 4) + ".bak";

        // ----------------------------------------------------------------- utilidades

        private static void Log(string line)
        {
            try
            {
                Directory.CreateDirectory(UpdateDir);
                File.AppendAllText(LogFile, "[update] " + line + "\n");
            }
            catch (Exception)
            {
                // El log es diagnóstico: que no pueda escribirse nunca puede tumbar una update.
            }
        }

        private static void Fail(string error)
        {
            _error = error;
            _state = Status.Failed;
            Log("FALLO: " + error);
        }

        /// <summary>Copia con fsync + commit de la SD. Sin el commit, fsdev puede perder la escritura.</summary>
        private static bool CopyDurable(string from, string to)
        {
            if (from == null || to == null || string.Equals(from, to, StringComparison.Ordinal))
                return false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                var buf = new byte[64 * 1024];
                using (var src = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.Read, buf.Length))
                using (var dst = new FileStream(to, FileMode.Create, FileAccess.Write, FileShare.None, buf.Length))
                {
                    int n;
                    while ((n = src.Read(buf, 0, buf.Length)) > 0)
                        dst.Write(buf, 0, n);
                    dst.Flush(true);
                }
                SwitchNet.CommitSd();
                return true;
            }
            catch (Exception e)
            {
                Log("copia " + from + " -> " + to + " fallo: " + e.Message);
                return false;
            }
        }

        private static bool WriteDurable(string path, string text)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var bytes = Encoding.UTF8.GetBytes(text);
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                }
                SwitchNet.CommitSd();
                return true;
            }
            catch (Exception e)
            {
                Log("escritura " + path + " fallo: " + e.Message);
                return false;
            }
        }

        private static bool VerifyNro(string path, long expectedSize, string expectedSha256)
        {
            long size;
            string hash = SwitchSha256.HashFile(path, out size);
            if (hash == null)
            {
                Log("verificacion: no se pudo leer " + path);
                return false;
            }
            if (size != expectedSize)
            {
                Log("verificacion: " + path + " mide " + size + ", se esperaba " + expectedSize);
                return false;
            }
            if (!string.Equals(hash, expectedSha256, StringComparison.Ordinal))
            {
                Log("verificacion: hash distinto en " + path);
                return false;
            }
            return true;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { }
        }

        // ----------------------------------------------------------------- diario

        private sealed class Journal
        {
            public JournalState State;
            public string TargetVersion = "";
            public long TargetSize;
            public string TargetSha256 = "";
            public long BackupSize;
            public string BackupSha256 = "";
        }

        private static uint Fnv1a(string s)
        {
            uint h = 2166136261;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 16777619;
            }
            return h;
        }

        private static string JournalBody(Journal j) =>
            "schema=1\n" +
            "state=" + (int)j.State + "\n" +
            "target_version=" + j.TargetVersion + "\n" +
            "target_size=" + j.TargetSize + "\n" +
            "target_sha256=" + j.TargetSha256 + "\n" +
            "backup_size=" + j.BackupSize + "\n" +
            "backup_sha256=" + j.BackupSha256 + "\n";

        private static bool WriteJournal(Journal j)
        {
            string body = JournalBody(j);
            return WriteDurable(JournalFile, body + "crc=" + Fnv1a(body).ToString("x8") + "\n");
        }

        private static Journal ReadJournal()
        {
            try
            {
                if (!File.Exists(JournalFile))
                    return null;
                string all = File.ReadAllText(JournalFile);
                int crcAt = all.LastIndexOf("crc=", StringComparison.Ordinal);
                if (crcAt < 0) return null;
                string body = all.Substring(0, crcAt);
                string crc = all.Substring(crcAt + 4).Trim();
                if (!string.Equals(crc, Fnv1a(body).ToString("x8"), StringComparison.Ordinal))
                {
                    Log("diario con crc invalido; se ignora");
                    return null;
                }
                var j = new Journal();
                foreach (var raw in body.Split('\n'))
                {
                    int eq = raw.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = raw.Substring(0, eq);
                    string v = raw.Substring(eq + 1).Trim();
                    switch (k)
                    {
                        case "state": j.State = (JournalState)int.Parse(v); break;
                        case "target_version": j.TargetVersion = v; break;
                        case "target_size": j.TargetSize = long.Parse(v); break;
                        case "target_sha256": j.TargetSha256 = v; break;
                        case "backup_size": j.BackupSize = long.Parse(v); break;
                        case "backup_sha256": j.BackupSha256 = v; break;
                    }
                }
                return j;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ----------------------------------------------------------------- comprobación

        /// <summary>
        /// Arranca la comprobación en segundo plano. Si no hay red, acaba en
        /// <see cref="Status.Unknown"/> y la UI no enseña nada: una consola sin wifi no tiene
        /// por qué ver un error.
        /// </summary>
        public static void StartCheck()
        {
            lock (Gate)
            {
                if (Busy) return;
                _error = null;
                _state = Status.Checking;
                StartWorker(CheckWorker);
            }
        }

        private static void StartWorker(ThreadStart body)
        {
            _worker = new Thread(() =>
            {
                try { body(); }
                catch (Exception e) { Fail("Error interno: " + e.Message); }
            });
            _worker.IsBackground = true;
            _worker.Start();
        }

        private static void CheckWorker()
        {
            if (!SwitchNet.Init())
            {
                Log("sin red: socket/curl no arrancaron");
                _state = Status.Unknown;
                return;
            }

            long status;
            string body = SwitchNet.HttpGet(ManifestUrl, SwitchUpdateManifest.MaxBytes, out status);
            Log("manifiesto http=" + status + " bytes=" + (body == null ? 0 : body.Length));
            if (body == null)
            {
                // Offline o GitHub caído: silencio. El juego instalado sigue entero.
                _state = Status.Unknown;
                return;
            }

            var manifest = SwitchUpdateManifest.Parse(body);
            if (manifest == null)
            {
                Fail("Manifiesto rechazado");
                return;
            }

            _manifest = manifest;
            _latestVersion = manifest.Version;
            _changelog = manifest.Changelog;
            WriteDurable(ManifestFile, body);

            if (SwitchUpdateManifest.IsNewer(manifest.Version, PortVersion))
            {
                Log("instalada=" + PortVersion + " ultima=" + manifest.Version + " -> HAY ACTUALIZACION");
                _state = Status.Available;
            }
            else
            {
                Log("instalada=" + PortVersion + " ultima=" + manifest.Version + " -> al dia");
                _state = Status.UpToDate;
            }
        }

        // ----------------------------------------------------------------- descarga + instalación

        /// <summary>Descarga, verifica e instala. Solo tiene efecto si hay una versión disponible.</summary>
        public static void StartDownloadAndInstall()
        {
            lock (Gate)
            {
                if (Busy || _state != Status.Available || _manifest == null) return;
                _error = null;
                _state = Status.Downloading;
                StartWorker(DownloadAndInstallWorker);
            }
        }

        private static void DownloadAndInstallWorker()
        {
            var manifest = _manifest;
            if (manifest == null) { Fail("No hay manifiesto"); return; }

            try { Directory.CreateDirectory(UpdateDir); }
            catch (Exception) { Fail("No se pudo crear la carpeta update/"); return; }

            // Ruta de staging fija, jamás la canónica.
            TryDelete(StagedNro);

            Log("descarga: version=" + manifest.Version + " tamano=" + manifest.NroSize + " url=" + manifest.NroUrl);
            long http = SwitchNet.DownloadFile(manifest.NroUrl, StagedNro, manifest.NroSize);
            if (http < 200 || http >= 300)
            {
                Fail("Descarga fallida (" + http + ")");
                TryDelete(StagedNro);
                return;
            }

            _state = Status.Verifying;
            if (!VerifyNro(StagedNro, manifest.NroSize, manifest.NroSha256))
            {
                Fail("La descarga no coincide con el manifiesto");
                TryDelete(StagedNro);
                return;
            }

            var journal = new Journal
            {
                State = JournalState.Downloaded,
                TargetVersion = manifest.Version,
                TargetSize = manifest.NroSize,
                TargetSha256 = manifest.NroSha256,
            };
            if (!WriteJournal(journal)) { Fail("No se pudo escribir el diario"); return; }

            Install(journal);
        }

        private static void Install(Journal journal)
        {
            _state = Status.Installing;

            string current = CurrentNro;
            long currentSize;
            string currentHash = SwitchSha256.HashFile(current, out currentSize);
            if (currentHash == null || currentSize <= 0)
            {
                Fail("No se encuentra el NRO actual");
                return;
            }

            // Solo AHORA puede sobrescribirse un .bak anterior.
            if (!CopyDurable(current, BackupNro) || !VerifyNro(BackupNro, currentSize, currentHash))
            {
                Fail("No se pudo crear una copia de seguridad verificada");
                return;
            }
            journal.BackupSize = currentSize;
            journal.BackupSha256 = currentHash;
            journal.State = JournalState.Installing;
            if (!WriteJournal(journal)) { Fail("No se pudo escribir el diario de instalacion"); return; }
            Log("copia de seguridad verificada en " + BackupNro);

            if (!CopyDurable(StagedNro, current) ||
                !VerifyNro(current, journal.TargetSize, journal.TargetSha256))
            {
                Log("la copia final fallo; restaurando");
                if (RestoreBackup(journal))
                {
                    journal.State = JournalState.RolledBack;
                    WriteJournal(journal);
                    Fail("Instalacion fallida; se restauro la version anterior");
                }
                else
                {
                    Fail("Instalacion fallida; copia el .bak sobre el .nro a mano");
                }
                return;
            }

            journal.State = JournalState.Installed;
            WriteJournal(journal);
            TryDelete(StagedNro);
            Log("instalacion completa, version=" + journal.TargetVersion);
            _state = Status.InstalledRestart;
        }

        private static bool RestoreBackup(Journal journal)
        {
            if (!VerifyNro(BackupNro, journal.BackupSize, journal.BackupSha256))
            {
                Log("la copia de seguridad tampoco verifica: NO se restaura nada");
                return false;
            }
            if (!CopyDurable(BackupNro, CurrentNro) ||
                !VerifyNro(CurrentNro, journal.BackupSize, journal.BackupSha256))
            {
                Log("restauracion FALLIDA");
                return false;
            }
            Log("restaurada la copia de seguridad verificada");
            return true;
        }

        // ----------------------------------------------------------------- arranque

        /// <summary>
        /// Se llama una vez al arrancar. No toca la red: solo mira si quedó un diario a medias
        /// de un intento anterior, para dejarlo registrado y limpiar el staging.
        /// </summary>
        public static void OnBoot()
        {
            var j = ReadJournal();
            if (j == null)
                return;
            Log("diario previo: estado=" + j.State + " version=" + j.TargetVersion);
            if (j.State == JournalState.Installed || j.State == JournalState.RolledBack)
                TryDelete(StagedNro);
        }
    }
}
#endif
