#if SWITCH
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.InGame.Things;

namespace ProjectZ
{
    /// <summary>
    /// Game1 con los añadidos del port Switch.
    ///
    /// El spike v2.0.4 metía la llamada al second screen dentro de Game1.Draw con un
    /// bloque #if SWITCH. Desde v2.0.5 Core no lleva código de plataforma, así que en vez
    /// de parchearlo heredamos: Game1 no es sealed y Draw es protected override, de modo
    /// que el panel se compone después del frame del juego sin tocar una sola línea de Core.
    ///
    /// Se dibuja DESPUÉS de base.Draw(), no en mitad del pipeline como hacía el spike.
    /// Es más seguro (no puede alterar render targets ni SpriteBatch a medio frame) y para
    /// un panel en la barra lateral da igual: esa zona es letterbox, el juego no dibuja ahí.
    /// </summary>
    public class SwitchGame : Game1
    {
        public SwitchGame(bool editorMode, bool loadSave, int loadSlot)
            : base(editorMode, loadSave, loadSlot)
        {
        }

        // Combos (17-sep-2026, solo quedan dos):
        //         L + R + X               -> modo Flip Grip (vertical) <-> normal
        //         L + R + cruceta izq/der -> cambiar pestana del panel (sin toque)
        // Todo lo demas (fps, contador, desenfoque, optimizados) esta en la pestana AJUSTES.
        // Los tres botones estan mapeados a acciones del juego por separado (LB/RB/RS), pero
        // los tres a la vez no significan nada, asi que no hay riesgo de pulsarlo sin querer.
        // Se hace aqui, leyendo el gamepad en crudo, para no tocar ControlHandler en Core.
        // Fase 2 lo sustituira por una fila normal en Ajustes -> Video.
        private bool _fpsComboWasDown;
        private bool _tabComboWasDown;
        private bool _blurComboWasDown;
        private bool _benchComboWasDown;
        private bool _limitComboWasDown;
        private int _appliedFpsLimit = -1;
        private bool _flipComboWasDown;
        private int _appliedFlip = -1;
        private int _appliedFlipScale = -1;

        // Escala de camara forzada al arrancar. Se aplica en el primer Update: para entonces
        // Initialize/LoadContent ya han cargado los ajustes guardados, asi que no nos los pisa
        // a nosotros ni nosotros a ellos a destiempo.
        private bool _gameScaleApplied;

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!_gameScaleApplied)
            {
                _gameScaleApplied = true;
                if (SwitchSettings.GameScale > 0)
                {
                    GameSettings.GameScale = SwitchSettings.GameScale;
                    ScaleChanged = true;
                    SwitchDiag.Log("secondscreen.log",
                        "gameScale forzada a " + SwitchSettings.GameScale);
                }

                // Desenfoque de UI apagado por defecto. Medido en consola: activarlo cuesta
                // fps de forma clara, y son pasadas a pantalla completa con alpha blending,
                // justo lo que satura el ancho de banda en Tegra X1.
                if (SwitchSettings.NoUiBlur != 0)
                {
                    GameSettings.DisableUiBlur = true;
                    SwitchDiag.Log("secondscreen.log", "desenfoque de UI apagado por defecto");
                }
            }

            ReapplyOptimizedOnBoot();
            SwitchVideoOptions.InjectStrings();

            // UI del juego un paso mas grande (ver Game1.UiScaleRoundUp). Cada frame: es un bool.
            var roundUp = SwitchSettings.UiLarge != 0;
            if (UiScaleRoundUp != roundUp)
            {
                UiScaleRoundUp = roundUp;
                ScaleChanged = true;
            }

            if (SettingsChangedFromMenu)
            {
                SettingsChangedFromMenu = false;
                _appliedFlip = -1;
                _appliedRenderScale = -1;
            }

            ApplyRenderScale();

