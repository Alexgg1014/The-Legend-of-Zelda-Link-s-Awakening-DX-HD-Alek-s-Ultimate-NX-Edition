#if SWITCH
using System;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ
{
    /// <summary>
    /// Read-only graphics snapshot for crash reports. Never mutates state.
    ///
    /// Up to v2.0.4 this lived inside Game1 as a <c>#if SWITCH</c> block and could reach Game1's
    /// private _renderTarget1/_renderTarget2. Since v2.0.5 the platform layer no longer patches
    /// Core, so this reads public API only. Losing the two private render targets costs little:
    /// the diag v4 repro already ruled out use-after-dispose of render targets and effects.
    /// </summary>
    internal static class SwitchGraphicsSnapshot
    {
        public static string Capture()
        {
            var sb = new System.Text.StringBuilder();
            try
            {
                var gd = Game1.Graphics?.GraphicsDevice;
                if (gd == null)
                {
                    sb.Append("GraphicsDevice=<null>");
                }
                else
                {
                    var bound = gd.GetRenderTargets();
                    sb.Append("GraphicsDevice=").Append(gd.IsDisposed ? "DISPOSED" : "alive")
                      .Append(" viewport=").Append(gd.Viewport.Width).Append('x').Append(gd.Viewport.Height)
                      .Append(" backbuffer=").Append(gd.PresentationParameters.BackBufferWidth).Append('x')
                      .Append(gd.PresentationParameters.BackBufferHeight)
                      .Append(" boundTargets=").Append(bound == null ? 0 : bound.Length);
                }

                sb.Append("\n  main=").Append(Rt(Game1.MainRenderTarget));
                sb.Append("\n  blur=").Append(Fx(Resources.BlurEffect))
                  .Append(" roundedBlur=").Append(Fx(Resources.RoundedCornerBlurEffect))
                  .Append(" blurH=").Append(Fx(Resources.BlurEffectH))
                  .Append(" blurV=").Append(Fx(Resources.BlurEffectV));
                sb.Append("\n  windowSize=").Append(Game1.WindowWidth).Append('x').Append(Game1.WindowHeight)
                  .Append(" maxGameScale=").Append(Game1.MaxGameScale);
            }
            catch (Exception e)
            {
                sb.Append("<snapshot threw ").Append(e.GetType().Name).Append('>');
            }
            return sb.ToString();

            string Rt(RenderTarget2D rt)
            {
                try { return rt == null ? "null" : (rt.IsDisposed ? "DISPOSED " : "") + rt.Width + "x" + rt.Height + "#" + rt.GetHashCode().ToString("X"); }
                catch { return "<threw>"; }
            }

            string Fx(Effect e)
            {
                try { return e == null ? "null" : (e.IsDisposed ? "DISPOSED" : "alive") + "#" + e.GetHashCode().ToString("X"); }
                catch { return "<threw>"; }
            }
        }
    }
}
#endif
