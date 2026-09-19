"""Genera SwitchPixelFont.cs: fuente bitmap 8x16 (Latin-1 32..255) a partir de Consolas.
El instalador in-game no tiene Content (lo esta generando), asi que necesita una fuente propia."""
import os
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "upstream204",
                   "ladxhd_game_source_code", "ProjectZ.Switch", "SwitchPixelFont.cs")
W, H = 8, 16
font = ImageFont.truetype(r"C:\Windows\Fonts\consolab.ttf", 15)
rows = []
for code in range(32, 256):
    ch = chr(code)
    img = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(img)
    # Consolas: ancho ~0.55em -> 8px a 15px; la linea base cae bien con y=-1.
    d.text((0, -1), ch, font=font, fill=255)
    px = img.load()
    glyph = []
    for y in range(H):
        b = 0
        for x in range(W):
            if px[x, y] >= 110:
                b |= 0x80 >> x
        glyph.append(b)
    rows.append(glyph)

lines = []
for i, g in enumerate(rows):
    code = 32 + i
    label = chr(code) if (32 <= code < 127 or code >= 161) and code != 39 else ("quote" if code == 39 else "ctrl")
    lines.append("            " + ", ".join("0x%02X" % b for b in g) + ",  // %d '%s'" % (code, label))

cs = """// Generado por _switchbuild/tools/make_pixel_font.py (Consolas Bold 15px -> 8x16, Latin-1).
// La pantalla del instalador no puede usar las fuentes del juego: Content/ es justo lo que
// esta generando. 16 bytes por glifo, un bit por pixel, bit 7 = columna 0.
namespace ProjectZ.Switch.Install
{
    internal static class SwitchPixelFont
    {
        public const int GlyphWidth = %d;
        public const int GlyphHeight = %d;
        public const int FirstChar = 32;
        public const int LastChar = 255;

        public static readonly byte[] Data =
        {
%s
        };
    }
}
""" % (W, H, "\n".join(lines))
open(OUT, "w", encoding="utf-8").write(cs)
print("ok", OUT, len(rows), "glifos")

# vista previa ascii de unos cuantos
for ch in "Aág%":
    g = rows[ord(ch) - 32]
    for b in g:
        print("".join("#" if b & (0x80 >> x) else "." for x in range(W)))
    print()
