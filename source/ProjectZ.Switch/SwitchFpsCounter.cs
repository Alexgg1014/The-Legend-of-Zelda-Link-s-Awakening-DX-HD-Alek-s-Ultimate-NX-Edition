#if SWITCH
using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// Contador de FPS del port Switch.
    ///
    /// Core ya tiene uno (SimpleFps + Game1.ShowDebugText), pero es inalcanzable aquí: está
    /// detrás de "if (EditorMode && ... InputHandler.KeyPressed(...))", o sea editor + teclado,
    /// y además vuelca media pantalla de diagnóstico (tiempos, historial, posición del jugador)
    /// que depende de estado que puede no existir todavía.
    ///
    /// Este mide solo lo que interesa y dibuja una línea. Usa Stopwatch, NUNCA DateTime:
    /// en Switch DateTime.UtcNow está congelado en 00:00:00.
    ///
    /// Muestra FPS de presentación (frames dibujados) y el tiempo medio por frame, que es lo
    /// que de verdad dice si vas sobrado o justo: a 60 Hz el presupuesto son 16,7 ms.
    /// </summary>
    public static class SwitchFpsCounter
    {
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        private static int _frames;
        private static double _windowStartMs;
        private static double _fps;
        private static double _msPerFrame;
        private static string _text = "";

        private static double _lastFrameStampMs;

        /// <summary>Duracion del ultimo frame, en ms. La usa el benchmark.</summary>
        public static double LastFrameMs { get; private set; }

        /// <summary>Llamar una vez por frame dibujado, antes de Draw().</summary>
        public static void CountFrame()
        {
            _frames++;

            var nowMs = Clock.Elapsed.TotalMilliseconds;

            // Delta de este frame, para el benchmark.
            if (_lastFrameStampMs > 0)
                LastFrameMs = nowMs - _lastFrameStampMs;
            _lastFrameStampMs = nowMs;
            var elapsed = nowMs - _windowStartMs;

            // Ventana de medio segundo: reacciona rápido sin bailar en pantalla.
            if (elapsed < 500.0)
                return;

            _fps = _frames * 1000.0 / elapsed;
            _msPerFrame = elapsed / _frames;
            _text = _fps.ToString("00.0") + " FPS   " + _msPerFrame.ToString("00.0") + " ms";

            _frames = 0;
            _windowStartMs = nowMs;
        }

        /// <summary>
        /// Dibuja el contador. Llamar sin render target activo y fuera de cualquier
        /// SpriteBatch. Nunca lanza: un contador no puede tirar el frame.
        /// </summary>
        public static void Draw(SpriteBatch spriteBatch, Matrix? transform = null)
        {
            if (SwitchSettings.ShowFps == 0 || _text.Length == 0)
                return;

            try
            {
                var font = Resources.GameFont;
                if (font == null || spriteBatch == null)
                    return;

                // Verde si va sobrado, ámbar si flojea, rojo si va mal.
                var color = _fps >= 55.0 ? new Color(120, 230, 120)
                          : _fps >= 40.0 ? new Color(240, 200, 100)
                          : new Color(240, 120, 120);

                // Escalado: la fuente del juego es diminuta a 720p/1080p.
                var scale = SwitchSettings.FpsScale < 1 ? 1 : SwitchSettings.FpsScale;
                var size = font.MeasureString(_text) * scale;
                var pos = new Vector2(10, 10);

                // En FLIP la transformacion rota el contador con todo lo demas; si no,
                // es null y se dibuja igual que siempre.
                spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, null, transform);

                // Fondo para que se lea sobre cualquier escena.
                if (Resources.SprWhite != null)
                {
                    spriteBatch.Draw(Resources.SprWhite,
                        new Rectangle((int)pos.X - 6, (int)pos.Y - 4,
                                      (int)size.X + 12, (int)size.Y + 8),
                        Color.Black * 0.7f);
                }

                spriteBatch.DrawString(font, _text, pos, color, 0f, Vector2.Zero,
                    scale, SpriteEffects.None, 0f);
                spriteBatch.End();
            }
            catch (Exception e)
            {
                SwitchDiag.LogOnce("secondscreen.log", "fpsdrawfail",
                    "SwitchFpsCounter.Draw fallo: " + e.GetType().Name + ": " + e.Message + " - apagando");
                SwitchSettings.ShowFps = 0;
            }
        }
    }
}
#endif
