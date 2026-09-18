#if SWITCH
using ProjectZ.InGame.Map;

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// Estado del panel companion — equivalente de port_second_screen_state.c del TMC ALEKS.
    ///
    /// Guarda la pestaña activa y decide el auto-cambio. La regla es la del donante: al entrar
    /// en una mazmorra el panel salta solo a MAZMORRA, y al salir vuelve a MAPA — salvo que el
    /// usuario haya elegido pestaña a mano, en cuyo caso se respeta su elección ("pin") hasta
    /// que cambie de contexto otra vez.
    ///
    /// La barra de pestañas tiene TRES botones: MISIÓN · MAPA/MAZMORRA · EQUIPO. MAPA y
    /// MAZMORRA comparten botón (el rótulo cambia con el contexto), igual que en TMC.
    /// </summary>
    public static class SecondScreenState
    {
        public enum Tab
        {
            Map = 0,
            Dungeon = 1,
            Equipment = 2,
            Quest = 3,
            Settings = 4,
        }

        /// <summary>Botones de la barra, en orden de izquierda a derecha.</summary>
        public const int BarSlots = 3;      // botones con rótulo
        public const int BarQuest = 0;
        public const int BarMap = 1;
        public const int BarEquipment = 2;
        public const int BarSettings = 3;   // el engranaje; la cruceta también llega a él

        public static Tab Current { get; private set; } = Tab.Map;

        /// <summary>El usuario eligió pestaña a mano: no auto-cambiar hasta cambiar de contexto.</summary>
        private static bool _pinned;

        /// <summary>Último contexto conocido, para detectar el flanco entrada/salida de mazmorra.</summary>
        private static bool _wasInDungeon;

        /// <summary>True si Link está actualmente dentro de una mazmorra.</summary>
        public static bool InDungeon
        {
            get
            {
                try
                {
                    return MapManager.ObjLink?.Map != null && MapManager.ObjLink.Map.IsDungeon;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Llamar una vez por frame. Aplica el auto-cambio al cruzar la frontera mazmorra/exterior.
        /// </summary>
        public static void Update()
        {
            var inDungeon = InDungeon;

            if (inDungeon != _wasInDungeon)
            {
                // Cambio de contexto: el auto-cambio manda otra vez y se suelta el pin.
                _wasInDungeon = inDungeon;
                _pinned = false;
                Current = inDungeon ? Tab.Dungeon : Tab.Map;
                return;
            }

            // Sin pin, la pestaña sigue al contexto. Con pin en el botón de mapa, el rótulo
            // sigue al contexto igualmente (MAPA fuera, MAZMORRA dentro).
            if (!_pinned || Current == Tab.Map || Current == Tab.Dungeon)
                Current = inDungeon ? Tab.Dungeon : Tab.Map;
        }

        /// <summary>Botón de la barra que corresponde a la pestaña activa.</summary>
        public static int CurrentBarSlot =>
            Current == Tab.Quest ? BarQuest :
            Current == Tab.Equipment ? BarEquipment :
            Current == Tab.Settings ? BarSettings : BarMap;

        /// <summary>Pestaña que activa un botón de la barra, según el contexto actual.</summary>
        public static Tab TabForBarSlot(int slot)
        {
            if (slot == BarQuest) return Tab.Quest;
            if (slot == BarEquipment) return Tab.Equipment;
            if (slot == BarSettings) return Tab.Settings;
            return InDungeon ? Tab.Dungeon : Tab.Map;
        }

        /// <summary>Cambio manual de pestaña (cruceta). Fija el pin hasta el próximo cambio de contexto.</summary>
        public static void Cycle(int direction)
        {
            var next = (CurrentBarSlot + direction) % (BarSlots + 1);
            if (next < 0)
                next += BarSlots + 1;
            Set(TabForBarSlot(next));
        }

        /// <summary>Selección directa (toque en un botón de la barra). Fija el pin.</summary>
        public static void Set(Tab tab)
        {
            Current = tab;
            _pinned = true;
        }

        /// <summary>Rótulo del botón de mapa según el contexto.</summary>
        public static string MapBarLabel => SwitchStrings.Get(InDungeon ? "tab_dungeon" : "tab_map");

        /// <summary>Nombre corto de la pestaña activa (logs).</summary>
        public static string CurrentTitle =>
            Current == Tab.Dungeon   ? "MAZMORRA" :
            Current == Tab.Equipment ? "EQUIPO" :
            Current == Tab.Quest     ? "MISION" :
            Current == Tab.Settings  ? "AJUSTES" : "MAPA";
    }
}
#endif
