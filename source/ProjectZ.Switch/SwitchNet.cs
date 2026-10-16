#if SWITCH
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ProjectZ.Switch.Update
{
    /// <summary>
    /// Puente a <c>switch_net.c</c> (libcurl sobre el backend TLS de libnx).
    ///
    /// Mismo patrón que el resto de la capa: <c>DllImport("*")</c> porque todo queda enlazado
    /// estáticamente en el mismo NRO, y las cadenas cruzan como UTF-8 con NUL final.
    ///
    /// OJO con las rutas: lo que se pasa AQUÍ sí lleva prefijo <c>sdmc:</c>, porque lo resuelve
    /// el devoptab de newlib en C. La regla de oro del port (nunca "sdmc:" en una API de
    /// ficheros de C#) sigue intacta: <see cref="ToNative"/> es el único sitio que lo añade.
    /// </summary>
    internal static class SwitchNet
    {
        [DllImport("*", EntryPoint = "SwitchNetInit")]
        private static extern int NativeInit();

        [DllImport("*", EntryPoint = "SwitchNetExit")]
        private static extern void NativeExit();

        [DllImport("*", EntryPoint = "SwitchCommitSd")]
        private static extern void NativeCommitSd();

        [DllImport("*", EntryPoint = "SwitchHttpProgressPermille")]
        private static extern uint NativeProgress();

        [DllImport("*", EntryPoint = "SwitchHttpGet")]
        private static extern long NativeGet(byte[] url, byte[] outBuf, int cap, out int outLen);

        [DllImport("*", EntryPoint = "SwitchHttpDownloadFile")]
        private static extern long NativeDownload(byte[] url, byte[] dest, ulong expected);

        private static byte[] Utf8(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
            var z = new byte[bytes.Length + 1];
            Buffer.BlockCopy(bytes, 0, z, 0, bytes.Length);
            return z;
        }

        /// <summary>
        /// Ruta de C# ("/switch/zelda-ladxhd/...") a ruta nativa ("sdmc:/switch/zelda-ladxhd/...").
        /// Es el ÚNICO sitio del port donde se construye un "sdmc:".
        /// </summary>
        internal static string ToNative(string managedPath)
        {
            if (string.IsNullOrEmpty(managedPath))
                return managedPath;
            if (managedPath.StartsWith("sdmc:", StringComparison.Ordinal))
                return managedPath;
            return "sdmc:" + managedPath.Replace('\\', '/');
        }

        /// <summary>Levanta sockets + curl. Idempotente. Devuelve false si la consola no tiene red.</summary>
        public static bool Init()
        {
            try { return NativeInit() != 0; }
            catch (Exception) { return false; }
        }

        public static void Shutdown()
        {
            try { NativeExit(); } catch (Exception) { }
        }

        /// <summary>fsdevCommitDevice("sdmc"): sin esto, un corte de luz puede perder la escritura.</summary>
        public static void CommitSd()
        {
            try { NativeCommitSd(); } catch (Exception) { }
        }

        /// <summary>Progreso de la descarga en curso, 0..1000.</summary>
        public static int ProgressPermille
        {
            get { try { return (int)NativeProgress(); } catch (Exception) { return 0; } }
        }

        /// <summary>
        /// GET HTTPS a memoria. Devuelve el cuerpo, o null si el estado HTTP no fue 2xx o falló
        /// el transporte. <paramref name="status"/> recibe el código HTTP (negativo = transporte).
        /// </summary>
        public static string HttpGet(string url, int maxBytes, out long status)
        {
            status = -1;
            try
            {
                var buf = new byte[maxBytes + 1];
                int len;
                status = NativeGet(Utf8(url), buf, buf.Length, out len);
                if (status < 200 || status >= 300 || len <= 0 || len > maxBytes)
                    return null;
                return Encoding.UTF8.GetString(buf, 0, len);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Descarga en streaming a <paramref name="managedDest"/> (ruta de C#, sin "sdmc:").
        /// El tamaño esperado es un límite duro en el lado nativo. Devuelve el código HTTP.
        /// </summary>
        public static long DownloadFile(string url, string managedDest, long expected)
        {
            try { return NativeDownload(Utf8(url), Utf8(ToNative(managedDest)), (ulong)expected); }
            catch (Exception) { return -2; }
        }
    }
}
#endif
