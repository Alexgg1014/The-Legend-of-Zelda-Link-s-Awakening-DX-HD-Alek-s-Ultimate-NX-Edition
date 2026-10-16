#if SWITCH
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Things;

namespace ProjectZ
{
    /// <summary>
    /// Filas del port en Ajustes -> Video del propio juego (menú de pausa): resolución,
    /// límite de fps y Flip Grip. Mismos valores que la pestaña AJUSTES del panel; ambas
    /// leen y escriben SwitchSettings, así que siempre están de acuerdo.
    ///
    /// Las cadenas no están en los .lng del usuario: se inyectan en Language.Strings del
    /// idioma activo. Así no hay que tocar Data/. Las traducciones viven en SwitchStrings,
    /// junto a las del panel: eng/esp/por/fre/deu/ita, y el resto cae al inglés.
    /// </summary>
    internal sealed class SwitchVideoOptions : IPlatformVideoOptions
    {
        private static int _injectedLanguage = -1;

        public static void InjectStrings()
        {
            var lm = Game1.LanguageManager;
            if (lm == null || lm.Strings == null)
                return;
            if (_injectedLanguage == lm.CurrentLanguageIndex)
                return;
            _injectedLanguage = lm.CurrentLanguageIndex;

            var d = lm.Strings;
            d["switch_video_render"]      = SwitchStrings.Get("video_render");
            d["switch_video_fps"]         = SwitchStrings.Get("video_fps");
            d["switch_video_flip"]        = SwitchStrings.Get("video_flip");
            d["switch_video_hidehud"]     = SwitchStrings.Get("video_hidehud");
            d["switch_video_uilarge"]     = SwitchStrings.Get("video_uilarge");
            d["tooltip_switch_render"]    = SwitchStrings.Get("tip_render");
            d["tooltip_switch_fps"]       = SwitchStrings.Get("tip_fps");
            d["tooltip_switch_flip"]      = SwitchStrings.Get("tip_flip");
            d["tooltip_switch_hidehud"]   = SwitchStrings.Get("tip_hidehud");
            d["tooltip_switch_uilarge"]   = SwitchStrings.Get("tip_uilarge");
        }

        public void AddVideoOptions(InterfaceListLayout content, List<string> tooltips,
                                    int buttonWidth, int buttonHeight, int sliderHeight)
        {
            InjectStrings();

            // Resolución: 0 = 1080, 1 = 720
            var render = new InterfaceSlider("switch_video_render", buttonWidth, sliderHeight, new Point(1, 2),
                0, 1, 1, SwitchSettings.Render == 720 ? 1 : 0,
                n => { SwitchSettings.Render = n == 1 ? 720 : 1080; SwitchSettings.Save(); SwitchGame.SettingsChangedFromMenu = true; })
            { SetString = n => n == 1 ? " 720" : " 1080" };
            content.AddElement(render);
            tooltips.Add("tooltip_switch_render");

            // Límite de fps: 0 = sin límite, 1 = 30, 2 = 45, 3 = 60
            var fps = new InterfaceSlider("switch_video_fps", buttonWidth, sliderHeight, new Point(1, 2),
                0, 3, 1, FpsIndex(SwitchSettings.FpsLimit),
                n => { SwitchSettings.FpsLimit = n == 1 ? 30 : n == 2 ? 45 : n == 3 ? 60 : 0; SwitchSettings.Save(); })
            { SetString = n => n == 0 ? " --" : n == 1 ? " 30" : n == 2 ? " 45" : " 60" };
            content.AddElement(fps);
            tooltips.Add("tooltip_switch_fps");

            // Flip Grip
            content.AddElement(InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "switch_video_flip", SwitchSettings.Flip != 0,
                state => { SwitchSettings.Flip = state ? 1 : 0; SwitchSettings.Save(); }));
            tooltips.Add("tooltip_switch_flip");

            // HUD oculto en FLIP
            content.AddElement(InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "switch_video_hidehud", SwitchSettings.HideHud != 0,
                state => { SwitchSettings.HideHud = state ? 1 : 0; SwitchSettings.Save(); }));
            tooltips.Add("tooltip_switch_hidehud");

            // UI grande
            content.AddElement(InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "switch_video_uilarge", SwitchSettings.UiLarge != 0,
                state => { SwitchSettings.UiLarge = state ? 1 : 0; SwitchSettings.Save(); }));
            tooltips.Add("tooltip_switch_uilarge");
        }

        private static int FpsIndex(int limit) => limit == 30 ? 1 : limit == 45 ? 2 : limit == 60 ? 3 : 0;
    }
}
#endif
