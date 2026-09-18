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