            var fpsCombo = false;
            var tabNext = false;
            var tabPrev = false;
            var blurCombo = false;
            var benchCombo = false;
            var limitCombo = false;
            var flipCombo = false;
            for (var i = 0; i < 4; i++)
            {
                var pad = GamePad.GetState((PlayerIndex)i);
                if (!pad.IsConnected)
                    continue;

                var shoulders =
                    pad.Buttons.LeftShoulder == ButtonState.Pressed &&
                    pad.Buttons.RightShoulder == ButtonState.Pressed;

                // 17-sep-2026: fuera todos los combos salvo el de FLIP. El resto vive en la
                // pestana AJUSTES del panel (tocable) y los combos se disparaban sin querer
                // (L+R+Y bloqueaba el limite de fps sin que se supiera por que).
                if (shoulders && pad.Buttons.X  == ButtonState.Pressed) flipCombo = true;
                // Cruceta izq/der con L+R para cambiar de pestana: se mantiene porque no
                // colisiona con nada y es la unica navegacion sin toque.
                if (shoulders && pad.DPad.Right == ButtonState.Pressed) tabNext = true;
                if (shoulders && pad.DPad.Left  == ButtonState.Pressed) tabPrev = true;
            }

            // Por flanco: solo actua al pulsar, no mientras se mantiene.
            if (fpsCombo && !_fpsComboWasDown)
            {
                SwitchSettings.ShowFps = SwitchSettings.ShowFps != 0 ? 0 : 1;
                SwitchSettings.Save();
                SwitchDiag.Log("secondscreen.log",
                    "toggle fps -> " + (SwitchSettings.ShowFps != 0 ? "ON" : "OFF"));
            }
            _fpsComboWasDown = fpsCombo;

            // Cambio manual de pestana del panel (fija el "pin" hasta cambiar de contexto).
            var tabCombo = tabNext || tabPrev;
            if (tabCombo && !_tabComboWasDown && SwitchSettings.Flip != 0)
                SecondScreenState.Cycle(tabNext ? 1 : -1);
            _tabComboWasDown = tabCombo;

            PollTouch();

            // Alternar el desenfoque de la UI en el sitio, para poder medir su coste sin
            // entrar al menu (entrar cambia la escena y falsea la comparacion).
            // OpaqueHudBg = true significa "sin desenfoque": el juego se salta BlurImage()
            // y las pasadas a pantalla completa asociadas.
            if (blurCombo && !_blurComboWasDown)
            {
                GameSettings.DisableUiBlur = !GameSettings.DisableUiBlur;
                SwitchDiag.Log("secondscreen.log",
                    "desenfoque de UI -> " + (GameSettings.DisableUiBlur ? "OFF (opaco)" : "ON"));
            }
            _blurComboWasDown = blurCombo;

            // Benchmark automatico: recorre las 8 combinaciones de panel/desenfoque/niebla,
            // mide cada una y escribe crash/benchmark.log. Evita tener que anotar a mano.
            if (benchCombo && !_benchComboWasDown)
                SwitchBenchmark.Start();
            _benchComboWasDown = benchCombo;

            // Ciclar el limite de fps: sin limite -> 30 -> 45 -> 60 -> sin limite.
            if (limitCombo && !_limitComboWasDown)
            {
                SwitchSettings.FpsLimit =
                    SwitchSettings.FpsLimit == 0  ? 30 :
                    SwitchSettings.FpsLimit == 30 ? 45 :
                    SwitchSettings.FpsLimit == 45 ? 60 : 0;
                SwitchSettings.Save();
                SwitchDiag.Log("secondscreen.log",
                    "limite de fps -> " + (SwitchSettings.FpsLimit == 0 ? "sin limite" : SwitchSettings.FpsLimit.ToString()));
            }
            _limitComboWasDown = limitCombo;

            if (flipCombo && !_flipComboWasDown)
            {
                SwitchSettings.Flip = SwitchSettings.Flip != 0 ? 0 : 1;
                SwitchSettings.Save();
                SwitchDiag.Log("secondscreen.log",
                    "Flip Grip -> " + (SwitchSettings.Flip != 0 ? "ON" : "OFF"));
            }
            _flipComboWasDown = flipCombo;

            ApplyFlipMode();
            ApplyFpsLimit();

            // El estado de pestanas se actualiza AQUI, no dentro de Draw: si no, en el frame
            // en que entras a una mazmorra el refresco usaba todavia la pestana anterior.
            if (SwitchSettings.Flip != 0)
            {
                SecondScreenState.Update();
                SecondScreen.Update();
            }

