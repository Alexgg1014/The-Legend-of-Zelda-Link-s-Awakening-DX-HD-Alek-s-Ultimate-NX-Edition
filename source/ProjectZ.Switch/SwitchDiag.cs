using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// Diagnostic logging for the Switch build.
    ///
    /// Every method is [Conditional("SWITCH")], so on Windows/Android/Linux the calls
    /// are removed by the compiler and nothing is emitted - these helpers cost nothing
    /// off-Switch and need no #if at the call sites.
    ///
    /// Each write opens/appends/closes, so entries reach the SD card immediately and
    /// survive an abort() or fail-fast. That makes it unsuitable for per-frame logging,
    /// hence LogOnce() for anything on a hot path.
    /// </summary>
    public static class SwitchDiag
    {
#if SWITCH
        private const string LogDir = "sdmc:/switch/zelda-ladxhd/";
        private static readonly object Sync = new object();
        private static readonly HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        private static int _seq;

        private static void Append(string file, string message)
        {
            try
            {
                lock (Sync)
                {
                    _seq++;
                    // Via nativa OBLIGATORIA: File.AppendAllText con una ruta con prefijo
                    // "sdmc:" mata el proceso en consola real (ver la nota larga en
                    // SwitchCrash, donde estaba TryManagedDirCreate). SwitchLiveLog si
                    // entiende el prefijo. Ademas DateTime.Now esta congelado en Switch.
                    SwitchCrash.Live(file, message);
                }
            }
            catch { }
        }
#endif

        /// <summary>Append a line to the given log file under sdmc:/switch/zelda-ladxhd/.</summary>
        [Conditional("SWITCH")]
        public static void Log(string file, string message)
        {
#if SWITCH
            Append(file, message);
#endif
        }

        /// <summary>
        /// Append a line only the first time this dedupKey is seen. For hot paths where
        /// repeated identical entries would stall on SD-card writes.
        /// </summary>
        [Conditional("SWITCH")]
        public static void LogOnce(string file, string dedupKey, string message)
        {
#if SWITCH
            lock (Sync)
            {
                if (!Seen.Add(dedupKey))
                    return;
            }
            Append(file, message);
#endif
        }
    }
}
