#if SWITCH
using System;

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// C# port of aleks_layout.c from the Z3NXB (zelda3 ALEKS) build - THE authority for
    /// where anything lands on the screen. Ported arithmetic, not reinvented: the donor
    /// file is the physically tested authority for NORMAL / DUAL layouts on Switch.
    ///
    /// Differences from the donor, deliberate:
    ///  - LADXHD pixels are square (PAR 1:1). The SNES 7:6 PAR correction does not apply;
    ///    FrameAspect therefore returns src_w:src_h directly.
    ///  - FLIP (Flip Grip portrait) is not ported yet - phase 2 if wanted.
    ///  - RIGHTBAR is new: LADXHD's own composition letterboxes the game inside
    ///    MainRenderTarget, so the cheapest non-invasive DUAL is "leave the game frame
    ///    untouched, put the companion in the dead right-hand bar".
    ///
    /// Everything is pure integer arithmetic, no state beyond the surface correction,
    /// safe to call every frame - same contract as the donor.
    /// </summary>
    public static class AleksLayout
    {
        /// <summary>The companion's logical surface - the 3DS bottom screen the donor UI
        /// was written against. Kept so donor proportions transfer 1:1.</summary>
        public const int CompanionW = 320;
        public const int CompanionH = 240;

        /// <summary>
        /// Lienzo lógico del modo FLIP (Flip Grip). Fijo a propósito, como en los dos
        /// donantes: todo se compone en 720x1280 vertical y el compositor lo rota UNA vez
        /// al presentar. Nada aguas arriba sabe que hay una rotación.
        /// </summary>
        /// <summary>Resolución del digitalizador de la consola: fija, independiente del backbuffer.</summary>
        public const int TouchPanelW = 1280, TouchPanelH = 720;

        public const int FlipCanvasW = 720;
        public const int FlipCanvasH = 1280;

        public struct Rect { public int X, Y, W, H; public bool IsEmpty => W <= 0 || H <= 0; }

        public enum Mode
        {
            Off = 0,      // game alone, exactly as today
            RightBar,     // companion aspect-fitted into the right side of the output
            Dual,         // donor DUAL: explicit split, game left + companion right
            Flip,         // Flip Grip: lienzo vertical 720x1280 rotado 270, juego arriba
        }

        public struct Layout
        {
            public Mode Mode;
            public Rect Game;        // W==0 => do not re-place the game (RightBar/Off)
            public Rect Companion;   // W==0 => no companion this mode

            // --- solo FLIP ---
            /// <summary>Tamaño del espacio de coordenadas en el que están Game/Companion.
            /// En FLIP es el lienzo 720x1280; en el resto de modos, la salida.</summary>
            public int LogicalW, LogicalH;
            /// <summary>0, o 270 en FLIP.</summary>
            public int RotationDegrees;
            /// <summary>Rectángulo de la SALIDA donde aterriza el lienzo, ya sin rotar.</summary>
            public Rect FlipDst;
            /// <summary>Lienzo -> salida, por eje. Los dos difieren, y es correcto.</summary>
            public float FlipScaleX, FlipScaleY;
            /// <summary>Hueco realmente aplicado entre juego y companion (puede recortarse).</summary>
            public int EffectiveGap;
        }

        // ------------------------------------------------------------------ surface corr
        //
        // SURFACE CORRECTION (donor lesson, kept verbatim in spirit): the renderer's
        // coordinate space is not necessarily the shape of the panel. zelda3 on Switch/Eden
        // measured a 1280x1280 SDL surface stretched onto a 16:9 panel. LADXHD's backbuffer
        // has so far matched the display (1920x1080 on 1080p), so this defaults to 1:1 -
        // but the correction exists because discovering it late cost the donor project a
        // "wide and squashed" picture. Exact rational, integer fits.
        private static long _corrNum = 1, _corrDen = 1;

        private static long Gcd(long a, long b) { while (b != 0) { var t = a % b; a = b; b = t; } return Math.Abs(a); }

        public static void SetSurface(int surfaceW, int surfaceH, int displayW, int displayH)
        {
            if (surfaceW <= 0 || surfaceH <= 0 || displayW <= 0 || displayH <= 0) { _corrNum = _corrDen = 1; return; }
            long n = (long)surfaceW * displayH;
            long d = (long)displayW * surfaceH;
            var g = Gcd(n, d);
            if (g > 0) { n /= g; d /= g; }
            _corrNum = n; _corrDen = d;
        }

        // ------------------------------------------------------------------ fits

        /// <summary>Displayed aspect of a src_w x src_h LADXHD frame. Square pixels.</summary>
        public static void FrameAspect(int srcW, int srcH, out int num, out int den)
        {
            num = srcW <= 0 ? 160 : srcW;
            den = srcH <= 0 ? 128 : srcH;
        }

        /// <summary>
        /// The one aspect-fit helper (donor contract): largest rectangle inside the box
        /// that APPEARS as num:den on the panel, centred. Every placement goes through
        /// this - there is no second fit anywhere.
        /// </summary>
        public static Rect Fit(int boxX, int boxY, int boxW, int boxH, int num, int den)
        {
            var r = new Rect { X = boxX, Y = boxY, W = 0, H = 0 };
            if (num <= 0 || den <= 0 || boxW <= 0 || boxH <= 0) return r;

            long n = (long)num * _corrNum;
            long d = (long)den * _corrDen;
            var g = Gcd(n, d);
            if (g > 0) { n /= g; d /= g; }

            int w, h;
            if ((long)boxW * d >= (long)boxH * n) { h = boxH; w = (int)((long)boxH * n / d); }
            else                                  { w = boxW; h = (int)((long)boxW * d / n); }
            r.W = w; r.H = h;
            r.X = boxX + (boxW - w) / 2;
            r.Y = boxY + (boxH - h) / 2;
            return r;
        }

        /// <summary>
        /// Ajuste SIN corrección de superficie, centrado. Existe solo para el interior del
        /// lienzo de FLIP.
        ///
        /// LECCIÓN DEL DONANTE, y de las caras. Comentario literal de aleks_layout.c:
        /// "Applying the correction twice is what shrank FLIP: a 4:3 game came out 543x724
        /// in the canvas instead of 720x540, so the stack no longer filled the portrait
        /// screen." El lienzo se mapea a la pantalla de una sola vez, en FlipDst, así que
        /// la corrección pertenece ahí y a ningún otro sitio.
        /// </summary>
        public static Rect FitRaw(int boxX, int boxY, int boxW, int boxH, int num, int den)
        {
            var r = new Rect { X = boxX, Y = boxY, W = 0, H = 0 };
            if (num <= 0 || den <= 0 || boxW <= 0 || boxH <= 0) return r;

            int w, h;
            if ((long)boxW * den >= (long)boxH * num) { h = boxH; w = (int)((long)boxH * num / den); }
            else                                      { w = boxW; h = (int)((long)boxW * den / num); }
            r.W = w; r.H = h;
            r.X = boxX + (boxW - w) / 2;
            r.Y = boxY + (boxH - h) / 2;
            return r;
        }

        // ------------------------------------------------------------------ modes

        /// <summary>
        /// Pure arithmetic, keeps no state, safe every frame (donor contract).
        /// gameVisibleW: the width in output pixels the game's own composition actually
        /// occupies (its letterbox bars are OUTSIDE that). Pass 0 when unknown - RightBar
        /// then assumes a bar of outW/4, which is the conservative fallback.
        /// companionPct: companion size as % of what the layout allows (donor semantics:
        /// 100 = as large as this layout allows). Clamped 40..100.
        /// </summary>
        public static Layout Compute(Mode mode, int outW, int outH, int gameVisibleW, int companionPct)
        {
            var l = new Layout
            {
                Mode = mode,
                LogicalW = outW,
                LogicalH = outH,
                RotationDegrees = 0,
                FlipScaleX = 1f,
                FlipScaleY = 1f,
            };
            if (mode == Mode.Off || outW <= 0 || outH <= 0)
                return l;

            companionPct = companionPct < 40 ? 40 : (companionPct > 100 ? 100 : companionPct);

            if (mode == Mode.Flip)
                return ComputeFlip(outW, outH, companionPct, gameVisibleW);

            if (mode == Mode.RightBar)
            {
                // The game stays exactly where LADXHD put it. The companion gets the dead
                // space to the right of the visible game frame.
                int barW = gameVisibleW > 0 && gameVisibleW < outW
                    ? (outW - gameVisibleW) / 2
                    : outW / 4;
                int margin = Math.Max(4, barW / 24);
                int boxX = outW - barW + margin;
                int boxY = margin;
                int boxW = barW - 2 * margin;
                int boxH = outH - 2 * margin;
                var slot = Fit(boxX, boxY, boxW, boxH, CompanionW, CompanionH);
                // percentage shrinks from "as large as the slot allows", donor semantics
                l.Companion = Shrink(slot, companionPct);
                return l;
            }

            // DUAL (donor): companion takes its share of the canvas from the right,
            // game aspect-fits in what is left. Nothing is left over by accident at 100%.
            {
                int compBoxW = (int)((long)outW * 35 * companionPct / (100L * 100L)); // 35% of width at 100%
                int gap = Math.Max(4, outW / 160);
                l.Companion = Fit(outW - compBoxW, 0, compBoxW, outH, CompanionW, CompanionH);
                // The game frame here is the full MainRenderTarget (already composed),
                // so its aspect is simply the output aspect.
                FrameAspect(outW, outH, out var gn, out var gd);
                l.Game = Fit(0, 0, outW - compBoxW - gap, outH, gn, gd);
                return l;
            }
        }

        /// <summary>
        /// FLIP, portado de compute_flip() en aleks_layout.c. Sobre el lienzo vertical fijo
        /// de 720x1280 el compositor rota una sola vez:
        ///
        ///   +------------------+  0
        ///   |       juego      |   ocupa la altura que sobra, al ancho completo
        ///   +------------------+
        ///   |       hueco      |
        ///   +------------------+
        ///   |     companion    |   4:3, ancho completo del lienzo al 100%
        ///   +------------------+  1280
        ///
        /// Con companionPct = 100 y hueco 20 el reparto sale exacto:
        /// companion 720x540, juego 720x720, 540 + 20 + 720 = 1280 clavados.
        ///
        /// DESVIACIÓN respecto a los donantes, deliberada: allí el juego es un framebuffer
        /// de tamaño fijo que se ajusta con un aspecto (fit_raw sobre FrameAspect). LADXHD
        /// no tiene resolución nativa — dibuja por cámara a la resolución que se le dé — así
        /// que el juego toma la ranura ENTERA y es la escala de cámara la que decide cuánto
        /// mundo se ve. Por eso aquí no hay ajuste de aspecto para el juego: no hay ninguno
        /// que preservar.
        ///
        /// gameAspectHint: si es > 0 se interpreta como num:den y el juego SÍ se ajusta a
        /// ese aspecto dentro de la ranura. Queda para cuando se quiera forzar 16:9 o 5:4.
        /// </summary>
        private static Layout ComputeFlip(int outW, int outH, int companionPct, int gameAspectHint)
        {
            var l = new Layout
            {
                Mode = Mode.Flip,
                LogicalW = FlipCanvasW,
                LogicalH = FlipCanvasH,
                RotationDegrees = 270,
            };

            // El lienzo es vertical y se rota sobre un panel apaisado, así que lo que tiene
            // que VERSE bien es la huella ya rotada: 1280 de ancho por 720 de alto. Se ajusta
            // ese aspecto (con corrección de superficie, como todo lo demás) y luego se
            // des-rota para saber en qué rectángulo se dibuja el lienzo: al rotar, ancho y
            // alto se intercambian.
            var footprint = Fit(0, 0, outW, outH, FlipCanvasH, FlipCanvasW);
            l.FlipDst = new Rect
            {
                W = footprint.H,   // intercambiados por la rotación
                H = footprint.W,
            };
            l.FlipDst.X = (outW - l.FlipDst.W) / 2;
            l.FlipDst.Y = (outH - l.FlipDst.H) / 2;

            l.FlipScaleX = l.FlipDst.W > 0 ? l.FlipDst.W / (float)FlipCanvasW : 1f;
            l.FlipScaleY = l.FlipDst.H > 0 ? l.FlipDst.H / (float)FlipCanvasH : 1f;
            if (l.FlipScaleX <= 0f) l.FlipScaleX = 1f;
            if (l.FlipScaleY <= 0f) l.FlipScaleY = 1f;

            // Companion: ancho como porcentaje del lienzo, alto por su aspecto 4:3.
            int compW = FlipCanvasW * companionPct / 100;
            int compH = compW * CompanionH / CompanionW;

            int gap = FlipGap < 0 ? 0 : (FlipGap > 128 ? 128 : FlipGap);
            int remaining = FlipCanvasH - compH - gap;
            if (remaining < 200)
            {
                // No cabe con hueco: el juego manda, el hueco se sacrifica.
                gap = 0;
                remaining = FlipCanvasH - compH;
            }
            l.EffectiveGap = gap;
            if (remaining < 0) remaining = 0;

            // Ajustes RAW: el lienzo se mapea a la pantalla de una sola vez en FlipDst, así
            // que aquí NO se aplica la corrección de superficie (ver FitRaw).
            // La ranura del juego va en 4:3. Probado en consola: una ranura CUADRADA se ve
            // rara, porque no es la proporcion para la que esta pensada la composicion del
            // juego. Al ancho del lienzo, 4:3 son 720x540 exactos.
            l.Game = FitRaw(0, 0, FlipCanvasW, remaining, FlipGameAspectW, FlipGameAspectH);

            l.Companion = FitRaw(0, 0, compW, compH, CompanionW, CompanionH);

            // Pila centrada verticalmente en el lienzo.
            int total = l.Game.H + gap + l.Companion.H;
            int stackY = (FlipCanvasH - total) / 2;
            if (stackY < 0) stackY = 0;

            l.Game.X = (FlipCanvasW - l.Game.W) / 2;
            l.Game.Y = stackY;
            l.Companion.X = (FlipCanvasW - l.Companion.W) / 2;
            l.Companion.Y = stackY + l.Game.H + gap;

            return l;
        }

        /// <summary>Hueco entre juego y companion en el lienzo de FLIP.</summary>
        public static int FlipGap = 20;

        /// <summary>
        /// Proporcion de la ranura del juego en FLIP. 4:3 por decision del usuario tras verlo
        /// en consola: la ranura cuadrada se veia rara.
        /// </summary>
        public static int FlipGameAspectW = 4, FlipGameAspectH = 3;

        private static Rect Shrink(Rect r, int pct)
        {
            if (pct >= 100 || r.IsEmpty) return r;
            int w = r.W * pct / 100, h = r.H * pct / 100;
            return new Rect { X = r.X + (r.W - w) / 2, Y = r.Y + (r.H - h) / 2, W = w, H = h };
        }

        /// <summary>
        /// Physical point -> companion-local (0..319, 0..239). False when outside the
        /// companion - what keeps a tap on the game half from reaching the UI (donor
        /// contract; used when touch input is wired in phase 2).
        /// </summary>
        public static bool MapTouch(in Layout layout, int physX, int physY, out int localX, out int localY)
        {
            localX = localY = 0;

            int px = physX, py = physY;

            if (layout.RotationDegrees == 270)
            {
                // El lienzo se dibuja en FlipDst y se gira en sentido horario alrededor del
                // centro de la pantalla, así que el eje X del lienzo acaba sobre el Y de la
                // pantalla y viceversa. Cada eje lleva su propia escala (ver ComputeFlip).
                // Portado literalmente del donante: aquí improvisar sale caro.
                float fx = layout.FlipScaleX > 0f ? layout.FlipScaleX : 1f;
                float fy = layout.FlipScaleY > 0f ? layout.FlipScaleY : 1f;
                int outW = layout.FlipDst.W > 0 ? layout.FlipDst.X * 2 + layout.FlipDst.W : layout.LogicalW;
                int outH = layout.FlipDst.H > 0 ? layout.FlipDst.Y * 2 + layout.FlipDst.H : layout.LogicalH;

                px = (int)(layout.LogicalW / 2.0f - (physY - outH / 2.0f) / fx);
                py = (int)(layout.LogicalH / 2.0f + (physX - outW / 2.0f) / fy);
            }

            var c = layout.Companion;
            if (c.IsEmpty || px < c.X || py < c.Y || px >= c.X + c.W || py >= c.Y + c.H)
                return false;
            localX = (px - c.X) * CompanionW / c.W;
            localY = (py - c.Y) * CompanionH / c.H;
            return true;
        }

        /// <summary>
        /// Igual que MapTouch, pero devuelve la posición en PÍXELES DEL PANEL (origen en la
        /// esquina superior izquierda del companion, sin normalizar a 320x240). Es lo que
        /// necesita el hit-test de los controles dibujados, cuyas cotas están en px del panel.
        /// Devuelve true aunque el toque caiga fuera del panel (coordenadas negativas o mayores),
        /// para poder registrarlo; `inside` dice si cayó dentro.
        /// </summary>
        public static bool MapTouchPixels(in Layout layout, int physX, int physY,
                                          out int panelX, out int panelY, out bool inside)
        {
            panelX = panelY = 0;
            inside = false;

            int px = physX, py = physY;
            if (layout.RotationDegrees == 270)
            {
                float fx = layout.FlipScaleX > 0f ? layout.FlipScaleX : 1f;
                float fy = layout.FlipScaleY > 0f ? layout.FlipScaleY : 1f;
                int outW = layout.FlipDst.W > 0 ? layout.FlipDst.X * 2 + layout.FlipDst.W : layout.LogicalW;
                int outH = layout.FlipDst.H > 0 ? layout.FlipDst.Y * 2 + layout.FlipDst.H : layout.LogicalH;

                // El panel táctil entrega SIEMPRE 1280x720 (HidTouchState), sea cual sea el
                // tamaño del backbuffer (en FLIP el juego corre a 1920x1080 y la consola lo
                // reescala). Sin esto los taps caían a 1.5x de donde se tocaba.
                float sx = outW / (float)TouchPanelW, sy = outH / (float)TouchPanelH;
                float tx = physX * sx, ty = physY * sy;

                px = (int)(layout.LogicalW / 2.0f - (ty - outH / 2.0f) / fx);
                py = (int)(layout.LogicalH / 2.0f + (tx - outW / 2.0f) / fy);
            }

            var c = layout.Companion;
            if (c.IsEmpty)
                return false;
            panelX = px - c.X;
            panelY = py - c.Y;
            inside = panelX >= 0 && panelY >= 0 && panelX < c.W && panelY < c.H;
            return true;
        }
    }
}
#endif
