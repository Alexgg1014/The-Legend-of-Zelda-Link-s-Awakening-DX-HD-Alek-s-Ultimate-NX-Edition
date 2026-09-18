#if SWITCH
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// Switch crash / breadcrumb diagnostics.
    ///
    /// Breadcrumbs live in a native ring buffer (nativeaot_shims.c) rather than managed
    /// memory, so they survive an abort()/fail-fast where no C# can run any more. Nothing
    /// is written to the SD card per frame; the ring is only dumped on a managed exception,
    /// a native abort, or the hang watchdog firing.
    ///
    /// Entirely diagnostic: it never swallows an exception and never alters control flow.
    /// </summary>
    public static class SwitchCrash
    {
        public const string CrashDir = "sdmc:/switch/zelda-ladxhd/crash/";

        [DllImport("*", EntryPoint = "SwitchBreadcrumb")]
        private static extern void NativeBreadcrumb(byte[] utf8);

        [DllImport("*", EntryPoint = "SwitchDumpBreadcrumbs")]
        private static extern void NativeDump(byte[] pathUtf8, byte[] reasonUtf8);

        /// <summary>
        /// Synchronous append+flush+close of one line, in native code. This is the only
        /// logging path proven to survive the TEST 1 failure, where the process vanished
        /// without reaching any handler. Use for navigation/state boundaries only.
        /// </summary>
        [DllImport("*", EntryPoint = "SwitchLiveLog")]
        private static extern void NativeLive(byte[] fileUtf8, byte[] textUtf8);

        [DllImport("*", EntryPoint = "SwitchSetPhase")]
        private static extern void NativeSetPhase(byte[] utf8);

        [DllImport("*", EntryPoint = "SwitchDumpBreadcrumbsLatest")]
        private static extern void NativeDumpLatest(byte[] pathUtf8, byte[] reasonUtf8);

        /// <summary>
        /// Record the current execution position. Overwrites a single native slot, so it is
        /// safe to call many times per frame - unlike Drop(), which appends to the ring.
        /// The first attempt logged every frame with Drop() and drowned the 192-slot ring in
        /// FRAME lines, leaving only ~1.6s of history and no actual events.
        /// </summary>
        public static void Phase(string phase)
        {
            try { NativeSetPhase(Utf8(phase)); } catch { }
        }

        public const string NavLogFile = "nav_live.log";
        public const string NewGameLogFile = "newgame_live.log";
        public const string InitLogFile = "diag_init.txt";

        private static int _liveSeq;

        /// <summary>Write one durable line to crash/&lt;file&gt;. Never throws.</summary>
        public static void Live(string file, string text)
        {
            try
            {
                int n = Interlocked.Increment(ref _liveSeq);
                string stamp;
                try { stamp = "+" + NowMs.ToString("000000") + "ms"; } catch { stamp = "+??????ms"; }
                int tid;
                try { tid = Thread.CurrentThread.ManagedThreadId; } catch { tid = -1; }
                NativeLive(Utf8(file), Utf8(n.ToString("00000") + " " + stamp + " t" + tid + " | " + text));
            }
            catch { }
        }

        private static readonly object Sync = new object();
        private static bool _installed;
        private static int _dumpSeq;

        // Watchdog counters. Written by the game thread, read by the watchdog thread.
        private static long _updateEntered, _updateCompleted, _drawEntered, _drawCompleted;
        private static long _lastProgressTicks;
        private static string _currentSubsystem = "(none)";
        private static bool _hangReported;

        // DateTime.UtcNow is frozen at 00:00:00.000 on this Switch build (every log line
        // from the first run proved it), which silently made the old watchdog dead code:
        // "now - last" was always zero, so the 5s threshold could never be reached.
        // Stopwatch is monotonic and independent of the wall clock, so timing uses it.
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static long NowMs => Clock.ElapsedMilliseconds;

        public static long UpdateEntered => Interlocked.Read(ref _updateEntered);
        public static long UpdateCompleted => Interlocked.Read(ref _updateCompleted);
        public static long DrawEntered => Interlocked.Read(ref _drawEntered);
        public static long DrawCompleted => Interlocked.Read(ref _drawCompleted);

        private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes((s ?? "") + "\0");

        // ---------------------------------------------------------------- breadcrumbs

        /// <summary>Record a breadcrumb. Cheap: no allocation beyond the UTF-8 copy, no I/O.</summary>
        public static void Drop(string message)
        {
            try { NativeBreadcrumb(Utf8(message)); } catch { }
        }

        /// <summary>Breadcrumb for a navigation event. See NavReason for the taxonomy.</summary>
        public static void Nav(string phase, string op, string reason, string detail)
        {
            Drop("NAV " + phase + " " + op + " reason=" + reason + " " + detail);
        }

        public static void SetSubsystem(string name)
        {
            _currentSubsystem = name ?? "(null)";
            Drop("SUBSYS " + _currentSubsystem);
        }

        // ---------------------------------------------------------------- frame counters

        public static void BeginUpdate()
        {
            Interlocked.Increment(ref _updateEntered);
        }

        public static void EndUpdate()
        {
            Interlocked.Increment(ref _updateCompleted);
            Interlocked.Exchange(ref _lastProgressTicks, NowMs);
            _hangReported = false;
        }

        public static void BeginDraw()
        {
            Interlocked.Increment(ref _drawEntered);
        }

        public static void EndDraw()
        {
            Interlocked.Increment(ref _drawCompleted);
            Interlocked.Exchange(ref _lastProgressTicks, NowMs);
            _hangReported = false;
        }

        // ---------------------------------------------------------------- install

        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            // Sanity check FIRST: prove the logger can actually reach the SD card before
            // anything else. Written through the native path (SwitchLiveLog), which also
            // mkdir's the crash directory - managed Directory.CreateDirectory is known to
            // be unreliable on the Switch filesystem (see Program.cs).
            Live(InitLogFile, "DIAGNOSTICS INITIALIZED");
            Live(InitLogFile, "  resolvedDir = " + CrashDir);
            Live(InitLogFile, "  writePath   = " + CrashDir + InitLogFile + " (via native SwitchLiveLog)");
            Live(InitLogFile, "  clock probe: DateTime.UtcNow = " + SafeGet(() => DateTime.UtcNow.ToString("HH:mm:ss.fff")));
            Live(InitLogFile, "  clock probe: Environment.TickCount64 = " + SafeGet(() => Environment.TickCount64.ToString()));
            Live(InitLogFile, "  clock probe: Stopwatch.ElapsedMilliseconds = " + SafeGet(() => NowMs.ToString())
                 + " (frequency=" + SafeGet(() => Stopwatch.Frequency.ToString()) + ")");
            Drop("DIAG_NATIVE_RING_READY");
            Live(InitLogFile, "  native breadcrumb ring: DIAG_NATIVE_RING_READY pushed");

            Interlocked.Exchange(ref _lastProgressTicks, NowMs);

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                WriteReport("AppDomain.UnhandledException (terminating=" + e.IsTerminating + ")", ex);
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                WriteReport("TaskScheduler.UnobservedTaskException", e.Exception);
                // Deliberately NOT calling e.SetObserved(): do not change behaviour.
            };

            var wd = new Thread(WatchdogLoop) { IsBackground = true, Name = "SwitchHangWatchdog" };
            wd.Start();

            Drop("DIAG installed (crash handlers + hang watchdog)");
            Live(InitLogFile, "DIAGNOSTICS READY (handlers installed, watchdog starting)");
        }

        // ELIMINADO: TryManagedDirCreate().
        //
        // Era una sonda de diagnostico que llamaba a Directory.CreateDirectory(CrashDir).
        // En Eden lanzaba una IOException capturable; en consola REAL mata el proceso, y un
        // catch(Exception) no puede atrapar un Data Abort de ARM. Fue la causa del "este
        // programa se ha cerrado" en hardware: crash report 2168-0002, Data Abort en
        // address 0x68 con fault address 0x0, pila
        // Program.Main -> SwitchCrash.Install -> TryManagedDirCreate ->
        // Directory.CreateDirectory -> SystemNative_MkDir -> mkdir.
        //
        // Motivo: .NET en Unix solo considera absoluta una ruta que empiece por '/'.
        // "sdmc:/..." no lo hace, asi que le antepone el cwd y queda
        // "/switch/zelda-ladxhd/sdmc:/switch/zelda-ladxhd/crash/". newlib no encuentra
        // devoptab para esa ruta y llama a traves de un puntero nulo (offset 0x68).
        //
        // REGLA: nunca pasar una ruta con prefijo "sdmc:" a una API de ficheros de C#.
        // Usar siempre la via nativa (SwitchLiveLog / NativeDump), que si entiende el prefijo.

        private static void WatchdogLoop()
        {
            const int hangSeconds = 5;
            try { Live(InitLogFile, "WATCHDOG THREAD STARTED thread=" + Thread.CurrentThread.ManagedThreadId); }
            catch { }

            while (true)
            {
                try
                {
                    Thread.Sleep(1000);

                    // Periodic ring flush. The process can vanish with no handler running,
                    // so mirror the RAM ring to disk every second. This is what makes cheap
                    // per-frame breadcrumbs durable without writing to SD every frame.
                    NativeDumpLatest(Utf8(CrashDir + "ring_latest.log"), Utf8("periodic +" + NowMs + "ms"
                        + " subsys=" + _currentSubsystem
                        + " upd=" + UpdateEntered + "/" + UpdateCompleted
                        + " draw=" + DrawEntered + "/" + DrawCompleted));

                    if (_hangReported) continue;

                    // No reportar durante el arranque. Install() se llama ahora desde
                    // Program.cs, antes de que el juego cargue contenido, y con la carga
                    // sincrona (CanCreateGraphicsResourcesOnWorkerThread=false) eso tarda
                    // mas de 5 s. Sin esto el watchdog escribia un "HANG DETECTED" falso
                    // en cada arranque, con updateEntered=0 y subsystem=Startup.
                    if (UpdateCompleted == 0 && DrawCompleted == 0) continue;

                    var last = Interlocked.Read(ref _lastProgressTicks);
                    var elapsedSec = (NowMs - last) / 1000.0;
                    if (elapsedSec < hangSeconds) continue;

                    _hangReported = true;   // log once per hang, do not terminate

                    // Tiny GUARANTEED write first. The full state report touches
                    // GraphicsDevice and the page stack, either of which could block or
                    // fault while the game thread is wedged - so it must never be the
                    // thing standing between us and knowing a hang happened.
                    var marker = "HANG DETECTED after " + ((int)elapsedSec)
                               + "s | subsystem=" + _currentSubsystem
                               + " upd=" + UpdateEntered + "/" + UpdateCompleted
                               + " draw=" + DrawEntered + "/" + DrawCompleted;
                    Live(NavLogFile, marker);
                    Live(NewGameLogFile, marker);

                    // Now the expensive part, which is allowed to fail.
                    WriteReport("HANG DETECTED (no Update/Draw completed for "
                                + ((int)elapsedSec) + "s)", null);
                }
                catch { }
            }
        }

        // ---------------------------------------------------------------- reporting

        /// <summary>
        /// Writes crash_&lt;n&gt;.txt with full state, then dumps the native breadcrumb ring.
        /// Never throws.
        /// </summary>
        public static void WriteReport(string reason, Exception ex)
        {
            try
            {
                lock (Sync)
                {
                    _dumpSeq++;
                    var sb = new StringBuilder(4096);
                    sb.Append("==================== CRASH / DIAGNOSTIC REPORT ====================\n");
                    Line(sb, "reason", reason);
                    Line(sb, "utc", SafeGet(() => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")));
                    Line(sb, "thread", SafeGet(() =>
                        Thread.CurrentThread.ManagedThreadId + " \"" + (Thread.CurrentThread.Name ?? "(unnamed)") + "\""));

                    sb.Append("\n---- exception ----\n");
                    if (ex == null)
                    {
                        sb.Append("(none - not an exception-driven dump)\n");
                    }
                    else
                    {
                        int depth = 0;
                        for (var e = ex; e != null && depth < 8; e = e.InnerException, depth++)
                        {
                            sb.Append(depth == 0 ? "exception: " : "inner[" + depth + "]: ")
                              .Append(SafeGet(() => e.GetType().FullName)).Append('\n');
                            Line(sb, "  message", SafeGet(() => e.Message));
                            sb.Append("  stack:\n").Append(SafeGet(() => e.StackTrace) ?? "  (no stack)").Append('\n');
                        }
                    }

                    sb.Append("\n---- frame counters ----\n");
                    Line(sb, "updateEntered", UpdateEntered.ToString());
                    Line(sb, "updateCompleted", UpdateCompleted.ToString());
                    Line(sb, "drawEntered", DrawEntered.ToString());
                    Line(sb, "drawCompleted", DrawCompleted.ToString());
                    Line(sb, "subsystem", _currentSubsystem);

                    sb.Append("\n---- navigation state ----\n");
                    AppendNavState(sb);

                    sb.Append("\n---- game state ----\n");
                    AppendGameState(sb);

                    sb.Append("\n---- graphics state ----\n");
                    Line(sb, "graphics", SafeGet(SwitchGraphicsSnapshot.Capture));

                    sb.Append("==================================================================\n");

                    // Via nativa: SwitchLiveLog entiende el prefijo "sdmc:" y crea el
                    // directorio el mismo. Las APIs de C# aqui reventaban en consola real
                    // (ver la nota sobre TryManagedDirCreate).
                    var reportFile = "crash_" + _dumpSeq.ToString("00") + ".txt";
                    foreach (var reportLine in sb.ToString().Split('\n'))
                        Live(reportFile, reportLine);

                    Drop("CRASH REPORT written: " + reason);
                    NativeDump(Utf8(CrashDir + "breadcrumbs.log"), Utf8(reason));
                }
            }
            catch
            {
                // Last resort: at least get the breadcrumbs out.
                try { NativeDump(Utf8(CrashDir + "breadcrumbs.log"), Utf8(reason + " (report writer failed)")); }
                catch { }
            }
        }

        private static void AppendNavState(StringBuilder sb)
        {
            Line(sb, "pageStackDepth", SafeGet(() => Game1.UiPageManager.PageStack.Count.ToString()));
            Line(sb, "pageStack", SafeGet(() =>
            {
                var stack = Game1.UiPageManager.PageStack;
                var s = new StringBuilder();
                for (int i = 0; i < stack.Count; i++)
                {
                    s.Append('[').Append(i).Append(']').Append(stack[i]?.Name ?? "<null type>");
                    var present = Game1.UiPageManager.InsideElement.ContainsKey(stack[i]);
                    if (!present) s.Append("  !! NOT IN InsideElement !!");
                    else
                    {
                        var pg = Game1.UiPageManager.InsideElement[stack[i]];
                        s.Append("#").Append(pg == null ? "<null page>" : pg.GetHashCode().ToString("X"));
                    }
                    s.Append(i + 1 < stack.Count ? " -> " : "");
                }
                return s.ToString();
            }));
            Line(sb, "currentPage", SafeGet(() =>
            {
                var p = Game1.UiPageManager.GetCurrentPage();
                return p == null ? "<null>" : p.GetType().Name + "#" + p.GetHashCode().ToString("X");
            }));
            Line(sb, "registeredPages", SafeGet(() => Game1.UiPageManager.InsideElement.Count.ToString()));
            Line(sb, "overlayMenuOpen", SafeGet(() => Game1.GameManager.InGameOverlay.MenuIsOpen().ToString()));
        }

        private static void AppendGameState(StringBuilder sb)
        {
            Line(sb, "saveSlot", SafeGet(() => Game1.GameManager.SaveSlot.ToString()));
            Line(sb, "saveName", SafeGet(() => Game1.GameManager.SaveName));
            Line(sb, "map", SafeGet(() => Map.MapManager.ObjLink?.SaveMap));
            Line(sb, "playerPos", SafeGet(() =>
                Map.MapManager.ObjLink == null ? "<null link>"
                : Map.MapManager.ObjLink.PosX.ToString("0.0") + "," + Map.MapManager.ObjLink.PosY.ToString("0.0")));
            Line(sb, "cameraPos", SafeGet(() =>
                Map.MapManager.Camera == null ? "<null camera>"
                : Map.MapManager.Camera.X.ToString("0.0") + "," + Map.MapManager.Camera.Y.ToString("0.0")));
            Line(sb, "cameraScale", SafeGet(() => Map.MapManager.Camera?.Scale.ToString("0.00")));
            Line(sb, "gameScaleSetting", SafeGet(() => GameSettings.GameScale.ToString()));
        }

        private static void Line(StringBuilder sb, string key, string value)
        {
            sb.Append(key).Append(": ").Append(value ?? "<null>").Append('\n');
        }

        private static string SafeGet(Func<string> f)
        {
            try { return f() ?? "<null>"; }
            catch (Exception e) { return "<threw " + e.GetType().Name + ": " + e.Message + ">"; }
        }
    }

    /// <summary>Why a navigation happened. Deliberately not tied to a physical button.</summary>
    public static class NavReason
    {
        public const string BackNavigation = "BackNavigation";
        public const string PauseOverlayToggle = "PauseOverlayToggle";
        public const string MenuSelection = "MenuSelection";
        public const string Programmatic = "Programmatic";
        public const string Unknown = "Unknown";
    }
}
#endif
