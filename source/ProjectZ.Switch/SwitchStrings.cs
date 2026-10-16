#if SWITCH
using System.Collections.Generic;

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// Textos del panel companion, por idioma del juego (Game1.LanguageManager.CurrentLanguageCode).
    /// Sin acentos a propósito: la GameFont del juego no trae todos los glifos acentuados en
    /// mayúsculas y el panel escribe en mayúsculas. Idiomas sin tabla caen al inglés.
    /// </summary>
    internal static class SwitchStrings
    {
        private static readonly Dictionary<string, Dictionary<string, string>> T = new Dictionary<string, Dictionary<string, string>>
        {
            ["eng"] = new Dictionary<string, string>
            {
                ["tab_quest"] = "QUEST", ["tab_map"] = "MAP", ["tab_dungeon"] = "DUNGEON", ["tab_equip"] = "ITEMS", ["tab_settings"] = "SETTINGS",
                ["fps_limit"] = "FPS LIMIT", ["fps_counter"] = "FPS COUNTER", ["game_hud"] = "GAME HUD", ["ui_blur"] = "UI BLUR",
                ["flip_grip"] = "FLIP GRIP", ["resolution"] = "RESOLUTION", ["panel_hud"] = "PANEL HUD", ["game_ui"] = "GAME UI",
                ["optimized"] = "OPTIMIZED SETTINGS",
                ["no_limit"] = "NO LIMIT", ["on"] = "ON", ["off"] = "OFF", ["hidden"] = "HIDDEN", ["visible"] = "VISIBLE",
                ["large"] = "LARGE", ["normal"] = "NORMAL", ["res_fast"] = "720 (60 FPS)", ["res_sharp"] = "1080 (SHARP)",
                ["apply"] = "APPLY", ["applied"] = "APPLIED", ["confirm"] = "SURE? TAP AGAIN", ["undo_confirm"] = "UNDO? TAP AGAIN",
                ["hint_settings"] = "TAP A ROW TO CHANGE IT",
                ["hint_equip"] = "TAP AN ITEM = TO {0}   DRAG IT TO A BUTTON TO CHOOSE",
                ["photos"] = "PHOTOS", ["trade"] = "TRADING SEQUENCE",
                // Filas del port en el menu Ajustes -> Video del juego (SwitchVideoOptions).
                ["video_render"] = "Switch resolution", ["video_fps"] = "FPS limit",
                ["video_flip"] = "Flip Grip (portrait)", ["video_hidehud"] = "Hide HUD in Flip Grip",
                ["video_uilarge"] = "Large UI (menus, dialogs)",
                ["tip_render"] = "720 = handheld screen, 60 fps. 1080 = sharper docked, 30 fps.",
                ["tip_fps"] = "Fixed timestep. 60 recommended with 720 resolution.",
                ["tip_flip"] = "Portrait console: game on top, companion panel below. Also L+R+X.",
                ["tip_hidehud"] = "The panel sidebar already shows hearts, items and rupees.",
                ["tip_uilarge"] = "One extra UI scale step at 720. The pause menu may overflow the sides slightly.",
            },
            ["esp"] = new Dictionary<string, string>
            {
                ["tab_quest"] = "MISION", ["tab_map"] = "MAPA", ["tab_dungeon"] = "MAZMORRA", ["tab_equip"] = "EQUIPO", ["tab_settings"] = "AJUSTES",
                ["fps_limit"] = "LIMITE DE FPS", ["fps_counter"] = "CONTADOR DE FPS", ["game_hud"] = "HUD DEL JUEGO", ["ui_blur"] = "DESENFOQUE UI",
                ["flip_grip"] = "FLIP GRIP", ["resolution"] = "RESOLUCION", ["panel_hud"] = "HUD DEL PANEL", ["game_ui"] = "UI DEL JUEGO",
                ["optimized"] = "AJUSTES OPTIMIZADOS",
                ["no_limit"] = "SIN LIMITE", ["on"] = "ON", ["off"] = "OFF", ["hidden"] = "OCULTO", ["visible"] = "VISIBLE",
                ["large"] = "GRANDE", ["normal"] = "NORMAL", ["res_fast"] = "720 (60 FPS)", ["res_sharp"] = "1080 (NITIDO)",
                ["apply"] = "APLICAR", ["applied"] = "APLICADOS", ["confirm"] = "SEGURO? TOCA OTRA VEZ", ["undo_confirm"] = "DESHACER? TOCA OTRA VEZ",
                ["hint_settings"] = "TOCA UNA FILA PARA CAMBIARLA",
                ["hint_equip"] = "TOCA UN OBJETO = A {0}   ARRASTRALO A UN BOTON PARA ELEGIR",
                ["photos"] = "FOTOS", ["trade"] = "INTERCAMBIO",
                ["video_render"] = "Resolucion Switch", ["video_fps"] = "Limite de FPS",
                ["video_flip"] = "Flip Grip (vertical)", ["video_hidehud"] = "Ocultar HUD en Flip Grip",
                ["video_uilarge"] = "UI grande (menus, dialogos)",
                ["tip_render"] = "720 = pantalla portatil, 60 fps. 1080 = mas nitido en dock, 30 fps.",
                ["tip_fps"] = "Timestep fijo. 60 recomendado con resolucion 720.",
                ["tip_flip"] = "Consola en vertical: juego arriba, panel companion abajo. Tambien L+R+X.",
                ["tip_hidehud"] = "La barra lateral del panel ya lleva corazones, objetos y rupias.",
                ["tip_uilarge"] = "Un paso mas de escala de UI a 720. El menu de pausa puede salirse un poco por los lados.",
            },
            ["por"] = new Dictionary<string, string>
            {
                ["tab_quest"] = "MISSAO", ["tab_map"] = "MAPA", ["tab_dungeon"] = "MASMORRA", ["tab_equip"] = "ITENS", ["tab_settings"] = "AJUSTES",
                ["fps_limit"] = "LIMITE DE FPS", ["fps_counter"] = "CONTADOR DE FPS", ["game_hud"] = "HUD DO JOGO", ["ui_blur"] = "DESFOQUE UI",
                ["flip_grip"] = "FLIP GRIP", ["resolution"] = "RESOLUCAO", ["panel_hud"] = "HUD DO PAINEL", ["game_ui"] = "UI DO JOGO",
                ["optimized"] = "AJUSTES OTIMIZADOS",
                ["no_limit"] = "SEM LIMITE", ["on"] = "ON", ["off"] = "OFF", ["hidden"] = "OCULTO", ["visible"] = "VISIVEL",
                ["large"] = "GRANDE", ["normal"] = "NORMAL", ["res_fast"] = "720 (60 FPS)", ["res_sharp"] = "1080 (NITIDO)",
                ["apply"] = "APLICAR", ["applied"] = "APLICADOS", ["confirm"] = "CERTEZA? TOQUE DE NOVO", ["undo_confirm"] = "DESFAZER? TOQUE DE NOVO",
                ["hint_settings"] = "TOQUE NUMA LINHA PARA MUDAR",
                ["hint_equip"] = "TOQUE NUM ITEM = PARA {0}   ARRASTE ATE UM BOTAO PARA ESCOLHER",
                ["photos"] = "FOTOS", ["trade"] = "TROCAS",
                ["video_render"] = "Resolucao Switch", ["video_fps"] = "Limite de FPS",
                ["video_flip"] = "Flip Grip (vertical)", ["video_hidehud"] = "Ocultar HUD no Flip Grip",
                ["video_uilarge"] = "UI grande (menus, dialogos)",
                ["tip_render"] = "720 = tela portatil, 60 fps. 1080 = mais nitido na dock, 30 fps.",
                ["tip_fps"] = "Timestep fixo. 60 recomendado com resolucao 720.",
                ["tip_flip"] = "Console na vertical: jogo em cima, painel companion embaixo. Tambem L+R+X.",
                ["tip_hidehud"] = "A barra lateral do painel ja mostra coracoes, itens e rubis.",
                ["tip_uilarge"] = "Um passo a mais de escala de UI a 720. O menu de pausa pode passar um pouco das bordas.",
            },
            ["fre"] = new Dictionary<string, string>
            {
                ["tab_quest"] = "QUETE", ["tab_map"] = "CARTE", ["tab_dungeon"] = "DONJON", ["tab_equip"] = "OBJETS", ["tab_settings"] = "OPTIONS",
                ["fps_limit"] = "LIMITE FPS", ["fps_counter"] = "COMPTEUR FPS", ["game_hud"] = "HUD DU JEU", ["ui_blur"] = "FLOU UI",
                ["flip_grip"] = "FLIP GRIP", ["resolution"] = "RESOLUTION", ["panel_hud"] = "HUD DU PANNEAU", ["game_ui"] = "UI DU JEU",
                ["optimized"] = "REGLAGES OPTIMISES",
                ["no_limit"] = "SANS LIMITE", ["on"] = "ON", ["off"] = "OFF", ["hidden"] = "CACHE", ["visible"] = "VISIBLE",
                ["large"] = "GRAND", ["normal"] = "NORMAL", ["res_fast"] = "720 (60 FPS)", ["res_sharp"] = "1080 (NET)",
                ["apply"] = "APPLIQUER", ["applied"] = "APPLIQUES", ["confirm"] = "SUR? TOUCHEZ ENCORE", ["undo_confirm"] = "ANNULER? TOUCHEZ ENCORE",
                ["hint_settings"] = "TOUCHEZ UNE LIGNE POUR LA CHANGER",
                ["hint_equip"] = "TOUCHEZ UN OBJET = SUR {0}   GLISSEZ-LE SUR UN BOUTON",
                ["photos"] = "PHOTOS", ["trade"] = "ECHANGES",
                ["video_render"] = "Resolution Switch", ["video_fps"] = "Limite de FPS",
                ["video_flip"] = "Flip Grip (portrait)", ["video_hidehud"] = "Masquer le HUD en Flip Grip",
                ["video_uilarge"] = "Grande UI (menus, dialogues)",
                ["tip_render"] = "720 = ecran portable, 60 fps. 1080 = plus net en dock, 30 fps.",
                ["tip_fps"] = "Pas de temps fixe. 60 recommande avec la resolution 720.",
                ["tip_flip"] = "Console a la verticale : jeu en haut, panneau companion en bas. Aussi L+R+X.",
                ["tip_hidehud"] = "La barre laterale du panneau affiche deja coeurs, objets et rubis.",
                ["tip_uilarge"] = "Un cran de plus d'echelle d'UI en 720. Le menu pause peut deborder un peu sur les cotes.",
            },
            ["deu"] = new Dictionary<string, string>
            {
                ["tab_quest"] = "QUEST", ["tab_map"] = "KARTE", ["tab_dungeon"] = "DUNGEON", ["tab_equip"] = "ITEMS", ["tab_settings"] = "OPTIONEN",
                ["fps_limit"] = "FPS-LIMIT", ["fps_counter"] = "FPS-ZAEHLER", ["game_hud"] = "SPIEL-HUD", ["ui_blur"] = "UI-UNSCHAERFE",
                ["flip_grip"] = "FLIP GRIP", ["resolution"] = "AUFLOESUNG", ["panel_hud"] = "PANEL-HUD", ["game_ui"] = "SPIEL-UI",
                ["optimized"] = "OPTIMIERTE EINSTELLUNGEN",
                ["no_limit"] = "KEIN LIMIT", ["on"] = "AN", ["off"] = "AUS", ["hidden"] = "VERSTECKT", ["visible"] = "SICHTBAR",
                ["large"] = "GROSS", ["normal"] = "NORMAL", ["res_fast"] = "720 (60 FPS)", ["res_sharp"] = "1080 (SCHARF)",
                ["apply"] = "ANWENDEN", ["applied"] = "ANGEWENDET", ["confirm"] = "SICHER? NOCHMAL TIPPEN", ["undo_confirm"] = "RUECKGAENGIG? NOCHMAL TIPPEN",
                ["hint_settings"] = "TIPPE EINE ZEILE ZUM AENDERN",
                ["hint_equip"] = "ITEM TIPPEN = AUF {0}   AUF EINEN KNOPF ZIEHEN ZUM WAEHLEN",
                ["photos"] = "FOTOS", ["trade"] = "TAUSCHKETTE",
                ["video_render"] = "Switch-Aufloesung", ["video_fps"] = "FPS-Limit",
                ["video_flip"] = "Flip Grip (Hochformat)", ["video_hidehud"] = "HUD im Flip Grip ausblenden",
                ["video_uilarge"] = "Grosse UI (Menues, Dialoge)",
                ["tip_render"] = "720 = Handheld-Bildschirm, 60 fps. 1080 = schaerfer im Dock, 30 fps.",
                ["tip_fps"] = "Fester Zeitschritt. 60 empfohlen mit Aufloesung 720.",
                ["tip_flip"] = "Konsole hochkant: Spiel oben, Companion-Panel unten. Auch L+R+X.",
                ["tip_hidehud"] = "Die Seitenleiste des Panels zeigt bereits Herzen, Items und Rubine.",
                ["tip_uilarge"] = "Eine UI-Stufe groesser bei 720. Das Pausenmenue kann seitlich leicht ueberstehen.",
            },
            ["ita"] = new Dictionary<string, string>
            {
                ["tab_quest"] = "MISSIONE", ["tab_map"] = "MAPPA", ["tab_dungeon"] = "DUNGEON", ["tab_equip"] = "OGGETTI", ["tab_settings"] = "OPZIONI",
                ["fps_limit"] = "LIMITE FPS", ["fps_counter"] = "CONTATORE FPS", ["game_hud"] = "HUD DI GIOCO", ["ui_blur"] = "SFOCATURA UI",
                ["flip_grip"] = "FLIP GRIP", ["resolution"] = "RISOLUZIONE", ["panel_hud"] = "HUD DEL PANNELLO", ["game_ui"] = "UI DI GIOCO",
                ["optimized"] = "IMPOSTAZIONI OTTIMIZZATE",
                ["no_limit"] = "SENZA LIMITE", ["on"] = "ON", ["off"] = "OFF", ["hidden"] = "NASCOSTO", ["visible"] = "VISIBILE",
                ["large"] = "GRANDE", ["normal"] = "NORMALE", ["res_fast"] = "720 (60 FPS)", ["res_sharp"] = "1080 (NITIDO)",
                ["apply"] = "APPLICA", ["applied"] = "APPLICATE", ["confirm"] = "SICURO? TOCCA ANCORA", ["undo_confirm"] = "ANNULLARE? TOCCA ANCORA",
                ["hint_settings"] = "TOCCA UNA RIGA PER CAMBIARLA",
                ["hint_equip"] = "TOCCA UN OGGETTO = SU {0}   TRASCINALO SU UN TASTO PER SCEGLIERE",
                ["photos"] = "FOTO", ["trade"] = "SCAMBI",
                ["video_render"] = "Risoluzione Switch", ["video_fps"] = "Limite di FPS",
                ["video_flip"] = "Flip Grip (verticale)", ["video_hidehud"] = "Nascondi HUD in Flip Grip",
                ["video_uilarge"] = "UI grande (menu, dialoghi)",
                ["tip_render"] = "720 = schermo portatile, 60 fps. 1080 = piu nitido in dock, 30 fps.",
                ["tip_fps"] = "Timestep fisso. 60 consigliato con risoluzione 720.",
                ["tip_flip"] = "Console in verticale: gioco sopra, pannello companion sotto. Anche L+R+X.",
                ["tip_hidehud"] = "La barra laterale del pannello mostra gia cuori, oggetti e rupie.",
                ["tip_uilarge"] = "Un passo in piu di scala UI a 720. Il menu di pausa puo sporgere un poco ai lati.",
            },
        };

        public static string Get(string key)
        {
            var code = Game1.LanguageManager?.CurrentLanguageCode ?? "eng";
            if (T.TryGetValue(code, out var d) && d.TryGetValue(key, out var v))
                return v;
            return T["eng"].TryGetValue(key, out var e) ? e : key;
        }

        public static string Get(string key, string arg0) => Get(key).Replace("{0}", arg0);
    }
}
#endif
