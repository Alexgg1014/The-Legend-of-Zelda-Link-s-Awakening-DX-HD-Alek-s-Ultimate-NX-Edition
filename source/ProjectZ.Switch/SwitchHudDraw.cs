#if SWITCH
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.SaveLoad;

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// Dibujado de HUD para el panel con la MISMA receta que ItemDrawHelper de Core, pero
    /// solo con API pública: ItemDrawHelper es `internal` y Core no se toca.
    ///
    /// Sprites: atlas `ui` ("ui heart", "ui ruby", "ui key", "ui letter") y los sprites de
    /// cada GameItem (Resources.SprItem vía ItemManager). Nada nuevo.
    /// </summary>
    internal static class SwitchHudDraw
    {
        /// <summary>Colores de túnica de Core (ItemDrawHelper.CloakColors): verde, azul, rojo.</summary>
        public static readonly Color TunicGreen = new Color(16, 173, 66);
        public static readonly Color TunicBlue  = new Color(24, 132, 255);
        public static readonly Color TunicRed   = new Color(255, 8, 41);

        private static DictAtlasEntry _heart, _ruby, _key, _letter;

        private static void EnsureSprites()
        {
            if (_letter != null) return;
            _heart  = Resources.GetSprite("ui heart");
            _ruby   = Resources.GetSprite("ui ruby");
            _key    = Resources.GetSprite("ui key");
            _letter = Resources.GetSprite("ui letter");
        }

        /// <summary>Alto en px de las filas de corazones dibujadas.</summary>
        public static int DrawHearts(SpriteBatch sb, int x, int y, int scale, int perRow, int maxHearts, int health)
        {
            EnsureSprites();
            if (_heart == null || maxHearts <= 0) return 0;

            var rec = _heart.ScaledRectangle;
            var step = rec.Width + _heart.TextureScale;    // 8 px lógicos: 7 + 1 de separación
            for (var i = 0; i < maxHearts; i++)
            {
                var value = health - i * 4;
                var type = value <= 0 ? 4 : (value <= 3 ? 4 - value : 0);
                sb.Draw(_heart.Texture,
                    new Rectangle(x + (i % perRow) * 8 * scale, y + (i / perRow) * 8 * scale,
                        7 * scale, 7 * scale),
                    new Rectangle(rec.X + type * step, rec.Y, rec.Width, rec.Height), Color.White);
            }
            return ((maxHearts + perRow - 1) / perRow) * 8 * scale;
        }

        /// <summary>Dígitos con la fuente `ui letter` (6x6 lógicos, paso 7), tintados.</summary>
        public static void DrawNumber(SpriteBatch sb, int x, int y, int number, int length, int scale, Color color)
        {
            EnsureSprites();
            if (_letter == null) return;
            var rec = _letter.ScaledRectangle;
            var step = rec.Width + _letter.TextureScale;
            for (var i = 0; i < length; i++)
            {
                var digit = number / Pow10(length - i - 1) % 10 + 1;
                sb.Draw(_letter.Texture,
                    new Rectangle(x + 7 * i * scale, y, 6 * scale, 6 * scale),
                    new Rectangle(rec.X + digit * step, rec.Y, rec.Width, rec.Height), color);
            }
        }

        private static int Pow10(int n) { var r = 1; while (n-- > 0) r *= 10; return r; }

        /// <summary>Contador de rupias como el HUD: 3 dígitos + icono. Ancho = 28 px lógicos.</summary>
        public static void DrawRupees(SpriteBatch sb, int x, int y, int scale, Color digits)
        {
            EnsureSprites();
            var count = Game1.GameManager?.GetItem("ruby")?.Count ?? 0;
            DrawNumber(sb, x, y, count, 3, scale, digits);
            if (_ruby != null)
                sb.Draw(_ruby.Texture, new Rectangle(x + 21 * scale, y - scale, 7 * scale + 1, 7 * scale),
                    _ruby.ScaledRectangle, Color.White);
        }

        /// <summary>Llaves pequeñas como el HUD (solo si Link lleva alguna). Ancho = 21 px lógicos.</summary>
        public static void DrawSmallKeys(SpriteBatch sb, int x, int y, int scale, Color digits)
        {
            EnsureSprites();
            var keys = Game1.GameManager?.GetItem("smallkey");
            if (keys == null) return;
            DrawNumber(sb, x, y, keys.Count, 2, scale, digits);
            if (_key != null)
                sb.Draw(_key.Texture, new Rectangle(x + 14 * scale, y - 2 * scale, 7 * scale, 9 * scale),
                    _key.ScaledRectangle, Color.White);
        }

        /// <summary>Tamaño lógico (sin escalar) del sprite de un objeto, o Point.Zero.</summary>
        public static Point ItemSize(GameItem item)
        {
            var sprite = ResolveSprite(item);
            if (sprite == null) return Point.Zero;
            return new Point(sprite.SourceRectangle.Width, sprite.SourceRectangle.Height);
        }

        private static DictAtlasEntry ResolveSprite(GameItem item)
        {
            if (item == null) return null;
            var baseItem = item.Sprite != null ? item : Game1.GameManager?.ItemManager[item.Name];
            return baseItem?.Sprite;
        }

        /// <summary>
        /// Dibuja el sprite de inventario de un objeto en `position` a `scale` (px por píxel
        /// lógico), como ItemDrawHelper.DrawItem sin los parpadeos especiales.
        /// </summary>
        public static void DrawItem(SpriteBatch sb, GameItem item, Vector2 position, Color color, int scale)
        {
            var sprite = ResolveSprite(item);
            if (sprite == null) return;
            var src = sprite.ScaledRectangle;
            sb.Draw(sprite.Texture,
                new Rectangle((int)position.X, (int)position.Y,
                    sprite.SourceRectangle.Width * scale, sprite.SourceRectangle.Height * scale),
                src, color);
        }

        /// <summary>Dibuja un objeto centrado en una celda.</summary>
        public static void DrawItemCentered(SpriteBatch sb, GameItem item, Rectangle cell, Color color, int scale)
        {
            var size = ItemSize(item);
            if (size == Point.Zero) return;
            DrawItem(sb, item,
                new Vector2(cell.X + (cell.Width - size.X * scale) / 2, cell.Y + (cell.Height - size.Y * scale) / 2),
                color, scale);
        }

        /// <summary>
        /// Objeto equipado con su contador/nivel, como ItemDrawHelper.DrawItemWithInfo: sprite
        /// centrado y, si el objeto tiene nivel o cuenta, el número a su derecha.
        /// </summary>
        public static void DrawItemWithInfo(SpriteBatch sb, GameItemCollected collected, Rectangle cell, int scale, Color color)
        {
            if (collected == null) return;
            var gm = Game1.GameManager;
            if (gm == null) return;
            var item = gm.ItemManager[collected.Name];
            if (item == null) return;
            var baseItem = item.Sprite != null ? item : gm.ItemManager[item.Name];
            var size = ItemSize(baseItem);
            if (size == Point.Zero) return;

            var extra = 0;      // ancho extra en px lógicos para el número
            var text = -1; var len = 0;
            if (item.Level > 0) { extra = 1 + 6 + 1; text = item.Level; len = 1; }
            else if (item.MaxCount != 1) { len = item.DrawLength > 0 ? item.DrawLength : 2; extra = 1 + 6 * len + 1; text = collected.Count; }

            var width = size.X + extra;
            var ix = cell.X + (cell.Width - width * scale) / 2;
            var iy = cell.Y + (cell.Height - size.Y * scale) / 2;
            DrawItem(sb, baseItem, new Vector2(ix, iy), color, scale);
            if (text >= 0)
                DrawNumber(sb, ix + (size.X + 1) * scale, iy + (size.Y - 6) * scale, text, len, scale, Color.Black * (color.A / 255f));
        }
    }
}
#endif
