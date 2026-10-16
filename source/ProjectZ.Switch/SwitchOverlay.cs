#if SWITCH
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ
{
    /// <summary>
    /// El panel companion como OVERLAY en apaisado (v1.2), manejado con el mando.
    ///
    /// Hasta ahora el panel solo existía en Flip Grip y solo por toque, así que en dock —que es
    /// como juega mucha gente— todo el trabajo del panel no se veía nunca. Esto lo abre con
    /// **Minus** sobre el juego, centrado y con el fondo atenuado, y se navega con cruceta,
    /// A/B y L/R, igual que en el port de Minish Cap del mismo autor.
    ///
    /// Dos decisiones que conviene no deshacer:
    ///
    ///  1. **Mientras está abierto, el juego se congela** (SwitchGame no llama a base.Update).
    ///     Es lo que evita que Link ande mientras mueves la cruceta por el panel, y de paso
    ///     hace que el juego NO vea el Minus que abre y cierra el overlay: si lo viera, abriría
    ///     además su propio mapa, que es lo que Minus hace en este juego.
    ///  2. Por eso mismo, al cerrar NO se reanuda hasta que Minus se suelta. Si se reanudara
    ///     con el botón aún pulsado, el juego vería un flanco de pulsación recién llegado
    ///     (su estado anterior es de antes de abrir) y abriría el mapa al salir.
    ///
    /// El overlay reutiliza SecondScreen tal cual: se le pasa un Layout sintético cuyo
    /// Companion es el rectángulo centrado de la pantalla, sin rotación ni transformación. Por
    /// eso el toque también funciona aquí sin tocar nada: HitTest ya trabaja en coordenadas
    /// del panel.
    /// </summary>
    internal static class SwitchOverlay
    {
        /// <summary>Proporción del alto de la salida que ocupa el panel (el resto es margen).</summary>
        private const float HeightFraction = 0.92f;

        /// <summary>Relación de aspecto del panel, la de sus cotas de referencia (720x540).</summary>
        private const float PanelAspect = 720f / 540f;

        private static bool _open;
        private static int _focus;
        private static Texture2D _dim;

        public static bool IsOpen => _open;
        public static int Focus => _focus;

        /// <summary>
        /// true mientras haya que seguir congelando el juego aunque el overlay ya esté cerrado:
        /// se está esperando a que se suelte Minus (ver la nota 2 de arriba).
        /// </summary>
        public static bool SwallowingClose;

        public static void Open()
        {
            if (_open)
                return;
            _open = true;
            _focus = 0;
            ClampFocus();
            SwitchDiag.Log("secondscreen.log", "overlay: abierto (pestana " + SecondScreenState.Current + ")");
        }

        public static void Close()
        {
            if (!_open)
                return;
            _open = false;
            SwallowingClose = true;
            SwitchDiag.Log("secondscreen.log", "overlay: cerrado");
        }

        /// <summary>Se cierra sin esperar a soltar el botón (cambio de modo, fin de partida…).</summary>
        public static void ForceClose()
        {
            _open = false;
            SwallowingClose = false;
        }

        private static void ClampFocus()
        {
            var n = SecondScreen.FocusCount();
            if (n <= 0) { _focus = 0; return; }
            if (_focus >= n) _focus = n - 1;
            if (_focus < 0) _focus = 0;
        }

        // ------------------------------------------------------------------ navegación

        /// <summary>
        /// Un paso de navegación. SwitchGame detecta los flancos y los pasa aquí; devuelve el
        /// código de HitTest a ejecutar (el MISMO que devolvería un toque), o -1.
        /// </summary>
        public static int Navigate(bool up, bool down, bool left, bool right,
                                   bool accept, bool cancel, bool tabPrev, bool tabNext)
        {
            if (!_open)
                return -1;

            if (tabPrev || tabNext)
            {
                SecondScreenState.Cycle(tabNext ? 1 : -1);
                _focus = 0;
                ClampFocus();
                return -1;
            }

            var count = SecondScreen.FocusCount();
            if (count > 0)
            {
                var cols = Math.Max(1, SecondScreen.FocusColumns());
                if (up)    _focus -= cols;
                if (down)  _focus += cols;
                if (left)  _focus -= 1;
                if (right) _focus += 1;
                // Envolver en los dos extremos: en una lista de 10 filas, subir desde la
                // primera lleva a la última, que es lo que espera cualquiera.
                if (_focus < 0) _focus += ((-_focus + count - 1) / count) * count;
                if (count > 0) _focus %= count;
            }
            else
            {
                _focus = 0;
            }

            if (accept)
                return SecondScreen.FocusHit(_focus);

            if (cancel)
            {
                // En EQUIPO, B no cierra: equipa al slot de B, como en el TMC. Es justo lo que
                // hace util la pestana sin pasar por el menu de pausa. Para salir esta Minus.
                if (SecondScreenState.Current == SecondScreenState.Tab.Equipment && count > 0)
                    return SecondScreen.FocusHit(_focus, true);
                Close();
            }
            return -1;
        }

        // ------------------------------------------------------------------ geometría

        /// <summary>
        /// Layout sintético para SecondScreen: Companion = panel centrado en la salida, sin
        /// rotación. El juego no se recoloca (Game queda vacío): el overlay va ENCIMA.
        /// </summary>
        public static AleksLayout.Layout BuildLayout(int outW, int outH)
        {
            var h = (int)(outH * HeightFraction);
            var w = (int)(h * PanelAspect);
            if (w > outW)
            {
                w = (int)(outW * HeightFraction);
                h = (int)(w / PanelAspect);
            }
            return new AleksLayout.Layout
            {
                Mode = AleksLayout.Mode.RightBar,
                Companion = new AleksLayout.Rect
                {
                    X = (outW - w) / 2,
                    Y = (outH - h) / 2,
                    W = w,
                    H = h,
                },
                LogicalW = outW,
                LogicalH = outH,
                RotationDegrees = 0,
            };
        }

        // ------------------------------------------------------------------ dibujo

        /// <summary>
        /// Atenúa el juego por detrás. Se dibuja antes que el panel y en su propia pasada,
        /// porque SecondScreen abre y cierra las suyas.
        /// </summary>
        public static void DrawBackdrop(SpriteBatch spriteBatch, int outW, int outH)
        {
            if (!_open)
                return;
            if (_dim == null)
            {
                _dim = new Texture2D(Game1.Graphics.GraphicsDevice, 1, 1);
                _dim.SetData(new[] { Color.White });
            }
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            spriteBatch.Draw(_dim, new Rectangle(0, 0, outW, outH), Color.Black * 0.62f);
            spriteBatch.End();
        }

        /// <summary>Línea de ayuda bajo el panel con los botones que valen aquí.</summary>
        public static void DrawHint(SpriteBatch spriteBatch, AleksLayout.Layout layout)
        {
            if (!_open)
                return;
            var font = Resources.GameFont;
            var c = layout.Companion;
            if (font == null || c.IsEmpty)
                return;

            var text = SwitchStrings.Get("overlay_hint");
            var scale = Math.Max(1f, Math.Min(c.W / 720f, c.H / 540f));
            var size = font.MeasureString(text) * scale;
            var y = c.Y + c.H + (int)(6 * scale);
            // Si no cabe debajo, va dentro del margen inferior de la pantalla.
            if (y + size.Y > layout.LogicalH)
                y = layout.LogicalH - (int)size.Y - (int)(4 * scale);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            spriteBatch.DrawString(font, text,
                new Vector2(c.X + (c.W - size.X) / 2f, y),
                new Color(214, 206, 180), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.End();
        }
    }
}
#endif
