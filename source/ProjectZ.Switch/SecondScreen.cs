#if SWITCH
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.SaveLoad;

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// LADXHD companion panel — ALEKS second screen, layout A (TMC ALEKS).
    ///
    /// Geometría fija en píxeles del lienzo (panel 720×540 en FLIP), ver PANEL_LAYOUT_A_SPEC.md:
    ///   área principal 8,8 552×468 · barra lateral 568,8 144×468 · pestañas 8,484 704×48.
    /// Si el panel no mide 720×540 todo se escala con u = min(w,h)/540.
    ///
    /// Doctrina ALEKS: cero píxeles nuevos. Corazones, rupias, llaves y dígitos salen del
    /// atlas `ui`; los objetos de `SprItem` vía ItemDrawHelper; el marcador de Link es el
    /// animator `mapPlayer` del propio mapa; los colores son los del inventario (respetan
    /// LAHDMods) y los de la túnica (CloakColors).
    ///
    /// Nunca lanza: cualquier fallo se registra una vez y apaga el FLIP.
    /// </summary>
    public static class SecondScreen
    {
        private static Texture2D _pixel;
        private static AleksLayout.Layout _layout;
        private static Animator _mapPlayer;
        private static bool _mapPlayerFailed;

        // Cotas de referencia (panel de 720×540).
        private const int RefW = 720, RefH = 540;
        private const int MainX = 8, MainY = 8, MainH = 468;
        // Barra lateral 144 (normal) o 192 (HUD grande); el area principal cede el resto.
        private static int SideW => SwitchSettings.PanelHudLarge != 0 ? 192 : 144;
        private static int SideX => RefW - MainX - SideW;
        private static int MainW => SideX - MainX - 8;
        private const int BarY = 484, BarH = 48, BarBtnW = 150, BarGap = 8, GearW = 56;

        /// <summary>
        /// El panel SOLO existe en Flip Grip. En apaisado el mapa se ve donde siempre: en el
        /// menu de pausa. Superponerlo al juego tapaba parte de la imagen y no aportaba.
        /// </summary>
        public static bool Enabled => SwitchSettings.Flip != 0;

        /// <summary>Último layout calculado — el toque lee esto.</summary>
        public static AleksLayout.Layout CurrentLayout => _layout;

        /// <summary>
        /// Compone el companion sobre el backbuffer. Llamar sin render target activo y fuera
        /// de cualquier SpriteBatch. Nunca lanza.
        /// </summary>
        public static void Draw(SpriteBatch spriteBatch, int outW, int outH)
        {
            if (!Enabled)
                return;

            var layout = AleksLayout.Compute(
                AleksLayout.Mode.RightBar, outW, outH,
                0 /* gameVisibleW desconocido en RightBar -> barra conservadora */,
                100);

            DrawInLayout(spriteBatch, layout, null);
        }

        /// <summary>Una vez por frame: animaciones del panel (marcador de Link).</summary>
        public static void Update()
        {
            try
            {
                if (_mapPlayer == null && !_mapPlayerFailed)
                {
                    _mapPlayer = AnimatorSaveLoad.LoadAnimator("mapPlayer");
                    if (_mapPlayer != null)
                        _mapPlayer.Play("idle");
                    else
                        _mapPlayerFailed = true;
                }
                _mapPlayer?.Update();
            }
            catch
            {
                _mapPlayerFailed = true;
                _mapPlayer = null;
            }
        }

        /// <summary>
        /// Dibuja el panel usando un layout ya calculado y, opcionalmente, una transformación.
        /// En FLIP el panel se dibuja en coordenadas del LIENZO vertical y la transformación
        /// se encarga de la rotación, igual que el juego.
        /// </summary>
        public static void DrawInLayout(SpriteBatch spriteBatch, in AleksLayout.Layout layout,
                                        Matrix? transform)
        {
            if (!Enabled)
                return;

            try
            {
                _layout = layout;

                var c = _layout.Companion;
                if (c.IsEmpty)
                    return;

                if (_pixel == null)
                {
                    _pixel = new Texture2D(Game1.Graphics.GraphicsDevice, 1, 1);
                    _pixel.SetData(new[] { Color.White });
                }

                var panel = new Rectangle(c.X, c.Y, c.W, c.H);

                // Fuera de la partida (titulo, seleccion de archivo): la pantalla idle de los
                // otros ports ALEKS (campo casi negro, filete oro oscuro, trifuerza pulsante).
                if (!Game1.InProgress)
                {
                    DrawIdle(spriteBatch, panel, transform);
                    return;
                }

                // Chrome de ventana del juego: SprWhite + RoundedCornerEffect tintado con el
                // color de inventario del OverlayManager (respeta LAHDMods).
                DrawWindowChrome(spriteBatch, panel, transform);

                var main = R(panel, MainX, MainY, MainW, MainH);

                // Fondos con esquinas redondeadas (área principal y botones de la barra): van
                // en una pasada Immediate con el shader, separada del contenido.
                DrawRoundedBackgrounds(spriteBatch, panel, main, transform);

                spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, null, transform);

                DrawMainArea(spriteBatch, panel, main);
                DrawSidebar(spriteBatch, panel);
                DrawTabLabels(spriteBatch, panel);

                spriteBatch.End();
            }
            catch (Exception e)
            {
                // Log una vez y apagar: nunca arriesgar el bucle de frames.
                SwitchDiag.LogOnce("secondscreen.log", "drawfail",
                    "SecondScreen.Draw fallo: " + e.GetType().Name + ": " + e.Message + " - apagando");
                SwitchSettings.Flip = 0;
            }
        }

        // ------------------------------------------------------------------ idle (trifuerza)

        /// <summary>
        /// Portado de PaintIdleScreen del TMC ALEKS (a su vez del bottom_idle del 3DS): fondo
        /// (8,8,10), filete oro oscuro (122,88,30) a 12u con 2u de grosor, y trifuerza de tres
        /// triangulos equilateros de lado min(w,h)*0.06 que pulsa con sin(t*1.5). Sin texturas:
        /// rectangulos de scanline con el pixel blanco.
        /// </summary>
        private static void DrawIdle(SpriteBatch spriteBatch, Rectangle panel, Matrix? transform)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, null, transform);

            spriteBatch.Draw(_pixel, panel, new Color(8, 8, 10));

            var unit = Math.Max(0.5f, Math.Min(panel.Width, panel.Height) / 720f);
            var inset = (int)Math.Floor(12f * unit + 0.5f);
            var th = Math.Max(1, (int)Math.Floor(2f * unit + 0.5f));
            var darkGold = new Color(122, 88, 30);
            var x0 = panel.X + inset; var y0 = panel.Y + inset;
            var x1 = panel.Right - inset; var y1 = panel.Bottom - inset;
            spriteBatch.Draw(_pixel, new Rectangle(x0, y0, x1 - x0, th), darkGold);
            spriteBatch.Draw(_pixel, new Rectangle(x0, y1 - th, x1 - x0, th), darkGold);
            spriteBatch.Draw(_pixel, new Rectangle(x0, y0, th, y1 - y0), darkGold);
            spriteBatch.Draw(_pixel, new Rectangle(x1 - th, y0, th, y1 - y0), darkGold);

            var t = Environment.TickCount64 / 1000.0;
            var pulse = (float)(Math.Sin(t * 1.5) * 0.5 + 0.5);
            var gold = (byte)(150f + 100f * pulse);
            var tri = new Color(gold, (byte)(gold * 0.83f), (byte)(gold * 0.41f));
            var size = (int)Math.Floor(Math.Min(panel.Width, panel.Height) * 0.06f + 0.5f);
            var cx = panel.X + panel.Width * 0.5f;
            var cy = panel.Y + panel.Height * 0.5f;

            FillUpTriangle(spriteBatch, cx, cy - size, size, tri);
            FillUpTriangle(spriteBatch, cx - size * 0.58f, cy, size, tri);
            FillUpTriangle(spriteBatch, cx + size * 0.58f, cy, size, tri);

            spriteBatch.End();
        }

        /// <summary>Triangulo equilatero con el vertice arriba, por filas: ensancha 2/sqrt(3) por fila.</summary>
        private static void FillUpTriangle(SpriteBatch spriteBatch, float cx, float top, int size, Color color)
        {
            for (var row = 0; row <= size; row++)
            {
                var lw = row * 1.1546f;
                var xa = (int)Math.Ceiling(cx - lw * 0.5f);
                var xb = (int)Math.Floor(cx + lw * 0.5f);
                if (xb < xa) xb = xa;
                var y = (int)Math.Floor(top + row + 0.5f);
                spriteBatch.Draw(_pixel, new Rectangle(xa, y, xb - xa + 1, 1), color);
            }
        }

        // ------------------------------------------------------------------ geometría

        /// <summary>Rectángulo en px del panel a partir de cotas de referencia (720×540).</summary>
        private static Rectangle R(Rectangle panel, int x, int y, int w, int h)
        {
            var u = Math.Min(panel.Width / (float)RefW, panel.Height / (float)RefH);
            return new Rectangle(
                panel.X + (int)(x * u), panel.Y + (int)(y * u),
                (int)(w * u), (int)(h * u));
        }

        private static float U(Rectangle panel) =>
            Math.Min(panel.Width / (float)RefW, panel.Height / (float)RefH);

        /// <summary>Botón i (0..2) de la barra de pestañas, en px del panel.</summary>
        public static Rectangle TabButtonRect(Rectangle panel, int i) =>
            R(panel, MainX + i * (BarBtnW + BarGap), BarY, BarBtnW, BarH);

        public static Rectangle GearRect(Rectangle panel) =>
            R(panel, RefW - MainX - GearW, BarY, GearW, BarH);

        /// <summary>
        /// Zona tocada, en px del panel (origen = esquina del panel). Devuelve el botón de la
        /// barra (0..2), 3 para el engranaje, o -1 si no cae en ningún control.
        /// </summary>
        public static int HitTest(int panelX, int panelY)
        {
            var c = _layout.Companion;
            if (c.IsEmpty)
                return -1;
            var panel = new Rectangle(0, 0, c.W, c.H);
            var p = new Point(panelX, panelY);
            for (var i = 0; i < SecondScreenState.BarSlots; i++)
            {
                if (TabButtonRect(panel, i).Contains(p))
                    return i;
            }
            if (GearRect(panel).Contains(p))
                return SecondScreenState.BarSettings;

            if (SecondScreenState.Current == SecondScreenState.Tab.Settings)
            {
                var main = R(panel, MainX, MainY, MainW, MainH);
                for (var i = 0; i < SettingsRowCount; i++)
                {
                    if (SettingsRowRect(main, panel, i).Contains(p))
                        return HitSettingsRow + i;
                }
            }

            var gm = Game1.GameManager;
            if (gm != null)
            {
                var hand = HandSlotCount(gm);
                var cells = HandSlotRects(panel, HandSlotsTop(panel, gm));
                for (var i = 0; i < hand; i++)
                {
                    if (cells[i].Contains(p))
                        return HitHandSlot + i;
                }
                if (SecondScreenState.Current == SecondScreenState.Tab.Equipment)
                {
                    var main = R(panel, MainX, MainY, MainW, MainH);
                    for (var i = hand; i < gm.Equipment.Length; i++)
                    {
                        if (BagCellRect(main, panel, i - hand).Contains(p))
                            return HitBagItem + i;
                    }
                }
            }
            return -1;
        }

        /// <summary>HitTest devuelve HitSettingsRow + i para la fila i de AJUSTES.</summary>
        public const int HitSettingsRow = 100;

        // ------------------------------------------------------------------ fondos

        private static void DrawWindowChrome(SpriteBatch spriteBatch, Rectangle panel, Matrix? transform)
        {
            var backColor = new Color(16, 18, 24) * 0.92f;
            var overlay = Game1.GameManager?.InGameOverlay;
            if (overlay != null)
            {
                backColor = !overlay.UserCustomAlpha && GameSettings.DisableUiBlur
                    ? new Color(overlay.InventoryBackgroundColorBot, 1.0f)
                    : overlay.InventoryBackgroundColorBot;
            }

            var effect = Resources.RoundedCornerEffect;
            if (effect == null || Resources.SprWhite == null)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp,
                    null, null, null, transform);
                spriteBatch.Draw(_pixel, panel, backColor);
                spriteBatch.End();
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, null, effect, transform);
            RoundedRect(spriteBatch, effect, panel, 3f, backColor);
            spriteBatch.End();
        }

        private static void RoundedRect(SpriteBatch spriteBatch, Effect effect, Rectangle rect, float radius, Color color)
        {
            effect.Parameters["scale"]?.SetValue(1f);
            effect.Parameters["radius"]?.SetValue(radius);
            effect.Parameters["width"]?.SetValue(rect.Width);
            effect.Parameters["height"]?.SetValue(rect.Height);
            spriteBatch.Draw(Resources.SprWhite, rect, color);
        }

        private static void DrawRoundedBackgrounds(SpriteBatch spriteBatch, Rectangle panel, Rectangle main, Matrix? transform)
        {
            var effect = Resources.RoundedCornerEffect;
            var rounded = effect != null && Resources.SprWhite != null;
            var u = U(panel);

            var green = SwitchHudDraw.TunicGreen;

            if (rounded)
                spriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, null, effect, transform);
            else
                spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, null, transform);

            void Box(Rectangle r, float radius, Color col)
            {
                if (rounded) RoundedRect(spriteBatch, effect, r, radius, col);
                else spriteBatch.Draw(_pixel, r, col);
            }

            // Área principal: misma receta que los slots del inventario, más suave.
            Box(main, 3f, Color.Black * 0.08f);

            // Botones de la barra.
            var active = SecondScreenState.CurrentBarSlot;
            for (var i = 0; i < SecondScreenState.BarSlots; i++)
            {
                var r = TabButtonRect(panel, i);
                if (i == active)
                {
                    Box(r, 6f, green);
                    var inner = new Rectangle(r.X + (int)(3 * u), r.Y + (int)(3 * u), r.Width - (int)(6 * u), r.Height - (int)(6 * u));
                    Box(inner, 5f, Color.White * 0.95f);
                }
                else
                {
                    Box(r, 6f, Color.Black * 0.12f);
                }
            }
            {
                var g = GearRect(panel);
                if (active == SecondScreenState.BarSettings)
                {
                    Box(g, 6f, green);
                    var inner = new Rectangle(g.X + (int)(3 * u), g.Y + (int)(3 * u), g.Width - (int)(6 * u), g.Height - (int)(6 * u));
                    Box(inner, 5f, Color.White * 0.95f);
                }
                else
                {
                    Box(g, 6f, Color.Black * 0.12f);
                }
            }

            spriteBatch.End();
        }

        // ------------------------------------------------------------------ área principal

        private static void DrawMainArea(SpriteBatch spriteBatch, Rectangle panel, Rectangle main)
        {
            if (SecondScreenState.Current == SecondScreenState.Tab.Quest)
            {
                DrawQuest(spriteBatch, panel, main);
                return;
            }
            if (SecondScreenState.Current == SecondScreenState.Tab.Settings)
            {
                DrawSettings(spriteBatch, panel, main);
                return;
            }
            if (SecondScreenState.Current == SecondScreenState.Tab.Equipment)
            {
                DrawEquipment(spriteBatch, panel, main);
                return;
            }

            var overlay = Game1.GameManager?.InGameOverlay;
            if (overlay == null)
                return;

            RenderTarget2D rt;
            Rectangle src;

            switch (SecondScreenState.Current)
            {
                case SecondScreenState.Tab.Dungeon:
                    rt = overlay.DungeonOverlayRef?.RenderTarget;
                    if (rt == null || rt.IsDisposed) return;
                    src = rt.Bounds;
                    break;

                default:
                    rt = overlay.MapOverlayRef?.RenderTarget;
                    if (rt == null || rt.IsDisposed) return;
                    src = rt.Bounds;
                    break;
            }

            var pad = (int)(4 * U(panel));
            var inner = AleksLayout.Fit(main.X + pad, main.Y + pad, main.Width - 2 * pad, main.Height - 2 * pad,
                src.Width, src.Height);
            var dest = new Rectangle(inner.X, inner.Y, inner.W, inner.H);
            spriteBatch.Draw(rt, dest, src, Color.White);

            if (SecondScreenState.Current == SecondScreenState.Tab.Map)
                DrawPlayerMarker(spriteBatch, dest, rt);
        }

        /// <summary>
        /// El RT del mapa NO lleva el marcador de Link: MapOverlay.Draw lo pinta aparte con
        /// el animator "mapPlayer". Hacemos lo mismo, en la misma posición y a la escala del panel.
        /// </summary>
        private static void DrawPlayerMarker(SpriteBatch spriteBatch, Rectangle dest, RenderTarget2D rt)
        {
            try
            {
                var gameManager = Game1.GameManager;
                if (gameManager == null || rt.Width <= 0)
                    return;

                var mapPosition = gameManager.PlayerMapPosition;
                if (!mapPosition.HasValue)
                    return;

                var scale = dest.Width / (float)rt.Width;
                var position = new Vector2(
                    dest.X + (8 + mapPosition.Value.X * 8 + 2) * scale,
                    dest.Y + (8 + mapPosition.Value.Y * 8 + 2) * scale);

                if (_mapPlayer != null)
                {
                    _mapPlayer.DrawBasic(spriteBatch, position, Color.White, scale);
                    return;
                }

                // Reserva si el animator no cargó: punto pequeño con contorno.
                var d = Math.Max(3, (int)(5 * scale));
                var dot = new Rectangle((int)position.X, (int)position.Y, d, d);
                spriteBatch.Draw(_pixel, new Rectangle(dot.X - 1, dot.Y - 1, d + 2, d + 2), Color.Black * 0.8f);
                spriteBatch.Draw(_pixel, dot, new Color(255, 80, 80));
            }
            catch
            {
                // Un indicador nunca puede tumbar el panel.
            }
        }

        // ------------------------------------------------------------------ barra lateral

        private static void DrawSidebar(SpriteBatch spriteBatch, Rectangle panel)
        {
            var gm = Game1.GameManager;
            if (gm == null)
                return;

            var u = U(panel);
            var large = SwitchSettings.PanelHudLarge != 0;
            var s3 = Math.Max(1, (int)Math.Round((large ? 4 : 3) * u));
            var s4 = Math.Max(1, (int)Math.Round(4 * u));

            // Corazones: filas de 5 a x3 (120 px; x4 = 160 en HUD grande). El helper del juego
            // hace filas de 7 (168 px) y no cabe. Mismo cálculo de tipo 0..4 que DrawHearts.
            var y = panel.Y + (int)(16 * u);
            var heartsH = SwitchHudDraw.DrawHearts(spriteBatch, panel.X + (int)((SideX + 12) * u), y, s3, 5,
                Math.Max(0, Math.Min(gm.MaxHearts, 14)), gm.CurrentHealth);
            y += heartsH + (int)((large ? 10 : 16) * u);

            // Slots de mano en rombo, como InventoryOverlay.UpdateButtonLayout: W=56, p=4.
            var W = (int)((large ? 64 : 56) * u); var p = (int)(4 * u);
            var hand = HandSlotCount(gm);
            var cells = HandSlotRects(panel, y);
            var labels = ControlHandler.ControllerLabels;
            var ci = ControlHandler.ControllerIndex;
            var font = Resources.GameFont;
            var slotItemScale = Math.Max(1, (int)Math.Round((large ? 4 : 3) * u));
            var equipTab = SecondScreenState.Current == SecondScreenState.Tab.Equipment;
            for (var i = 0; i < hand; i++)
            {
                var cell = cells[i];
                spriteBatch.Draw(_pixel, cell, Color.Black * 0.15f);
                if (DragIndex >= 0 ? i == DragHoverSlot : (equipTab && i == EquipTargetSlot(gm)))
                {
                    // Arrastrando: el slot bajo el dedo. Si no, el destino de un toque suelto (A).
                    var t = Math.Max(2, (int)(3 * u));
                    var g = SwitchHudDraw.TunicGreen;
                    spriteBatch.Draw(_pixel, new Rectangle(cell.X, cell.Y, cell.Width, t), g);
                    spriteBatch.Draw(_pixel, new Rectangle(cell.X, cell.Bottom - t, cell.Width, t), g);
                    spriteBatch.Draw(_pixel, new Rectangle(cell.X, cell.Y, t, cell.Height), g);
                    spriteBatch.Draw(_pixel, new Rectangle(cell.Right - t, cell.Y, t, cell.Height), g);
                }

                var item = gm.Equipment[i];
                if (item != null)
                    SwitchHudDraw.DrawItemWithInfo(spriteBatch, item, cell, slotItemScale, Color.White);

                if (font != null && labels != null && ci >= 0 && ci < labels.GetLength(0) && i < labels.GetLength(1))
                {
                    var label = labels[ci, i] ?? "";
                    spriteBatch.DrawString(font, label, new Vector2(cell.X + 3 * u, cell.Y + 2 * u),
                        new Color(42, 36, 24), 0f, Vector2.Zero, Math.Max(1f, u), SpriteEffects.None, 0f);
                }
            }
            // cells[0] es el slot de abajo: su borde inferior es el del rombo entero.
            y = cells[0].Bottom + (int)((large ? 14 : 24) * u);

            // Rupias y llaves con los helpers del HUD (misma animación de contador).
            var digits = new Color(42, 36, 24);
            SwitchHudDraw.DrawRupees(spriteBatch, panel.X + (int)((SideX + 14) * u), y, s4, digits);
            y += (int)(52 * u);
            SwitchHudDraw.DrawSmallKeys(spriteBatch, panel.X + (int)((SideX + 42) * u), y, s4, digits);
        }

        // ------------------------------------------------------------------ rombo / EQUIPO

        public static int HandSlotCount(GameManager gm) =>
            Math.Max(0, Math.Min(Values.HandItemSlots, Math.Min(6, gm.Equipment.Length)));

        /// <summary>Slot del botón A del juego (índice 1, o 0 con SwapButtons): destino de un
        /// toque suelto sobre un objeto, como en TMC ALEKS. Arrastrando se elige otro slot.</summary>
        public static int EquipTargetSlot(GameManager gm)
        {
            var hand = HandSlotCount(gm);
            var a = GameSettings.SwapButtons ? 0 : 1;
            return a < hand ? a : 0;
        }

        /// <summary>Arrastre en curso: Equipment[DragIndex] bajo el dedo (px del panel), slot bajo el dedo.</summary>
        public static int DragIndex = -1;
        public static int DragPanelX, DragPanelY;
        public static int DragHoverSlot = -1;

        /// <summary>Alto de las filas de corazones tal y como las dibuja DrawSidebar.</summary>
        private static int HeartsHeight(Rectangle panel, GameManager gm)
        {
            var u = U(panel);
            var large = SwitchSettings.PanelHudLarge != 0;
            var s3 = Math.Max(1, (int)Math.Round((large ? 4 : 3) * u));
            var max = Math.Max(0, Math.Min(gm.MaxHearts, 14));
            return ((max + 4) / 5) * 8 * s3;
        }

        /// <summary>Origen Y del rombo (debajo de los corazones), en px de pantalla.</summary>
        private static int HandSlotsTop(Rectangle panel, GameManager gm)
        {
            var u = U(panel);
            var large = SwitchSettings.PanelHudLarge != 0;
            return panel.Y + (int)(16 * u) + HeartsHeight(panel, gm) + (int)((large ? 10 : 16) * u);
        }

        /// <summary>Celdas del rombo de slots de mano (índices = Equipment[i]), en px de pantalla.</summary>
        public static Rectangle[] HandSlotRects(Rectangle panel, int y)
        {
            var gm = Game1.GameManager;
            var u = U(panel);
            var large = SwitchSettings.PanelHudLarge != 0;
            var W = (int)((large ? 64 : 56) * u); var p = (int)(4 * u);
            var hand = gm != null ? HandSlotCount(gm) : 0;
            var six = hand > 4;
            var top = six ? W + p : 0;
            var ox = panel.X + (int)((SideX + 12) * u);
            var oy = y + top;
            Point[] pos =
            {
                new Point(W / 2 + p, 2 * W + 2 * p),   // 0 abajo
                new Point(W + 2 * p, W + p),           // 1 derecha
                new Point(0, W + p),                   // 2 izquierda
                new Point(W / 2 + p, 0),               // 3 arriba
                new Point(0, -W - p),                  // 4 L
                new Point(W + 2 * p, -W - p),          // 5 R
            };
            var r = new Rectangle[6];
            for (var i = 0; i < 6; i++)
                r[i] = new Rectangle(ox + pos[i].X, oy + pos[i].Y, W, W);
            return r;
        }

        /// <summary>HitTest: HitHandSlot + i = slot de mano i; HitBagItem + i = Equipment[i] de la bolsa.</summary>
        public const int HitHandSlot = 300;
        public const int HitBagItem = 200;

        private const int BagCols = 4;

        /// <summary>Celda de la bolsa para Equipment[index] (index >= slots de mano).</summary>
        private static Rectangle BagCellRect(Rectangle main, Rectangle panel, int bagPos)
        {
            var u = U(panel);
            var cell = (int)(112 * u); var step = (int)(124 * u);
            var gridW = BagCols * step - (step - cell);
            var x0 = main.X + (main.Width - gridW) / 2;
            var y0 = main.Y + (int)(20 * u);
            return new Rectangle(x0 + (bagPos % BagCols) * step, y0 + (bagPos / BagCols) * step, cell, cell);
        }

        /// <summary>
        /// Pestaña EQUIPO a lo TMC ALEKS: la bolsa (Equipment[hand..11]) dibujada por nosotros,
        /// tocable. Tocar un objeto lo intercambia con el slot destino del rombo.
        /// </summary>
        private static void DrawEquipment(SpriteBatch spriteBatch, Rectangle panel, Rectangle main)
        {
            var gm = Game1.GameManager;
            if (gm == null)
                return;
            var u = U(panel);
            var s4 = Math.Max(1, (int)Math.Round(4 * u));
            var font = Resources.GameFont;
            var hand = HandSlotCount(gm);
            var target = EquipTargetSlot(gm);

            for (var i = hand; i < gm.Equipment.Length; i++)
            {
                var r = BagCellRect(main, panel, i - hand);
                spriteBatch.Draw(_pixel, r, Color.Black * 0.12f);
                var item = gm.Equipment[i];
                if (item != null)
                    SwitchHudDraw.DrawItemWithInfo(spriteBatch, item, r, s4, Color.White);
            }

            if (font != null)
            {
                var labels = ControlHandler.ControllerLabels;
                var ci = ControlHandler.ControllerIndex;
                var btn = labels != null && ci >= 0 && ci < labels.GetLength(0) && target < labels.GetLength(1) ? labels[ci, target] : "?";
                var hint = SwitchStrings.Get("hint_equip", btn);
                var hs = font.MeasureString(hint) * Math.Max(1f, u);
                spriteBatch.DrawString(font, hint, new Vector2(main.X + (main.Width - hs.X) / 2, main.Bottom - hs.Y - 12 * u),
                    new Color(90, 83, 64), 0f, Vector2.Zero, Math.Max(1f, u), SpriteEffects.None, 0f);
            }
        }

        // ------------------------------------------------------------------ pestañas

        private static void DrawTabLabels(SpriteBatch spriteBatch, Rectangle panel)
        {
            var font = Resources.GameHeaderFont ?? Resources.GameFont;
            if (font == null)
                return;

            var blue = SwitchHudDraw.TunicBlue;
            var dim = new Color(90, 83, 64);
            var active = SecondScreenState.CurrentBarSlot;

            for (var i = 0; i < SecondScreenState.BarSlots; i++)
            {
                var r = TabButtonRect(panel, i);
                var text = i == SecondScreenState.BarQuest ? SwitchStrings.Get("tab_quest") :
                           i == SecondScreenState.BarEquipment ? SwitchStrings.Get("tab_equip") : SecondScreenState.MapBarLabel;
                var size = font.MeasureString(text);
                if (size.Y <= 0) continue;
                var scale = Math.Max(1, (int)Math.Floor(r.Height * 0.55f / size.Y));
                while (scale > 1 && size.X * scale > r.Width - 12) scale--;
                var pos = new Vector2(r.X + (r.Width - size.X * scale) / 2, r.Y + (r.Height - size.Y * scale) / 2);
                spriteBatch.DrawString(font, text, pos, i == active ? blue : dim, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            // Objeto arrastrado: fantasma bajo el dedo, por encima de todo.
            var gmDrag = Game1.GameManager;
            if (DragIndex >= 0 && gmDrag != null && DragIndex < gmDrag.Equipment.Length && gmDrag.Equipment[DragIndex] != null)
            {
                var u2 = U(panel);
                var s5 = Math.Max(1, (int)Math.Round(5 * u2));
                var ghost = new Rectangle(panel.X + DragPanelX - (int)(40 * u2), panel.Y + DragPanelY - (int)(56 * u2), (int)(80 * u2), (int)(80 * u2));
                spriteBatch.Draw(_pixel, ghost, Color.White * 0.35f);
                SwitchHudDraw.DrawItemWithInfo(spriteBatch, gmDrag.Equipment[DragIndex], ghost, s5, Color.White);
            }

            // Engranaje = AJUSTES: el icono del propio menú de opciones del juego.
            var gear = Resources.SprIconOptions;
            if (gear != null)
            {
                var g = GearRect(panel);
                var fit = AleksLayout.Fit(g.X + 8, g.Y + 8, g.Width - 16, g.Height - 16, gear.Width, gear.Height);
                spriteBatch.Draw(gear, new Rectangle(fit.X, fit.Y, fit.W, fit.H),
                    active == SecondScreenState.BarSettings ? Color.White : Color.White * 0.7f);
            }
        }

        // ------------------------------------------------------------------ AJUSTES

        /// <summary>
        /// Filas de la pestaña AJUSTES. Sustituyen a los combos L+R+… (que siguen funcionando).
        /// Tocar una fila cicla su valor; SwitchGame aplica y guarda (SwitchSettings.Save).
        /// </summary>
        public enum SettingsRow
        {
            FpsLimit = 0,
            ShowFps = 1,
            HideHud = 2,
            UiBlur = 3,
            FlipGrip = 4,
            Render = 5,
            HudSize = 6,
            UiLarge = 7,
            Optimized = 8,
        }
        public const int SettingsRowCount = 9;

        /// <summary>
        /// Confirmación de AJUSTES OPTIMIZADOS: el primer toque arma la fila durante unos
        /// segundos; el segundo toque dentro de ese plazo aplica. Reloj monótono (UtcNow
        /// está congelado en Switch).
        /// </summary>
        public static long OptimizedArmedUntilMs;
        public static bool OptimizedArmed => Environment.TickCount64 < OptimizedArmedUntilMs;
        public const int OptimizedArmMs = 4000;
        private const int SettingsRowH = 44, SettingsRowGap = 4, SettingsTop = 6;

        public static Rectangle SettingsRowRect(Rectangle main, Rectangle panel, int i)
        {
            var u = U(panel);
            return new Rectangle(
                main.X + (int)(16 * u),
                main.Y + (int)((SettingsTop + i * (SettingsRowH + SettingsRowGap)) * u),
                main.Width - (int)(32 * u),
                (int)(SettingsRowH * u));
        }

        public static string SettingsLabel(SettingsRow row)
        {
            switch (row)
            {
                case SettingsRow.FpsLimit: return SwitchStrings.Get("fps_limit");
                case SettingsRow.ShowFps:  return SwitchStrings.Get("fps_counter");
                case SettingsRow.HideHud:  return SwitchStrings.Get("game_hud");
                case SettingsRow.UiBlur:   return SwitchStrings.Get("ui_blur");
                case SettingsRow.FlipGrip: return SwitchStrings.Get("flip_grip");
                case SettingsRow.Render:   return SwitchStrings.Get("resolution");
                case SettingsRow.HudSize:  return SwitchStrings.Get("panel_hud");
                case SettingsRow.UiLarge:  return SwitchStrings.Get("game_ui");
                default:                   return SwitchStrings.Get("optimized");
            }
        }

        public static string SettingsValue(SettingsRow row)
        {
            switch (row)
            {
                case SettingsRow.FpsLimit: return SwitchSettings.FpsLimit == 0 ? SwitchStrings.Get("no_limit") : SwitchSettings.FpsLimit.ToString();
                case SettingsRow.ShowFps:  return SwitchStrings.Get(SwitchSettings.ShowFps != 0 ? "on" : "off");
                case SettingsRow.HideHud:  return SwitchStrings.Get(SwitchSettings.HideHud != 0 ? "hidden" : "visible");
                case SettingsRow.UiBlur:   return SwitchStrings.Get(SwitchSettings.NoUiBlur != 0 ? "off" : "on");
                case SettingsRow.FlipGrip: return SwitchStrings.Get(SwitchSettings.Flip != 0 ? "on" : "off");
                case SettingsRow.Render:   return SwitchStrings.Get(SwitchSettings.Render == 720 ? "res_fast" : "res_sharp");
                case SettingsRow.HudSize:  return SwitchStrings.Get(SwitchSettings.PanelHudLarge != 0 ? "large" : "normal");
                case SettingsRow.UiLarge:  return SwitchStrings.Get(SwitchSettings.UiLarge != 0 ? "large" : "normal");
                default:
                    if (OptimizedArmed) return SwitchStrings.Get(SwitchSettings.Optimized != 0 ? "undo_confirm" : "confirm");
                    return SwitchStrings.Get(SwitchSettings.Optimized != 0 ? "applied" : "apply");
            }
        }

        private static void DrawSettings(SpriteBatch spriteBatch, Rectangle panel, Rectangle main)
        {
            var u = U(panel);
            var font = Resources.GameFont;
            if (font == null)
                return;
            var textColor = new Color(42, 36, 24);
            var scale = Math.Max(1f, 1.5f * u);

            for (var i = 0; i < SettingsRowCount; i++)
            {
                var r = SettingsRowRect(main, panel, i);
                spriteBatch.Draw(_pixel, r, Color.Black * 0.12f);

                var label = SettingsLabel((SettingsRow)i);
                var value = SettingsValue((SettingsRow)i);
                var ls = font.MeasureString(label) * scale;
                var vs = font.MeasureString(value) * scale;
                spriteBatch.DrawString(font, label, new Vector2(r.X + 16 * u, r.Y + (r.Height - ls.Y) / 2),
                    textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                // Valor en una "pastilla" a la derecha, azul túnica: es lo que se toca.
                var pill = new Rectangle((int)(r.Right - vs.X - 40 * u), r.Y + (int)(10 * u), (int)(vs.X + 24 * u), r.Height - (int)(20 * u));
                var armed = (SettingsRow)i == SettingsRow.Optimized && OptimizedArmed;
                spriteBatch.Draw(_pixel, pill, Color.White * 0.9f);
                spriteBatch.DrawString(font, value, new Vector2(pill.X + 12 * u, pill.Y + (pill.Height - vs.Y) / 2),
                    armed ? SwitchHudDraw.TunicRed : SwitchHudDraw.TunicBlue, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            var hint = SwitchStrings.Get("hint_settings");
            var hs = font.MeasureString(hint) * Math.Max(1f, u);
            spriteBatch.DrawString(font, hint, new Vector2(main.X + (main.Width - hs.X) / 2, main.Bottom - hs.Y - 12 * u),
                new Color(90, 83, 64), 0f, Vector2.Zero, Math.Max(1f, u), SpriteEffects.None, 0f);
        }

        // ------------------------------------------------------------------ MISIÓN

        private static readonly string[] QuestCounts = { "shell", "goldLeaf", "heartMeter" };

        private static void DrawQuest(SpriteBatch spriteBatch, Rectangle panel, Rectangle main)
        {
            var gm = Game1.GameManager;
            if (gm == null || gm.ItemManager == null)
                return;

            var u = U(panel);
            var s3 = Math.Max(1, (int)Math.Round(3 * u));
            var font = Resources.GameFont;
            var textColor = new Color(42, 36, 24);
            var dimColor = new Color(90, 83, 64);
            var X = main.X; var Y = main.Y;

            // Instrumentos: 8 celdas 56x56, paso 66, desde x=24.
            var cell = (int)(56 * u); var step = (int)(66 * u);
            for (var i = 0; i < 8; i++)
            {
                var r = new Rectangle(X + (int)(16 * u) + i * step, Y + (int)(16 * u), cell, cell);
                spriteBatch.Draw(_pixel, r, Color.Black * 0.12f);
                var item = gm.ItemManager["instrument" + i];
                if (item == null) continue;
                var have = gm.GetItem("instrument" + i) != null;
                SwitchHudDraw.DrawItemCentered(spriteBatch, item, r, have ? Color.White : Color.White * 0.28f, s3);
            }

            // Colección: conchas /20, hojas /5, piezas de corazón /4, fotos /12.
            var cy = Y + (int)(96 * u);
            var boxH = (int)(48 * u);
            int[] maxes = { 20, 5, 4 };
            var bx = X + (int)(16 * u);
            for (var k = 0; k < QuestCounts.Length; k++)
            {
                var boxW = (int)(124 * u);
                var box = new Rectangle(bx, cy, boxW, boxH);
                spriteBatch.Draw(_pixel, box, Color.Black * 0.10f);
                var item = gm.ItemManager[QuestCounts[k]];
                var collected = gm.GetItem(QuestCounts[k]);
                var count = collected?.Count ?? 0;
                var iconW = 8;
                if (item != null)
                {
                    var size = SwitchHudDraw.ItemSize(item);
                    iconW = Math.Max(8, size.X);
                    SwitchHudDraw.DrawItem(spriteBatch, item, new Vector2(box.X + 6 * u, box.Y + (boxH - size.Y * s3) / 2), Color.White, s3);
                }
                var numX = box.X + (int)(6 * u) + iconW * s3 + (int)(8 * u);
                var digitsN = count >= 10 ? 2 : 1;
                SwitchHudDraw.DrawNumber(spriteBatch, numX, box.Y + (int)(14 * u), count, digitsN, s3, textColor);
                if (font != null)
                    spriteBatch.DrawString(font, "/" + maxes[k], new Vector2(numX + digitsN * 7 * s3 + 4 * u, box.Y + 26 * u),
                        dimColor, 0f, Vector2.Zero, Math.Max(1f, u), SpriteEffects.None, 0f);
                bx += boxW + (int)(12 * u);
            }
            {
                var photos = 0;
                try
                {
                    for (var i = 0; i < 12; i++)
                        if (!string.IsNullOrEmpty(gm.SaveManager.GetString("photo_" + (i + 1)))) photos++;
                }
                catch { }
                var box = new Rectangle(bx, cy, (int)(112 * u), boxH);
                spriteBatch.Draw(_pixel, box, Color.Black * 0.10f);
                if (font != null)
                {
                    spriteBatch.DrawString(font, SwitchStrings.Get("photos"), new Vector2(box.X + 8 * u, box.Y + 6 * u), dimColor, 0f, Vector2.Zero, Math.Max(1f, u), SpriteEffects.None, 0f);
                    spriteBatch.DrawString(font, photos + "/12", new Vector2(box.X + 8 * u, box.Y + 26 * u), textColor, 0f, Vector2.Zero, Math.Max(1f, u * 1.5f), SpriteEffects.None, 0f);
                }
            }

            // Intercambio: 14 celdas 64x72 en 2 filas de 7. El objeto que Link lleva ahora,
            // marcado (solo puede haber uno: es el siguiente a entregar).
            var ty = Y + (int)(170 * u);
            if (font != null)
                spriteBatch.DrawString(font, SwitchStrings.Get("trade"), new Vector2(X + 16 * u, ty), dimColor, 0f, Vector2.Zero, Math.Max(1f, u), SpriteEffects.None, 0f);
            ty += (int)(16 * u);
            var cw = (int)(64 * u); var ch = (int)(72 * u); var cs = (int)(76 * u); var rs = (int)(84 * u);
            var reached = 14;
            for (var i = 0; i < 14; i++)
                if (gm.GetItem("trade" + i) != null) { reached = i; break; }
            for (var i = 0; i < 14; i++)
            {
                var r = new Rectangle(X + (int)(16 * u) + (i % 7) * cs, ty + (i / 7) * rs, cw, ch);
                spriteBatch.Draw(_pixel, r, Color.Black * 0.12f);
                var item = gm.ItemManager["trade" + i];
                if (item == null) continue;
                var started = reached < 14;      // 14 = ningun tradeN en el bolsillo = cadena sin empezar (§3.10)
                var done = started && i < reached;      // ya entregado
                var current = started && i == reached;  // en el bolsillo
                var col = current ? Color.White : (done ? Color.White * 0.55f : Color.White * 0.28f);
                SwitchHudDraw.DrawItemCentered(spriteBatch, item, r, col, s3);
                if (current)
                {
                    var t = Math.Max(2, (int)(3 * u));
                    var orange = new Color(232, 84, 42);
                    spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, r.Width, t), orange);
                    spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Bottom - t, r.Width, t), orange);
                    spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, t, r.Height), orange);
                    spriteBatch.Draw(_pixel, new Rectangle(r.Right - t, r.Y, t, r.Height), orange);
                }
                if (font != null)
                    spriteBatch.DrawString(font, (i + 1).ToString(), new Vector2(r.X + 3 * u, r.Y + 2 * u), dimColor, 0f, Vector2.Zero, Math.Max(1f, u), SpriteEffects.None, 0f);
            }

            // SIGUIENTE PASO (story guide, STORY_GUIDE.md) y, debajo, a quien entregar el objeto
            // del bolsillo. Ocupa el hueco bajo la rejilla (~108 px): rotulo + 2 lineas + 2 lineas.
            if (font != null)
            {
                var gy = ty + 2 * rs + (int)(4 * u);
                var textScale = Math.Max(1f, 1.4f * u);
                var lineH = (int)(font.LineSpacing * textScale);
                var gx = X + (int)(16 * u);

                spriteBatch.DrawString(font, SwitchStoryGuide.NextStepLabel, new Vector2(gx, gy), dimColor, 0f, Vector2.Zero, Math.Max(1f, u), SpriteEffects.None, 0f);
                gy += (int)(11 * u);
                var stepText = SwitchStoryGuide.NextStep(gm, reached);
                if (stepText != null)
                    spriteBatch.DrawString(font, stepText, new Vector2(gx, gy), textColor, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
                gy += 2 * lineH + (int)(4 * u);
                spriteBatch.DrawString(font, SwitchStoryGuide.TradeText(reached), new Vector2(gx, gy), dimColor, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
            }
        }
    }
}
#endif