            ApplyHideHud();
        }

        // HUD del juego oculto mientras el panel esta visible (la barra lateral ES el HUD).
        // OverlayManager.HideHud ya existe en Core y anima la salida con el fade de los menus.
        private bool _hudHidden;

        private void ApplyHideHud()
        {
            var overlay = GameManager?.InGameOverlay;
            if (overlay == null)
                return;

            var wantHidden = _flipActive && SwitchSettings.HideHud != 0;
            try
            {
                // Cada frame, no solo por flanco: al cargar partida el juego recrea su estado
                // de overlay y el HUD volvia a aparecer con la fila diciendo OCULTO (foto del
                // 17-sep). HideHud solo escribe un bool; es gratis.
                overlay.HideHud(wantHidden);
                if (wantHidden == _hudHidden)
                    return;
                _hudHidden = wantHidden;
                SwitchDiag.Log("secondscreen.log", "HUD del juego -> " + (wantHidden ? "oculto (panel)" : "visible"));
            }
            catch (System.Exception e)
            {
                SwitchDiag.LogOnce("secondscreen.log", "hidehudfail", "HideHud fallo: " + e.Message);
            }
        }

        /// <summary>
        /// Reemite el frame estirado al panel completo cuando se renderiza a menos resolucion.
        ///
        /// ERROR QUE ESTO CORRIGE: yo asumi que el blit final de Game1 estiraba la imagen al
        /// backbuffer. No lo hace. Hace esto:
        ///
        ///     Viewport = new Viewport(0, 0, MainRenderTarget.Width, MainRenderTarget.Height);
        ///     SpriteBatch.Draw(_finalRenderTarget, new Rectangle(0, 0, MainW, MainH), White);
        ///
        /// o sea, fija el viewport al tamano del render target y dibuja 1:1. Con
        /// RenderSizeOverride activo eso deja la imagen en una esquina y el resto en negro,
        /// que es justo el recorte que se vio en consola.
        ///
        /// Solucion: recuperar el viewport completo y reemitir el frame estirado. Cuesta un
        /// blit a pantalla completa, pero se ahorra renderizar TODA la escena (con su
        /// sobredibujado, blur y niebla) a resolucion nativa, asi que sigue compensando.
        /// </summary>
        private void PresentScaledFrame()
        {
            if (!RenderSizeOverride.HasValue)
                return;

            try
            {
                var frame = Game1.FinalRenderTarget;
                var device = Graphics?.GraphicsDevice;
                if (frame == null || frame.IsDisposed || device == null)
                    return;

                var pp = device.PresentationParameters;
                device.Viewport = new Viewport(0, 0, pp.BackBufferWidth, pp.BackBufferHeight);
                device.Clear(Color.Black);

                SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                SpriteBatch.Draw(frame,
                    new Rectangle(0, 0, pp.BackBufferWidth, pp.BackBufferHeight), Color.White);
                SpriteBatch.End();
            }
            catch (System.Exception e)
            {
                // Si falla, volver a resolucion nativa antes que dejar la pantalla rota.
                SwitchDiag.LogOnce("secondscreen.log", "presentfail",
                    "PresentScaledFrame fallo: " + e.GetType().Name + " - volviendo a nativo");
                SwitchSettings.RenderScale = 100;
                _appliedRenderScale = -1;
            }
        }


        /// <summary>
        /// Toque sobre el panel. Solo en FLIP: en apaisado el panel no existe y con la consola
        /// en el dock la pantalla táctil ni siquiera está accesible.
        ///
        /// La acción se dispara al LEVANTAR el dedo (contrato de los donantes). El punto se
        /// registra SIEMPRE, caiga donde caiga: si la inversa de la rotación estuviera mal, el
        /// log lo enseña en vez de dejar un panel que "no responde".
        ///
        /// Blindado de arriba abajo: el toque es accesorio y jamás puede tumbar el juego.
        /// </summary>
        private int _touchFailures;

        private void PollTouch()
        {
            if (!_flipActive || _touchFailures >= 3)
                return;

            try
            {
                var released = SwitchTouch.PollTap(out var physX, out var physY);

                // Arrastre en curso (dedo abajo): actualizar fantasma y slot bajo el dedo.
                if (SwitchTouch.IsDown)
                {
                    if (AleksLayout.MapTouchPixels(_flipLayout, SwitchTouch.CurrentX, SwitchTouch.CurrentY, out var cx, out var cy, out _))
                    {
                        if (SecondScreen.DragIndex < 0 &&
                            AleksLayout.MapTouchPixels(_flipLayout, SwitchTouch.StartX, SwitchTouch.StartY, out var sx0, out var sy0, out var sIn) && sIn)
                        {
                            // Solo arranca un arrastre desde un objeto de la bolsa o un slot del rombo,
                            // y solo tras moverse un poco (si no, es un toque).
                            var hs = SecondScreen.HitTest(sx0, sy0);
                            var moved = System.Math.Abs(cx - sx0) + System.Math.Abs(cy - sy0) > 24;
                            if (moved && hs >= SecondScreen.HitHandSlot && GameManager != null)
                                SecondScreen.DragIndex = hs - SecondScreen.HitHandSlot;
                            else if (moved && hs >= SecondScreen.HitBagItem && hs < SecondScreen.HitHandSlot && GameManager != null &&
                                     GameManager.Equipment[hs - SecondScreen.HitBagItem] != null)
                                SecondScreen.DragIndex = hs - SecondScreen.HitBagItem;
                        }
                        SecondScreen.DragPanelX = cx;
                        SecondScreen.DragPanelY = cy;
                        var hh = SecondScreen.HitTest(cx, cy);
                        SecondScreen.DragHoverSlot = hh >= SecondScreen.HitHandSlot ? hh - SecondScreen.HitHandSlot : -1;
                    }
                    return;
                }

                if (!released)
                    return;

                var dragging = SecondScreen.DragIndex;
                var hoverSlot = SecondScreen.DragHoverSlot;
                SecondScreen.DragIndex = -1;
                SecondScreen.DragHoverSlot = -1;

                if (!AleksLayout.MapTouchPixels(_flipLayout, physX, physY, out var px, out var py, out var inside))
                    return;

                if (dragging >= 0)
                {
                    // Soltar sobre un slot del rombo: intercambiar. Fuera: cancelar.
                    if (hoverSlot >= 0)
                        EquipTo(dragging, hoverSlot);
                    SwitchDiag.Log("secondscreen.log", "drag " + dragging + " -> slot " + hoverSlot);
                    return;
                }

                var hit = inside ? SecondScreen.HitTest(px, py) : -1;
                var c = _flipLayout.Companion;
                SwitchDiag.Log("secondscreen.log",
                    "tap fisico " + physX + "," + physY +
                    " -> lienzo " + (c.X + px) + "," + (c.Y + py) +
                    " -> panel " + px + "," + py + (inside ? "" : " (fuera)") +
                    " hit=" + hit +
                    " [out " + (_flipLayout.FlipDst.X * 2 + _flipLayout.FlipDst.W) + "x" + (_flipLayout.FlipDst.Y * 2 + _flipLayout.FlipDst.H) +
                    " f=" + _flipLayout.FlipScaleX.ToString("0.00") + " panel@" + c.X + "," + c.Y + " " + c.W + "x" + c.H + "]");

                if (hit >= 0 && hit <= SecondScreenState.BarSettings)
                    SecondScreenState.Set(SecondScreenState.TabForBarSlot(hit));
                else if (hit >= SecondScreen.HitHandSlot)
                {
                    // Toque suelto en un slot del rombo: abre EQUIPO.
                    SecondScreenState.Set(SecondScreenState.Tab.Equipment);
                }
                else if (hit >= SecondScreen.HitBagItem)
                {
                    // Toque suelto en un objeto: al botón A, como en TMC.
                    var gm0 = GameManager;
                    if (gm0 != null)
                        EquipTo(hit - SecondScreen.HitBagItem, SecondScreen.EquipTargetSlot(gm0));
                }
                else if (hit >= SecondScreen.HitSettingsRow)
                    ApplySettingsRow((SecondScreen.SettingsRow)(hit - SecondScreen.HitSettingsRow));
            }
            catch (System.Exception e)
            {
                _touchFailures++;
                SwitchDiag.LogOnce("secondscreen.log", "touchfail",
                    "PollTouch fallo: " + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>
        /// Preset "AJUSTES OPTIMIZADOS": baja lo que se ha medido o razonado como caro en
        /// Tegra X1 (el cuello es ancho de banda de memoria). No toca nada que cambie el
        /// juego (niebla de descubrimiento, luces globales de cuevas y mazmorras).
        ///
        ///  - Desenfoque de UI OFF (medido: pasadas a pantalla completa con alpha).
        ///  - Límite 60 fps con timestep fijo (a 720 se alcanza; evita el tope de TimeMultiplier).
        ///  - HUD del juego oculto en FLIP (la barra lateral ya lo lleva): una capa menos.
        ///  - Sombras, luces de objetos, efectos de niebla decorativa y corrección de color OFF:
        ///    todas son pasadas extra por frame sobre el mundo.
        ///  - En apaisado, escala de render al 75 %. En FLIP, render a 720x540 (Render=720).
        /// </summary>
        private void ApplyOptimizedPreset()
        {
            SwitchSettings.NoUiBlur = 1;
            GameSettings.DisableUiBlur = true;
            SwitchSettings.FpsLimit = 60;
            SwitchSettings.HideHud = 1;
            SwitchSettings.RenderScale = 75;
            SwitchSettings.Render = 720;
            _appliedFlip = -1;
            _appliedRenderScale = -1;

            GameSettings.EnableShadows = false;
            GameSettings.ObjectLights = false;
            GameSettings.FogEffects = false;
            GameSettings.ColorCorrection = false;

            SwitchDiag.Log("secondscreen.log",
                "ajustes optimizados APLICADOS: blur off, 30 fps, hud oculto, render 75%, sombras/luces obj/niebla fx/color off");
        }

        // Los ajustes de GameSettings del preset se reaplican en cada arranque, después de que
        // el juego cargue los suyos (primer Update). Mientras optimized=1 el preset manda;
        // se deshace tocando la fila otra vez (con confirmación).
        private bool _optimizedApplied;

        private void ReapplyOptimizedOnBoot()
        {
            if (_optimizedApplied)
                return;
            _optimizedApplied = true;
            if (SwitchSettings.Optimized != 0)
                ApplyOptimizedPreset();
        }

        /// <summary>
        /// EQUIPO a lo TMC ALEKS: intercambia Equipment[from] con Equipment[toSlot]. Equipment es
        /// el array que el juego consulta al pulsar el botón (ObjLink.UseItem), así que el cambio
        /// es inmediato y se guarda con la partida. Igual que hace el propio inventario del
        /// juego, solo que por toque/arrastre.
        /// </summary>
        private void EquipTo(int from, int toSlot)
        {
            var gm = GameManager;
            if (gm == null || from < 0 || from >= gm.Equipment.Length || toSlot < 0 || toSlot >= gm.Equipment.Length || from == toSlot)
                return;
            var item = gm.Equipment[from];
            var prev = gm.Equipment[toSlot];
            if (item == null && prev == null)
                return;
            gm.Equipment[toSlot] = item;
            gm.Equipment[from] = prev;
            SwitchDiag.Log("secondscreen.log", "equipo: " + (item?.Name ?? "(vacio)") + " -> slot " + toSlot +
                (prev != null ? " (" + prev.Name + " a " + from + ")" : ""));
        }

        /// <summary>
        /// Una fila de AJUSTES tocada: cicla el valor, aplica y guarda. Misma lógica que los
        /// combos L+R+…, que siguen funcionando como atajo.
        /// </summary>
        private void ApplySettingsRow(SecondScreen.SettingsRow row)
        {
            switch (row)
            {
                case SecondScreen.SettingsRow.FpsLimit:
                    SwitchSettings.FpsLimit =
                        SwitchSettings.FpsLimit == 0  ? 30 :
                        SwitchSettings.FpsLimit == 30 ? 45 :
                        SwitchSettings.FpsLimit == 45 ? 60 : 0;
                    break;
                case SecondScreen.SettingsRow.ShowFps:
                    SwitchSettings.ShowFps = SwitchSettings.ShowFps != 0 ? 0 : 1;
                    break;
                case SecondScreen.SettingsRow.HideHud:
                    SwitchSettings.HideHud = SwitchSettings.HideHud != 0 ? 0 : 1;
                    break;
                case SecondScreen.SettingsRow.UiBlur:
                    SwitchSettings.NoUiBlur = SwitchSettings.NoUiBlur != 0 ? 0 : 1;
                    GameSettings.DisableUiBlur = SwitchSettings.NoUiBlur != 0;
                    break;
                case SecondScreen.SettingsRow.FlipGrip:
                    SwitchSettings.Flip = SwitchSettings.Flip != 0 ? 0 : 1;
                    break;
                case SecondScreen.SettingsRow.Render:
                    SwitchSettings.Render = SwitchSettings.Render == 720 ? 1080 : 720;
                    _appliedFlip = -1;          // que ApplyFlipMode recalcule la ranura y la escala
                    _appliedRenderScale = -1;   // y ApplyRenderScale en apaisado
                    break;
                case SecondScreen.SettingsRow.HudSize:
                    SwitchSettings.PanelHudLarge = SwitchSettings.PanelHudLarge != 0 ? 0 : 1;
                    break;
                case SecondScreen.SettingsRow.UiLarge:
                    SwitchSettings.UiLarge = SwitchSettings.UiLarge != 0 ? 0 : 1;
                    break;
                case SecondScreen.SettingsRow.Optimized:
                    if (!SecondScreen.OptimizedArmed)
                    {
                        // Primer toque: armar y pedir confirmación. No se guarda nada.
                        SecondScreen.OptimizedArmedUntilMs = System.Environment.TickCount64 + SecondScreen.OptimizedArmMs;
                        SwitchDiag.Log("secondscreen.log", "ajustes: optimizados -> pidiendo confirmacion");
                        return;
                    }
                    SecondScreen.OptimizedArmedUntilMs = 0;
                    if (SwitchSettings.Optimized != 0)
                    {
                        // Ya aplicados: el segundo toque confirmado los deshace (vuelven los
                        // efectos del juego; límite de fps y demás se quedan como estén).
                        SwitchSettings.Optimized = 0;
                        GameSettings.EnableShadows = true;
                        GameSettings.ObjectLights = true;
                        GameSettings.FogEffects = true;
                        GameSettings.ColorCorrection = true;
                        SwitchSettings.RenderScale = 100;
                        SwitchSettings.Render = 1080;
                        _appliedFlip = -1;
                        _appliedRenderScale = -1;
                        SwitchDiag.Log("secondscreen.log", "ajustes optimizados DESHECHOS");
                        break;
                    }
                    SwitchSettings.Optimized = 1;
                    ApplyOptimizedPreset();
                    break;
            }
            SwitchSettings.Save();
            SwitchDiag.Log("secondscreen.log", "ajustes: " + SecondScreen.SettingsLabel(row) + " -> " + SecondScreen.SettingsValue(row));
        }

        /// <summary>
        /// Layout de FLIP del frame actual, o Mode.Off si no estamos en FLIP.
        /// </summary>
        private AleksLayout.Layout _flipLayout;
        private bool _flipActive;

        /// <summary>
        /// Entra o sale del modo Flip Grip.
        ///
        /// Lo unico que hace falta para que el juego dibuje en vertical es cambiar el tamano
        /// de render a la ranura del juego (720x720 en portatil) y su escala de camara. La
        /// rotacion ocurre despues, al componer, y el juego no se entera.
        /// </summary>
        private void ApplyFlipMode()
        {
            var flip = SwitchSettings.Flip;
            var pp = Graphics?.GraphicsDevice?.PresentationParameters;
            if (pp == null || pp.BackBufferWidth <= 0)
                return;

            _flipActive = flip != 0;

            if (!_flipActive)
            {
                if (_appliedFlip != 0)
                {
                    _appliedFlip = 0;
                    _appliedFlipScale = -1;
                    _appliedRenderScale = -1;   // que ApplyRenderScale recupere lo suyo
                    ApplyRenderScale();
                    if (SwitchSettings.GameScale > 0)
                    {
                        GameSettings.GameScale = SwitchSettings.GameScale;
                        ScaleChanged = true;
                    }
                }
                return;
            }

            _flipLayout = AleksLayout.Compute(AleksLayout.Mode.Flip,
                pp.BackBufferWidth, pp.BackBufferHeight, 0, 100);

            var size = SwitchFlipCompositor.GameRenderSize(_flipLayout);
            if (_appliedFlip != 1 || RenderSizeOverride != new Point(size.X, size.Y))
            {
                _appliedFlip = 1;
                RenderSizeOverride = new Point(size.X, size.Y);
                SwitchDiag.Log("secondscreen.log",
                    "FLIP activo: ranura de juego " + size.X + "x" + size.Y);
            }

            // Escala de camara FORZADA en FLIP, cada frame. El juego la pisa por su cuenta:
            // al morir, al cargar partida y al crear una nueva vuelve a la escala guardada o
            // a la automatica (OverlayManager.ResetGameScale / SettingsSaveLoad), y con la
            // ranura cuadrada de 720x720 eso deja la camara enorme y descuadrada. Si en algun
            // frame no es la nuestra, se reimpone y se pide recalcular.
            var wanted = SwitchSettings.FlipGameScale;
            if (GameSettings.GameScale != wanted)
            {
                GameSettings.GameScale = wanted;
                ScaleChanged = true;
                if (_appliedFlipScale != wanted)
                {
                    _appliedFlipScale = wanted;
                    SwitchDiag.Log("secondscreen.log", "FLIP: escala de camara forzada a " + wanted);
                }
                else
                {
                    SwitchDiag.LogOnce("secondscreen.log", "flipscale-reimpuesta",
                        "FLIP: el juego cambio la escala; reimpuesta a " + wanted + " (se repetira en silencio)");
                }
            }
        }

        /// <summary>
        /// Aplica la escala de resolucion de render.
        ///
        /// Renderiza a menos pixeles y deja que el blit final del juego lo estire al panel,
        /// que es un camino que Game1 ya recorre para presentar. Ataca directamente el cuello
        /// medido en hardware: el ancho de banda de memoria (la RAM overclockeada aporta
        /// tanto como la GPU, y la CPU casi nada).
        ///
        /// Se redondea a par para que el estirado no caiga en medio pixel.
        /// </summary>
        private int _appliedRenderScale = -1;

        /// <summary>Puesto por SwitchVideoOptions al cambiar la resolución desde el menú del juego.</summary>
        public static bool SettingsChangedFromMenu;

        private void ApplyRenderScale()
        {
            // En FLIP manda ApplyFlipMode: el tamano de render es la ranura del juego.
            if (_flipActive)
                return;

            // Render=720 manda sobre renderscale: 1280x720 exactos (la pantalla portatil).
            var pct = SwitchSettings.Render == 720 ? 720 : SwitchSettings.RenderScale;
            if (pct == _appliedRenderScale)
                return;
            _appliedRenderScale = pct;

            if (pct >= 100 && pct != 720)
            {
                RenderSizeOverride = null;
                return;
            }

            var pp = Graphics?.GraphicsDevice?.PresentationParameters;
            if (pp == null || pp.BackBufferWidth <= 0 || pp.BackBufferHeight <= 0)
            {
                RenderSizeOverride = null;
                return;
            }

            int w, h;
            if (pct == 720)
            {
                if (pp.BackBufferHeight <= 720) { RenderSizeOverride = null; return; }
                w = pp.BackBufferWidth * 720 / pp.BackBufferHeight;
                h = 720;
            }
            else
            {
                w = pp.BackBufferWidth * pct / 100;
                h = pp.BackBufferHeight * pct / 100;
            }
            w -= w & 1;
            h -= h & 1;
            if (w < 320) w = 320;
            if (h < 240) h = 240;

            RenderSizeOverride = new Point(w, h);
            SwitchDiag.Log("secondscreen.log",
                "escala de render " + pct + "% -> " + w + "x" + h);
        }

        /// <summary>
        /// Aplica el limite de fps. Se comprueba cada frame porque Core llama a
        /// UpdateFpsSettings() (que pone IsFixedTimeStep = false) al arrancar y cada vez que
        /// se toca un ajuste de video, asi que nos lo desharia. Solo escribe si cambia.
        /// </summary>
        private void ApplyFpsLimit()
        {
            var limit = SwitchSettings.FpsLimit;
            if (limit == _appliedFpsLimit && IsFixedTimeStep == (limit > 0))
                return;

            _appliedFpsLimit = limit;
            if (limit > 0)
            {
                IsFixedTimeStep = true;
                TargetElapsedTime = System.TimeSpan.FromTicks(System.TimeSpan.TicksPerSecond / limit);
            }
            else
            {
                IsFixedTimeStep = false;
            }
        }

        // Refresco del mapa del panel.
        //
        // Core solo redibuja el render target del mapa mientras el menu de inventario esta
        // abierto (OverlayManager: "if (_currentMenuState == MenuState.Inventory)"), asi que
        // el panel mostraba una foto congelada de la ultima vez que abriste el mapa y las
        // zonas nuevas no aparecian.
        //
        // DrawRenderTarget() es publico, crea el RT si falta y termina el mismo con
        // SetRenderTarget(null) ("OpenGL needs this to avoid weird state bugs"), asi que se
        // puede llamar desde fuera sin dejar estado grafico sucio. Se hace ANTES de
        // base.Draw() para no estar dentro de ningun SpriteBatch activo.
        //
        // Cada 20 frames (~3 veces por segundo) sobra para un mapa y el coste es
        // despreciable frente a hacerlo cada frame.
        private const int MapRefreshFrames = 30;
        private int _mapRefreshCounter;
        private int _mapRefreshFailures;

        private void RefreshCompanionMap()
        {
            if (SwitchSettings.Flip == 0 || _mapRefreshFailures >= 3)
                return;

            if (++_mapRefreshCounter < MapRefreshFrames)
                return;
            _mapRefreshCounter = 0;

            try
            {
                var overlay = GameManager?.InGameOverlay;
                if (overlay == null || SpriteBatch == null || Graphics?.GraphicsDevice == null)
                    return;

                // PRECONDICIONES OBLIGATORIAS.
                //
                // En este port un null deref NO es una NullReferenceException capturable:
                // es un Data Abort que mata el proceso (misma leccion que el mkdir con
                // rutas sdmc:). El try/catch de abajo NO protege de esto, asi que hay que
                // comprobar a mano todo lo que Core desreferencia sin comprobar.
                //
                // MapOverlay.DrawMap usa Resources.SprMiniMap y GameManager.MapVisibility
                // sin ningun null check (Core si la comprueba en otros sitios, o sea que
                // sabe que puede ser null). Llamarlo en el titulo, en el file select o en
                // una transicion de mapa reventaba la consola.
                if (Resources.SprMiniMap == null || GameManager.MapVisibility == null)
                    return;

                var currentMap = GameManager.MapManager?.CurrentMap;
                if (currentMap == null)
                    return;

                if (SecondScreenState.Current == SecondScreenState.Tab.Equipment)
                {
                    // EQUIPO, MISION y AJUSTES los dibujamos nosotros: no hay RT que refrescar.
                }
                else if (SecondScreenState.Current == SecondScreenState.Tab.Dungeon)
                {
                    var dungeon = overlay.DungeonOverlayRef;
                    // DrawOnRenderTarget desreferencia CurrentMap sin comprobarlo.
                    if (dungeon == null || !currentMap.IsDungeon)
                        return;

                    // OJO: a diferencia de MapOverlay.DrawRenderTarget, este NO crea el RT
                    // ni restaura el render target al terminar. Hay que hacer ambas cosas.
                    // Si no estamos en mazmorra hace un early-return, asi que es inocuo.
                    dungeon.UpdateRenderTarget();
                    dungeon.DrawOnRenderTarget(SpriteBatch);
                    Graphics.GraphicsDevice.SetRenderTarget(null);
                }
                else
                {
                    overlay.MapOverlayRef?.DrawRenderTarget(SpriteBatch);
                }
            }
            catch (System.Exception e)
            {
                // Tres fallos y se deja de intentar: un panel accesorio no puede tirar el frame.
                _mapRefreshFailures++;
                SwitchDiag.LogOnce("secondscreen.log", "refreshfail",
                    "RefreshCompanionMap fallo: " + e.GetType().Name + ": " + e.Message);
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            SwitchFpsCounter.CountFrame();
            RefreshCompanionMap();

            base.Draw(gameTime);

            if (_flipActive)
            {
                // FLIP: recomponer el frame rotado sobre el lienzo vertical. Todo lo que se
                // dibuje a partir de aqui va en coordenadas del lienzo 720x1280 y la
                // transformacion se encarga de la rotacion.
                var pp = Graphics.GraphicsDevice.PresentationParameters;
                SwitchFlipCompositor.Compose(SpriteBatch, _flipLayout,
                    pp.BackBufferWidth, pp.BackBufferHeight);

                var transform = SwitchFlipCompositor.BuildTransform(_flipLayout,
                    pp.BackBufferWidth, pp.BackBufferHeight);
                SecondScreen.DrawInLayout(SpriteBatch, _flipLayout, transform);
                SwitchFpsCounter.Draw(SpriteBatch, transform);
            }
            else
            {
                // Apaisado: sin panel. El mapa se ve donde siempre, en el menu de pausa.
                PresentScaledFrame();
                SwitchFpsCounter.Draw(SpriteBatch, null);
            }

            // El benchmark manda el estado de los ajustes mientras corre.
            SwitchBenchmark.Tick(SwitchFpsCounter.LastFrameMs);
            SwitchBenchmark.Draw(SpriteBatch);
        }
    }
}
#endif
