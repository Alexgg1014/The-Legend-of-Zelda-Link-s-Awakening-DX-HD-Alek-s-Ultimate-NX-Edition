#if SWITCH
using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// Benchmark automático del port.
    ///
    /// Recorre solo las 8 combinaciones de panel / desenfoque de UI / niebla, mide cada una
    /// y escribe los resultados en crash/benchmark.log. Evita el A/B manual, que obliga a
    /// leer números en pantalla y es facil de hacer mal: basta con quedarse quieto ~30 s.
    ///
    /// Mide con Stopwatch (DateTime está congelado en Switch) y descarta el primer segundo
    /// de cada paso, que es cuando el cambio de ajustes todavía está asentando (recreación
    /// de render targets, primer frame con el shader nuevo, etc.).
    ///
    /// Al terminar restaura los ajustes que había y deja el log listo para sacarlo por FTP.
    /// </summary>
    public static class SwitchBenchmark
    {
        private const string LogFile = "benchmark.log";

        // Tiempos por paso, en milisegundos.
        private const double SettleMs = 1000.0;
        private const double MeasureMs = 3000.0;

        private struct Step
        {
            public bool Panel;
            public bool Blur;
            public bool Fog;
        }

        // 2 x 2 x 2. El orden agrupa por panel para que los cambios entre pasos consecutivos
        // sean lo mas baratos posible.
        private static readonly Step[] Steps =
        {
            new Step { Panel = true,  Blur = true,  Fog = true  },
            new Step { Panel = true,  Blur = true,  Fog = false },
            new Step { Panel = true,  Blur = false, Fog = true  },
            new Step { Panel = true,  Blur = false, Fog = false },
            new Step { Panel = false, Blur = true,  Fog = true  },
            new Step { Panel = false, Blur = true,  Fog = false },
            new Step { Panel = false, Blur = false, Fog = true  },
            new Step { Panel = false, Blur = false, Fog = false },
        };

        private static readonly double[] ResultMs = new double[Steps.Length];
        private static readonly double[] ResultMin = new double[Steps.Length];
        private static readonly double[] ResultMax = new double[Steps.Length];

        private static readonly Stopwatch Clock = new Stopwatch();

        private static bool _running;
        private static int _stepIndex;
        private static double _stepStartMs;
        private static int _frames;
        private static double _accumMs;
        private static double _minMs, _maxMs;
        private static double _lastFrameMs;
        private static string _status = "";
        private static bool _finished;

        // Ajustes originales, para restaurarlos al acabar.
        private static int _savedPanel;
        private static bool _savedBlur;
        private static bool _savedFog;

        public static bool Running => _running;

        /// <summary>Arranca el benchmark. Ignorado si ya está corriendo.</summary>
        public static void Start()
        {
            if (_running)
                return;

            _savedPanel = SwitchSettings.SecondScreen;
            _savedBlur = GameSettings.DisableUiBlur;
            _savedFog = GameSettings.ModernFogEnabled;

            _running = true;
            _finished = false;
            _stepIndex = -1;
            Clock.Restart();

            SwitchCrash.Live(LogFile, "===== BENCHMARK INICIADO =====");
            SwitchCrash.Live(LogFile, "  " + Steps.Length + " pasos, " +
                (int)((SettleMs + MeasureMs) / 1000) + " s cada uno. NO TE MUEVAS.");

            BeginStep(0);
        }

        private static void BeginStep(int index)
        {
            _stepIndex = index;
            _stepStartMs = Clock.Elapsed.TotalMilliseconds;
            _frames = 0;
            _accumMs = 0;
            _minMs = double.MaxValue;
            _maxMs = 0;

            var step = Steps[index];
            SwitchSettings.SecondScreen = step.Panel ? 1 : 0;

            // OpaqueHudBg = true significa "sin desenfoque".
            GameSettings.DisableUiBlur = !step.Blur;
            GameSettings.ModernFogEnabled = step.Fog;
        }

        private static string Describe(Step s) =>
            "panel=" + (s.Panel ? "ON " : "OFF") +
            " blur=" + (s.Blur ? "ON " : "OFF") +
            " fog=" + (s.Fog ? "ON " : "OFF");

        /// <summary>Llamar una vez por frame dibujado, con el delta de ese frame.</summary>
        public static void Tick(double frameMs)
        {
            if (!_running)
                return;

            _lastFrameMs = frameMs;
            var elapsed = Clock.Elapsed.TotalMilliseconds - _stepStartMs;

            // Primer segundo: descartado, el cambio de ajustes aún está asentando.
            if (elapsed < SettleMs)
            {
                _status = "ASENTANDO  " + (_stepIndex + 1) + "/" + Steps.Length;
                return;
            }

            if (elapsed < SettleMs + MeasureMs)
            {
                _frames++;
                _accumMs += frameMs;
                if (frameMs < _minMs) _minMs = frameMs;
                if (frameMs > _maxMs) _maxMs = frameMs;
                _status = "MIDIENDO  " + (_stepIndex + 1) + "/" + Steps.Length +
                          "  " + Describe(Steps[_stepIndex]);
                return;
            }

            // Paso terminado: guardar y pasar al siguiente.
            ResultMs[_stepIndex] = _frames > 0 ? _accumMs / _frames : 0;
            ResultMin[_stepIndex] = _minMs == double.MaxValue ? 0 : _minMs;
            ResultMax[_stepIndex] = _maxMs;

            if (_stepIndex + 1 < Steps.Length)
            {
                BeginStep(_stepIndex + 1);
                return;
            }

            Finish();
        }

        private static void Finish()
        {
            _running = false;
            _finished = true;
            _status = "BENCHMARK TERMINADO - mira crash/benchmark.log";

            SwitchCrash.Live(LogFile, "----- resultados (ms por frame, menos es mejor) -----");
            for (var i = 0; i < Steps.Length; i++)
            {
                var avg = ResultMs[i];
                var fps = avg > 0 ? 1000.0 / avg : 0;
                SwitchCrash.Live(LogFile,
                    "  " + Describe(Steps[i]) +
                    " | media " + avg.ToString("00.00") + " ms" +
                    " (" + fps.ToString("00.0") + " fps)" +
                    " | mejor " + ResultMin[i].ToString("00.00") +
                    " | peor " + ResultMax[i].ToString("00.00"));
            }

            // Costes derivados: lo que de verdad interesa.
            SwitchCrash.Live(LogFile, "----- coste de cada cosa (ms) -----");
            Cost("desenfoque de UI", 0, 2);   // panel ON, fog ON: blur ON vs OFF
            Cost("niebla",           0, 1);   // panel ON, blur ON: fog ON vs OFF
            Cost("panel dual",       0, 4);   // blur ON, fog ON: panel ON vs OFF
            SwitchCrash.Live(LogFile, "===== BENCHMARK TERMINADO =====");

            // Restaurar lo que habia.
            SwitchSettings.SecondScreen = _savedPanel;
            GameSettings.DisableUiBlur = _savedBlur;
            GameSettings.ModernFogEnabled = _savedFog;
        }

        private static void Cost(string name, int withIndex, int withoutIndex)
        {
            var delta = ResultMs[withIndex] - ResultMs[withoutIndex];
            SwitchCrash.Live(LogFile, "  " + name + ": " + delta.ToString("+00.00;-00.00") + " ms");
        }

        /// <summary>HUD del benchmark. Nunca lanza.</summary>
        public static void Draw(SpriteBatch spriteBatch)
        {
            if (!_running && !_finished)
                return;

            try
            {
                var font = Resources.GameFont;
                if (font == null || spriteBatch == null)
                    return;

                var scale = Math.Max(2, SwitchSettings.FpsScale);
                var text = _status;
                if (_running)
                    text += "\n" + _lastFrameMs.ToString("00.0") + " ms   NO TE MUEVAS";

                var size = font.MeasureString(text) * scale;
                var pos = new Vector2(10, 10 + 34 * scale);

                spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null);
                if (Resources.SprWhite != null)
                {
                    spriteBatch.Draw(Resources.SprWhite,
                        new Rectangle((int)pos.X - 6, (int)pos.Y - 4,
                                      (int)size.X + 12, (int)size.Y + 8),
                        Color.Black * 0.75f);
                }
                spriteBatch.DrawString(font, text, pos,
                    _running ? new Color(255, 220, 120) : new Color(140, 240, 140),
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.End();
            }
            catch
            {
                // Un HUD de medida no puede tumbar el juego.
            }
        }
    }
}
#endif
