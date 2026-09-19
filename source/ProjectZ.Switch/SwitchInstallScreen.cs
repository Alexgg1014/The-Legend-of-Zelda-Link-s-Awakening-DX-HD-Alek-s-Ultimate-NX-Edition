#if SWITCH
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.Switch.Install
{
    /// <summary>
    /// Pantalla del instalador in-game. Se dibuja mientras <see cref="AssetInstaller"/> genera
    /// Content/ y Data/ en un hilo aparte: fase actual, barra de progreso, fichero que se
    /// está creando y, si algo falla, el motivo en claro y qué hacer.
    ///
    /// No puede usar nada de Content (no existe todavía): fuente bitmap propia
    /// (<see cref="SwitchPixelFont"/>) y un píxel blanco. Estética de la pantalla idle del
    /// panel (fondo negro, marco dorado, trifuerza latiendo).
    /// </summary>
    internal static class SwitchInstallScreen
    {
        private static Texture2D _pixel;
        private static Texture2D _font;
        private static SpriteBatch _batch;

        private static readonly Color Bg = new Color(8, 8, 10);
        private static readonly Color DarkGold = new Color(122, 88, 30);
        private static readonly Color Gold = new Color(250, 208, 102);
        private static readonly Color Text = new Color(236, 232, 220);
        private static readonly Color Dim = new Color(150, 146, 136);
        private static readonly Color Red = new Color(232, 84, 72);
        private static readonly Color Green = new Color(120, 220, 120);

        public static void Init(GraphicsDevice device)
        {
            if (_batch != null) return;
            _batch = new SpriteBatch(device);
            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _font = BuildFontAtlas(device);
        }

        /// <summary>Atlas de 224 glifos en una fila: (c-32)*8 es la columna del carácter c.</summary>
        private static Texture2D BuildFontAtlas(GraphicsDevice device)
        {
            int gw = SwitchPixelFont.GlyphWidth, gh = SwitchPixelFont.GlyphHeight;
            int count = SwitchPixelFont.LastChar - SwitchPixelFont.FirstChar + 1;
            var tex = new Texture2D(device, gw * count, gh);
            var px = new Color[gw * count * gh];
            for (int g = 0; g < count; g++)
            {
                for (int y = 0; y < gh; y++)
                {
                    byte row = SwitchPixelFont.Data[g * gh + y];
                    for (int x = 0; x < gw; x++)
                    {
                        if ((row & (0x80 >> x)) != 0)
                            px[y * gw * count + g * gw + x] = Color.White;
                    }
                }
            }
            tex.SetData(px);
            return tex;
        }

        // ------------------------------------------------------------------ texto

        private static int TextWidth(string s, int scale) => s.Length * SwitchPixelFont.GlyphWidth * scale;

        private static void DrawText(string s, int x, int y, int scale, Color color)
        {
            int gw = SwitchPixelFont.GlyphWidth, gh = SwitchPixelFont.GlyphHeight;
            for (int i = 0; i < s.Length; i++)
            {
                int c = s[i];
                if (c < SwitchPixelFont.FirstChar || c > SwitchPixelFont.LastChar) c = '?';
                var src = new Rectangle((c - SwitchPixelFont.FirstChar) * gw, 0, gw, gh);
                var dst = new Rectangle(x + i * gw * scale, y, gw * scale, gh * scale);
                _batch.Draw(_font, dst, src, color);
            }
        }

        private static void DrawCentered(string s, int cx, int y, int scale, Color color)
            => DrawText(s, cx - TextWidth(s, scale) / 2, y, scale, color);

        /// <summary>Parte una cadena en líneas de como mucho maxChars, por espacios; respeta '\n'.</summary>
        private static List<string> Wrap(string s, int maxChars)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(s)) return lines;
            foreach (var para in s.Split('\n'))
            {
                var words = para.Split(' ');
                string cur = "";
                foreach (var w in words)
                {
                    if (cur.Length == 0) cur = w;
                    else if (cur.Length + 1 + w.Length <= maxChars) cur += " " + w;
                    else { lines.Add(cur); cur = w; }
                }
                lines.Add(cur);
            }
            return lines;
        }

        private static string Shorten(string s, int maxChars)
        {
            if (s == null) return "";
            if (s.Length <= maxChars) return s;
            return "..." + s.Substring(s.Length - (maxChars - 3));
        }

        // ------------------------------------------------------------------ dibujo

        public static void Draw(GraphicsDevice device, AssetInstaller inst)
        {
            var pp = device.PresentationParameters;
            int W = pp.BackBufferWidth, H = pp.BackBufferHeight;
            // Escala de fuente relativa a 720p: 1080p docked usa el mismo diseño más grande.
            int s = Math.Max(1, H / 360);   // 2 a 720p, 3 a 1080p
            int big = s + 1;
            int cx = W / 2;

            device.Clear(Bg);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            // Marco dorado como el idle del panel.
            int inset = 12 * s / 2 + 6, th = s;
            _batch.Draw(_pixel, new Rectangle(inset, inset, W - 2 * inset, th), DarkGold);
            _batch.Draw(_pixel, new Rectangle(inset, H - inset - th, W - 2 * inset, th), DarkGold);
            _batch.Draw(_pixel, new Rectangle(inset, inset, th, H - 2 * inset), DarkGold);
            _batch.Draw(_pixel, new Rectangle(W - inset - th, inset, th, H - 2 * inset), DarkGold);

            // Trifuerza latiendo, arriba.
            var t = Environment.TickCount64 / 1000.0;
            var pulse = (float)(Math.Sin(t * 1.5) * 0.5 + 0.5);
            var gold = (byte)(150f + 100f * pulse);
            var tri = new Color(gold, (byte)(gold * 0.83f), (byte)(gold * 0.41f));
            int size = H / 18;
            int triTop = H / 9;
            FillUpTriangle(cx, triTop, size, tri);
            FillUpTriangle(cx - size * 0.58f, triTop + size, size, tri);
            FillUpTriangle(cx + size * 0.58f, triTop + size, size, tri);

            int y = triTop + size * 2 + H / 24;
            DrawCentered("ALEK'S ULTIMATE · ZELDA LINK'S AWAKENING DX HD", cx, y, big, Gold);
            y += 16 * big + 4 * s;

            var stage = inst.CurrentStage;
            if (stage == AssetInstaller.Stage.Failed)
            {
                DrawFailed(inst, cx, y, s, big, W);
            }
            else
            {
                DrawCentered("Instalación · Installation", cx, y, s, Dim);
                y += 16 * s + 12 * s;

                string es, en;
                PhaseText(stage, out es, out en);
                DrawCentered(es, cx, y, big, Text);
                y += 16 * big + 2 * s;
                DrawCentered(en, cx, y, s, Dim);
                y += 16 * s + 10 * s;

                // Barra de progreso.
                int done = inst.Done, total = inst.Total;
                int barW = W * 6 / 10, barH = 14 * s, barX = cx - barW / 2;
                bool determinate = total > 0 && stage != AssetInstaller.Stage.Done;
                float frac = stage == AssetInstaller.Stage.Done ? 1f
                    : (total > 0 ? Math.Min(1f, done / (float)total) : 0f);
                _batch.Draw(_pixel, new Rectangle(barX - th, y - th, barW + 2 * th, barH + 2 * th), DarkGold);
                _batch.Draw(_pixel, new Rectangle(barX, y, barW, barH), Bg);
                if (determinate || stage == AssetInstaller.Stage.Done)
                {
                    _batch.Draw(_pixel, new Rectangle(barX, y, (int)(barW * frac), barH), Gold);
                }
                else
                {
                    // Indeterminada: un bloque que va y viene mientras carga el paquete.
                    int block = barW / 5;
                    float ph = (float)((t * 0.8) % 2.0);
                    if (ph > 1f) ph = 2f - ph;
                    _batch.Draw(_pixel, new Rectangle(barX + (int)((barW - block) * ph), y, block, barH), Gold);
                }
                y += barH + 6 * s;

                string counter = total > 0 && determinate
                    ? ((int)(frac * 100)).ToString() + " %   " + done + " / " + total
                    : (stage == AssetInstaller.Stage.Done ? "100 %" : "");
                DrawCentered(counter, cx, y, s, Text);
                y += 16 * s + 4 * s;

                // Fichero en curso, recortado por la izquierda.
                var cur = inst.CurrentItem;
                if (!string.IsNullOrEmpty(cur) && stage != AssetInstaller.Stage.Done)
                    DrawCentered(Shorten(cur, W / (8 * s) - 10), cx, y, s, Dim);
                y += 16 * s + 14 * s;

                if (stage == AssetInstaller.Stage.Done)
                {
                    DrawCentered("Listo. Arrancando el juego...", cx, y, big, Green);
                    y += 16 * big + 2 * s;
                    DrawCentered("Done. Starting the game...", cx, y, s, Dim);
                }
                else
                {
                    DrawCentered("No apagues la consola · Do not power off", cx, y, s, Dim);
                }
            }

            // Pie.
            // Cabe a 720p (s=2): 73 caracteres x 16 px = 1168 px. La primera version se salia.
            DrawCentered("Parches de bighead (ladxhd_updated v" + AssetInstaller.ExpectedAssetVersion +
                         ") aplicados sobre tu copia v1.0.0",
                cx, H - inset - 16 * s - 8 * s, s, Dim);

            _batch.End();
        }

        private static void DrawFailed(AssetInstaller inst, int cx, int y, int s, int big, int W)
        {
            DrawCentered("No se ha podido instalar · Installation failed", cx, y, big, Red);
            y += 16 * big + 10 * s;

            int maxChars = Math.Max(20, (W * 8 / 10) / (8 * s));
            foreach (var line in Wrap(inst.Error ?? "", maxChars))
            {
                DrawCentered(line, cx, y, big, Text);
                y += 16 * big + 2 * s;
            }
            y += 6 * s;
            foreach (var line in Wrap(inst.ErrorDetail ?? "", maxChars))
            {
                DrawCentered(line, cx, y, s, Text);
                y += 16 * s + 2 * s;
            }
            y += 12 * s;

            // Recordatorio de cómo se instala, siempre.
            foreach (var line in new[]
                     {
                         "Copia tu Link's Awakening DX HD v1.0.0 (carpetas Content y Data) a:",
                         "sdmc:/switch/zelda-ladxhd/original/",
                         "Copy your Link's Awakening DX HD v1.0.0 (Content and Data folders) there.",
                     })
            {
                DrawCentered(line, cx, y, s, Dim);
                y += 16 * s + 2 * s;
            }
            y += 10 * s;
            DrawCentered("Pulsa + para salir · Press + to exit", cx, y, s, Gold);
        }

        private static void PhaseText(AssetInstaller.Stage stage, out string es, out string en)
        {
            switch (stage)
            {
                case AssetInstaller.Stage.Locating: es = "Buscando la carpeta original/"; en = "Looking for the original/ folder"; break;
                case AssetInstaller.Stage.Loading: es = "Cargando los parches"; en = "Loading patches"; break;
                case AssetInstaller.Stage.Verifying: es = "Comprobando que original/ es v1.0.0"; en = "Checking original/ is v1.0.0"; break;
                case AssetInstaller.Stage.Patching: es = "Creando Content y Data"; en = "Creating Content and Data"; break;
                case AssetInstaller.Stage.Finishing: es = "Terminando"; en = "Finishing up"; break;
                case AssetInstaller.Stage.Done: es = "Instalación completa"; en = "Installation complete"; break;
                default: es = "Preparando"; en = "Preparing"; break;
            }
        }

        private static void FillUpTriangle(float cx, float top, int size, Color color)
        {
            for (var row = 0; row <= size; row++)
            {
                var lw = row * 1.1546f;
                var xa = (int)Math.Ceiling(cx - lw * 0.5f);
                var xb = (int)Math.Floor(cx + lw * 0.5f);
                if (xb < xa) xb = xa;
                var yy = (int)Math.Floor(top + row + 0.5f);
                _batch.Draw(_pixel, new Rectangle(xa, yy, xb - xa + 1, 1), color);
            }
        }
    }
}
#endif
