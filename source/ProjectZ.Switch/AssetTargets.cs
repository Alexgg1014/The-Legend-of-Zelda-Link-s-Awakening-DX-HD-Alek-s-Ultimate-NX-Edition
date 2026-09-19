// Copia literal de upstream204/shared/AssetTargets.cs (bighead, v2.0.8). Es la tabla de
// ficheros derivados del patcher: cada clave es un fichero de v1.0.0 y sus valores los
// ficheros nuevos que se generan aplicando <nombre>.vcdiff sobre esa clave. Mantener
// sincronizada con upstream en cada rebase (bighead avisa de cambios de formato).

using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectZ.Shared
{
    public static class AssetTargets
    {
        // ------------------------------------------------------------------
        // Groups that are identical in both modes.
        // ------------------------------------------------------------------
        private static readonly string[] LangFiles  = { "chn.lng", "deu.lng", "esp.lng", "fre.lng", "ind.lng", "ita.lng", "por.lng", "pte.lng", "rus.lng", "swe.lng" };
        private static readonly string[] LangAchiev = { "achieve_chn.lng", "achieve_deu.lng", "achieve_eng.lng", "achieve_esp.lng", "achieve_fre.lng", "achieve_ind.lng", "achieve_ita.lng", "achieve_por.lng", "achieve_pte.lng", "achieve_rus.lng", "achieve_swe.lng" };
        private static readonly string[] LangDialog = { "dialog_chn.lng", "dialog_deu.lng", "dialog_esp.lng", "dialog_fre.lng", "dialog_ind.lng", "dialog_ita.lng", "dialog_por.lng", "dialog_pte.lng", "dialog_rus.lng", "dialog_swe.lng" };
        private static readonly string[] LinkImages = { "link1.png", "weapons.png" };
        private static readonly string[] LinkAtlas  = { "weapons.atlas" };
        private static readonly string[] NpcImages  = { "npcs_redux.png" };
        private static readonly string[] ItemImages = { "items_chn.png", "items_deu.png", "items_esp.png", "items_fre.png", "items_ind.png", "items_ita.png", "items_por.png", "items_rus.png", "items_swe.png", "items_redux.png",
                                                        "items_redux_chn.png", "items_redux_deu.png", "items_redux_esp.png", "items_redux_fre.png", "items_redux_ind.png", "items_redux_ita.png", "items_redux_por.png", "items_redux_rus.png", "items_redux_swe.png" };
        private static readonly string[] IntroImage = { "intro_chn.png", "intro_deu.png", "intro_esp.png", "intro_fre.png", "intro_ind.png", "intro_ita.png", "intro_por.png", "intro_rus.png", "intro_swe.png" };
        private static readonly string[] IntroAtlas = { "intro_chn.atlas" };
        private static readonly string[] MiniMapImg = { "minimap_chn.png", "minimap_deu.png", "minimap_esp.png", "minimap_fre.png", "minimap_ind.png", "minimap_ita.png", "minimap_por.png", "minimap_rus.png", "minimap_swe.png" };
        private static readonly string[] ObjectsImg = { "objects_chn.png", "objects_deu.png", "objects_esp.png", "objects_fre.png", "objects_ind.png", "objects_ita.png", "objects_por.png", "objects_rus.png", "objects_swe.png" };
        private static readonly string[] Photograph = { "photos_chn.png", "photos_deu.png", "photos_esp.png", "photos_fre.png", "photos_ind.png", "photos_ita.png", "photos_por.png", "photos_rus.png", "photos_swe.png", "photos_redux.png",
                                                        "photos_redux_chn.png", "photos_redux_deu.png", "photos_redux_esp.png", "photos_redux_fre.png", "photos_redux_ind.png", "photos_redux_ita.png", "photos_redux_por.png", "photos_redux_rus.png", "photos_redux_swe.png" };
        private static readonly string[] UiImages   = { "ui_chn.png", "ui_deu.png", "ui_esp.png", "ui_fre.png", "ui_ind.png", "ui_ita.png", "ui_por.png", "ui_rus.png", "ui_swe.png" };
        private static readonly string[] MusicTile  = { "musicOverworldClassic.data" };
        private static readonly string[] Dungeon3M  = { "dungeon3.map" };
        private static readonly string[] Dungeon3D  = { "dungeon3.map.data" };
        private static readonly string[] NewBridgeM = { "bridge_l_castle.map", "bridge_r_castle.map" };
        private static readonly string[] NewBridgeD = { "bridge_l_castle.map.data", "bridge_r_castle.map.data" };
        private static readonly string[] BowWowAnim = { "bowwow_water.ani" };
        private static readonly string[] DungeonAni = { "mapDungeon.ani", "mapManboPond.ani", "mapTeleporter.ani" };
        private static readonly string[] Boomerang  = { "boomerangOrig.ani" };
        private static readonly string[] EffectAni  = { "bushExplosion.ani", "fireballDeath.ani" };

        // ------------------------------------------------------------------
        // Groups that differ between modes. Only the extension changes here.
        // ------------------------------------------------------------------
        private static readonly string[] BackGround = { "menuBackgroundB", "menuBackgroundC", "sgb_border" };
        private static readonly string[] Lighting   = { "mamuLight" };
        private static readonly string[] ShaderFile = { "GBCColorCorrection", "PixelGrid" };

        // Fonts are NOT a straight extension swap: the XNB build also emits the
        // un-paged smallFont_chn / smallFont_chn_redux entries. Listed in full
        // so the difference stays visible instead of hiding in a helper.
        private static string[] SmallFonts(bool useXnb) => useXnb
            ? new[] { "smallFont_redux.xnb", "smallFont_vwf.xnb", "smallFont_vwf_redux.xnb", "smallFont_chn.xnb", "smallFont_chn_0.xnb", "smallFont_chn_redux.xnb", "smallFont_chn_redux_0.xnb" }
            : new[] { "smallFont_redux.png", "smallFont_vwf.png", "smallFont_vwf_redux.png", "smallFont_chn_0.png", "smallFont_chn_redux_0.png" };

        private static string[] WithExt(string[] names, string ext) => Array.ConvertAll(names, n => n + ext);

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------
        private static Dictionary<string, string[]> _raw;
        private static Dictionary<string, string[]> _xnb;

        public static IReadOnlyDictionary<string, string[]> Get(bool useXnb) =>
            useXnb ? (_xnb ??= Build(true)) : (_raw ??= Build(false));

        public static Dictionary<string, string[]> Build(bool useXnb)
        {
            string img = useXnb ? ".xnb" : ".png";
            string fx  = useXnb ? ".xnb" : ".fx";

            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "eng.lng",                 LangFiles.Concat(LangAchiev).ToArray() },
                { "dialog_eng.lng",          LangDialog },
                { "smallFont" + img,         SmallFonts(useXnb) },
                { "menuBackground" + img,    WithExt(BackGround, img) },
                { "ligth room" + img,        WithExt(Lighting, img) },
                { "link0.png",               LinkImages },
                { "link0.atlas",             LinkAtlas },
                { "npcs.png",                NpcImages },
                { "items.png",               ItemImages },
                { "intro.png",               IntroImage },
                { "intro.atlas",             IntroAtlas },
                { "minimap.png",             MiniMapImg },
                { "objects.png",             ObjectsImg },
                { "photos.png",              Photograph },
                { "ui.png",                  UiImages },
                { "musicOverworld.data",     MusicTile },
                { "dungeon3_1.map",          Dungeon3M },
                { "dungeon3_1.map.data",     Dungeon3D },
                { "bridge.map",              NewBridgeM },
                { "bridge.map.data",         NewBridgeD },
                { "BowWow.ani",              BowWowAnim },
                { "mapPlayer.ani",           DungeonAni },
                { "boomerang.ani",           Boomerang },
                { "explosion0.ani",          EffectAni },
                { "ShockEffect" + fx,        WithExt(ShaderFile, fx) }
            };
        }

        // ------------------------------------------------------------------
        // Reverse Map
        // ------------------------------------------------------------------
        private static Dictionary<string, string> _rawReverse;
        private static Dictionary<string, string> _xnbReverse;

        /// <summary>Derived name -> the master file it is created from. Read-only.</summary>
        public static IReadOnlyDictionary<string, string> GetReverse(bool useXnb) => useXnb
            ? (_xnbReverse ??= BuildReverse(true))
            : (_rawReverse ??= BuildReverse(false));

        private static Dictionary<string, string> BuildReverse(bool useXnb)
        {
            var reverse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in Get(useXnb))
                foreach (string longName in kvp.Value)
                    reverse[longName] = kvp.Key;
            return reverse;
        }

        // ------------------------------------------------------------------
        // Hash Set
        // ------------------------------------------------------------------
        private static HashSet<string> _rawNames;
        private static HashSet<string> _xnbNames;

        public static IReadOnlySet<string> GetTargetNames(bool useXnb) => useXnb
            ? (_xnbNames ??= Flatten(true))
            : (_rawNames ??= Flatten(false));

        private static HashSet<string> Flatten(bool useXnb) =>
            Get(useXnb).Values.SelectMany(v => v).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // ------------------------------------------------------------------
        // Dump Table: For scrapers.
        // ------------------------------------------------------------------
        public static string DumpTable(bool useXnb)
        {
            var table = Get(useXnb);

            foreach (var kvp in table)
            {
                if (kvp.Key.Contains('\t'))
                    throw new InvalidOperationException($"Source name contains a tab: '{kvp.Key}'");

                foreach (string derived in kvp.Value)
                    if (derived.Contains(' ') || derived.Contains('\t'))
                        throw new InvalidOperationException($"Derived name contains whitespace: '{derived}'");
            }

            return "# LADXHD derived-file map (" + (useXnb ? "compiled / .xnb" : "raw / uncompiled") + ")\n"
                 + "# Format: <source file><TAB><derived file> <derived file> ...\n"
                 + "# Source = a file present in the v1.0.0 release. Each derived name is\n"
                 + "# produced by applying the patch <derived name>.vcdiff to that source.\n"
                 + string.Join("\n", table.Select(kvp => kvp.Key + "\t" + string.Join(" ", kvp.Value)))
                 + "\n";
        }
    }
}
