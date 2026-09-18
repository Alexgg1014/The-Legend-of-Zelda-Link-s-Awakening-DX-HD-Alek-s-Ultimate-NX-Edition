#if SWITCH
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// Compositor del modo Flip Grip.
    ///
    /// Modelo copiado del donante (Z3NXB aleks_compositor.c): todo se compone en un lienzo
    /// LÓGICO vertical de 720x1280 y se rota UNA sola vez al presentar. Nada aguas arriba
    /// sabe que hay una rotación — ni el juego, ni el panel companion, ni el HUD.
    ///
    /// En vez de rotar cada pieza a mano, se construye la transformación lienzo -> pantalla
    /// una vez y se pasa a SpriteBatch.Begin. Así todo lo que se dibuje dentro va en
    /// coordenadas del lienzo, que es como está expresado el layout de AleksLayout.
    ///
    /// La matriz sale de invertir la des-rotación del toque del donante:
    ///
    ///     lx = W/2 - (phys_y - outH/2) / fx        (donante, pantalla -> lienzo)
    ///     ly = H/2 + (phys_x - outW/2) / fy
    ///
    /// despejando phys:
    ///
    ///     screen_x = outW/2 + (cy - H/2) * fy
    ///     screen_y = outH/2 - (cx - W/2) * fx
    ///
    /// Usar la inversa exacta de la fórmula del toque, y no una rotación inventada, es lo
    /// que garantiza que el toque caiga donde se ve (fase 3).
    /// </summary>
    public static class SwitchFlipCompositor
    {
        /// <summary>
        /// Transformación lienzo (720x1280) -> pantalla, incluida la rotación de 270.
        /// </summary>
        public static Matrix BuildTransform(in AleksLayout.Layout layout, int outW, int outH)
        {
            float fx = layout.FlipScaleX > 0f ? layout.FlipScaleX : 1f;
            float fy = layout.FlipScaleY > 0f ? layout.FlipScaleY : 1f;

            float tx = outW / 2f - fy * layout.LogicalH / 2f;
            float ty = outH / 2f + fx * layout.LogicalW / 2f;

            // screen_x = cx*M11 + cy*M21 + M41
            // screen_y = cx*M12 + cy*M22 + M42
            var m = Matrix.Identity;
            m.M11 = 0f;   m.M12 = -fx;
            m.M21 = fy;   m.M22 = 0f;
            m.M41 = tx;   m.M42 = ty;
            return m;
        }

        /// <summary>
        /// Tamaño en PÍXELES DE PANTALLA que ocupa la ranura del juego. Es el tamaño al que
        /// conviene renderizar el juego: así el lienzo no se escala en el camino.
        ///
        /// A 720p da 720x720 (mapeo 1:1). En dock da 1080x1080, que es lo que evita el 1.5x
        /// no entero contra el que avisa el donante de TMC: con filtrado nearest, escalar por
        /// 1.5 duplica unas columnas y triplica otras, y eso se lee como bandeo parpadeante.
        /// </summary>
        public static Point GameRenderSize(in AleksLayout.Layout layout)
        {
            float fx = layout.FlipScaleX > 0f ? layout.FlipScaleX : 1f;
            float fy = layout.FlipScaleY > 0f ? layout.FlipScaleY : 1f;

            // SwitchSettings.FlipRender: 1080 = tamano de backbuffer (layout.Game * f, 1080x810);
            // 720 = tamano de LIENZO (720x540, la pantalla portatil real). Medido el 17-sep-2026:
            // 30 fps clavados a 1080x810 frente a ~58 a 720x540. El compositor estira el RT a
            // la ranura en ambos casos.
            int w, h;
            if (ProjectZ.InGame.Things.SwitchSettings.Render == 720)
            {
                w = layout.Game.W;
                h = layout.Game.H;
            }
            else
            {
                w = (int)Math.Round(layout.Game.W * fx);
                h = (int)Math.Round(layout.Game.H * fy);
            }

            if (w < 320) w = 320;
            if (h < 240) h = 240;
            w -= w & 1;
            h -= h & 1;
            return new Point(w, h);
        }

        /// <summary>
        /// Compone el frame vertical sobre el backbuffer. Se llama DESPUÉS de base.Draw(),
        /// que ya dejó el frame del juego en Game1.FinalRenderTarget. Nunca lanza.
        /// </summary>
        public static void Compose(SpriteBatch spriteBatch, in AleksLayout.Layout layout,
                                   int outW, int outH)
        {
            try
            {
                var frame = Game1.FinalRenderTarget;
                var device = Game1.Graphics?.GraphicsDevice;
                if (frame == null || frame.IsDisposed || device == null || spriteBatch == null)
                    return;

                // Game1 acaba de presentar sin rotar y con el viewport reducido a la ranura.
                // Hay que recuperar el panel entero y borrar antes de recomponer.
                device.Viewport = new Viewport(0, 0, outW, outH);
                device.Clear(Color.Black);

                var transform = BuildTransform(layout, outW, outH);

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, transform);

                var g = layout.Game;
                spriteBatch.Draw(frame, new Rectangle(g.X, g.Y, g.W, g.H), Color.White);

                spriteBatch.End();
            }
            catch (Exception e)
            {
                SwitchDiag.LogOnce("secondscreen.log", "flipfail",
                    "SwitchFlipCompositor.Compose fallo: " + e.GetType().Name + ": " + e.Message
                    + " - volviendo a apaisado");
                SwitchSettings.Flip = 0;
            }
        }
    }
}
#endif
