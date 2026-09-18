#if SWITCH
using System;

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// Ajustes propios del port Switch.
    ///
    /// Viven aquí y no en GameSettings a propósito: desde v2.0.5 ProjectZ.Core es idéntico
    /// a upstream y queremos que siga siéndolo. El spike v2.0.4 añadía
    /// GameSettings.SecondScreen + su persistencia en SettingsSaveLoad; eso eran dos parches
    /// más a Core que ya no hacen falta.
    ///
    /// Persistencia: un fichero "switchsettings.txt" de líneas clave=valor en la raíz del
    /// juego. NO usar APIs de ficheros de C# con rutas "sdmc:" (ver la nota larga en
    /// SwitchCrash); aquí la ruta sale de AppContext.BaseDirectory ("/switch/zelda-ladxhd/",
    /// sin prefijo), que sí es segura.
    /// </summary>
    public static class SwitchSettings
    {
        private const string FileName = "switchsettings.txt";
        private const string LegacyFileName = "secondscreen.txt";

        /// <summary>0 = apagado, 1 = panel companion en la barra derecha.</summary>
        public static int SecondScreen;

        /// <summary>0 = oculto, 1 = contador de FPS en pantalla.</summary>
        public static int ShowFps;

        /// <summary>
        /// Escala de cámara a forzar al arrancar. 0 = no tocar (respeta lo que tengas puesto
        /// en Ajustes → Vídeo).
        ///
        /// Importa para el RENDIMIENTO, no solo para verlo más grande: a menos escala se ve
        /// más mundo, o sea más tiles, más objetos y un quad de niebla mayor. A escala 3 se
        /// dibuja del orden de 4 veces más superficie que a escala 6. Medido en hardware el
        /// cuello es de GPU, así que subir la escala ES una optimización.
        /// </summary>
        public static int GameScale = 6;

        /// <summary>Multiplicador de tamaño del contador de FPS.</summary>
        public static int FpsScale = 3;

        /// <summary>
        /// Límite de fps: 0 = sin límite (solo vsync), o 30 / 45 / 60.
        ///
        /// Un 30 estable suele SENTIRSE mejor que un 45 que fluctúa, porque el ritmo de
        /// frames es constante. Se implementa con IsFixedTimeStep + TargetElapsedTime, no
        /// durmiendo a mano, y eso además evita un problema real del juego: Game1 calcula
        /// TimeMultiplier = elapsed/166667 y lo TOPA en 2.0, o sea a 30 fps. Por debajo de
        /// 30 el juego se va a cámara lenta de verdad. Con timestep fijo el delta es
        /// constante y MonoGame hace updates de recuperación, así que no se llega al tope.
        /// </summary>
        public static int FpsLimit = 60;

        /// <summary>
        /// 1 = desenfoque de UI APAGADO (el ajuste OpaqueHudBg del juego). Por defecto 1.
        ///
        /// Medido por el usuario en consola: con el desenfoque activo caen los fps de forma
        /// clara. Son pasadas a pantalla completa con alpha blending cada frame, y en Tegra
        /// X1 el cuello es de ancho de banda de memoria — justo lo que eso satura.
        /// </summary>
        public static int NoUiBlur = 1;

        /// <summary>
        /// Porcentaje de resolucion de render, 25..100. 100 = nativo.
        ///
        /// Ataca directamente el cuello real: renderizar a menos pixeles baja el ancho de
        /// banda proporcionalmente. El blit final estira al panel, que es un camino que el
        /// juego ya recorre para presentar.
        /// </summary>
        public static int RenderScale = 100;

        /// <summary>
        /// 1 = modo Flip Grip: lienzo vertical 720x1280 rotado 270, juego arriba y companion
        /// abajo, como una DS. 0 = apaisado normal.
        ///
        /// Además de ser el modo "de verdad" del second screen (nada tapa a nada), es más
        /// rápido: la ranura del juego son 720x720 = 518.400 píxeles frente a los 921.600 de
        /// 1280x720. Un 44% menos de relleno, y el cuello medido es ancho de banda.
        /// </summary>
        public static int Flip;

        /// <summary>
        /// Escala de cámara dentro del modo FLIP. Tiene que ser PROPIA y distinta de la de
        /// apaisado (6): la ranura es cuadrada, no 16:9. A 720x720 la escala 4 da 180x180 de
        /// mundo visible, por encima de los 160x128 de Game Boy. Entera a propósito: con
        /// filtrado nearest una escala fraccionaria produce bandeo (lección del donante TMC).
        /// </summary>
        /// Derivada de la resolución de render de FLIP para que el mundo visible sea el mismo
        /// (~15-17 tiles de ancho): 4 a 1080 px (270 px de mundo), 3 a 720 px (240 px).
        /// El juego reaplica su GameScale guardada al morir o cargar ("zoom bug"); SwitchGame
        /// la reimpone cada frame.
        public static int FlipGameScale => Render == 720 ? 3 : 4;

        /// <summary>
        /// Resolución de RENDER del juego, en FLIP y en apaisado: 1080 (lo que da el backbuffer,
        /// 1920x1080 / 1080x810) o 720 (1280x720 / 720x540, el tamaño real de la pantalla
        /// portátil). Medido el 17-sep-2026: a 1080 la GPU clava 30 fps; a 720 va a 60 incluso
        /// con niebla. Por defecto 720: Flip Grip y portátil son 720p, los 1080 no se ven.
        /// </summary>
        public static int Render = 720;

        /// <summary>Tamaño del HUD del panel (barra lateral): 0 = normal, 1 = grande.</summary>
        public static int PanelHudLarge = 0;

        /// <summary>
        /// 1 = UI del juego (menús, diálogos, HUD) un paso más grande: el juego redondea su
        /// escala de UI hacia abajo y a 720x540 se quedaba en x1. Con esto pasa a x2 (el menú
        /// de pausa se sale ~5% por cada lado; asumido). Por defecto 1.
        /// </summary>
        public static int UiLarge = 1;
        /// <summary>1 = ocultar el HUD del juego mientras el panel FLIP esta visible (la barra lateral ya lo lleva).</summary>
        public static int HideHud = 1;

        /// <summary>
        /// 1 = el usuario aplicó "AJUSTES OPTIMIZADOS" desde el panel. Se reaplica en cada
        /// arranque DESPUÉS de que el juego cargue sus ajustes, porque varios viven en
        /// GameSettings (sombras, luces de objetos, niebla, corrección de color) y el juego
        /// los guarda cuando le parece.
        /// </summary>
        public static int Optimized;

        private static bool _loaded;

        private static string PathFor(string name) =>
            System.IO.Path.Combine(AppContext.BaseDirectory, name);

        /// <summary>
        /// Lee los ajustes del disco. Idempotente y nunca lanza: si algo falla, todo apagado.
        /// </summary>
        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                var path = PathFor(FileName);
                if (System.IO.File.Exists(path))
                {
                    foreach (var rawLine in System.IO.File.ReadAllLines(path))
                    {
                        var line = rawLine.Trim();
                        if (line.Length == 0 || line[0] == '#')
                            continue;

                        var eq = line.IndexOf('=');
                        if (eq <= 0)
                            continue;

                        var key = line.Substring(0, eq).Trim().ToLowerInvariant();
                        if (!int.TryParse(line.Substring(eq + 1).Trim(), out var value))
                            continue;

                        if (key == "secondscreen")   SecondScreen = value != 0 ? 1 : 0;
                        else if (key == "showfps")   ShowFps      = value != 0 ? 1 : 0;
                        else if (key == "gamescale") GameScale    = value < 0 ? 0 : (value > 20 ? 20 : value);
                        else if (key == "fpsscale")  FpsScale     = value < 1 ? 1 : (value > 8 ? 8 : value);
                        else if (key == "fpslimit")  FpsLimit     = (value == 30 || value == 45 || value == 60) ? value : 0;
                        else if (key == "nouiblur")    NoUiBlur    = value != 0 ? 1 : 0;
                        else if (key == "renderscale") RenderScale = value < 25 ? 25 : (value > 100 ? 100 : value);
                        else if (key == "flip")          Flip          = value != 0 ? 1 : 0;
                        else if (key == "flipgamescale") { /* fija a 4 desde el 17-sep-2026; se ignora */ }
                        else if (key == "flipgap")       AleksLayout.FlipGap = value < 0 ? 0 : (value > 128 ? 128 : value);
                        else if (key == "hidehud")       HideHud       = value != 0 ? 1 : 0;
                        else if (key == "optimized")     Optimized     = value != 0 ? 1 : 0;
                        else if (key == "fliprender")    Render        = value == 720 ? 720 : 1080;   // clave antigua
                        else if (key == "render")        Render        = value == 720 ? 720 : 1080;
                        else if (key == "panelhudlarge") PanelHudLarge = value != 0 ? 1 : 0;
                        else if (key == "uilarge")       UiLarge       = value != 0 ? 1 : 0;
                    }
                    return;
                }

                // Compatibilidad con el fichero de la primera build de la fase 1.
                var legacy = PathFor(LegacyFileName);
                if (System.IO.File.Exists(legacy) &&
                    int.TryParse(System.IO.File.ReadAllText(legacy).Trim(), out var legacyValue))
                {
                    SecondScreen = legacyValue != 0 ? 1 : 0;
                }
            }
            catch
            {
                // Unos ajustes opcionales jamás deben impedir arrancar.
                SecondScreen = 0;
                ShowFps = 0;
            }
        }

        /// <summary>
        /// Guarda los ajustes. Nunca lanza: si no se puede escribir, los toggles siguen
        /// funcionando en esta sesión y simplemente no se recuerdan.
        /// </summary>
        public static void Save()
        {
            try
            {
                System.IO.File.WriteAllText(PathFor(FileName),
                    "# Ajustes del port Switch de LADXHD\n" +
                    "#\n" +
                    "# secondscreen: panel lateral    (L + R + stick derecho)\n" +
                    "# showfps:      contador de FPS  (L + R + stick izquierdo)\n" +
                    "# fpslimit:     0 sin limite, o 30 / 45 / 60  (L + R + Y)\n" +
                    "# fpsscale:     tamano del contador de FPS (1-8)\n" +
                    "#\n" +
                    "# --- rendimiento (el cuello en Tegra X1 es ancho de banda de memoria) ---\n" +
                    "# gamescale:    escala de camara al arrancar (0 = no tocar). SUBIRLA MEJORA\n" +
                    "#               EL RENDIMIENTO: a menos escala se ve mas mundo, o sea mas\n" +
                    "#               tiles, objetos y niebla que dibujar.\n" +
                    "# nouiblur:     1 = desenfoque de UI APAGADO. Medido en consola: activarlo\n" +
                    "#               cuesta fps de forma clara.\n" +
                    "# renderscale:  25-100, porcentaje de resolucion de render. Bajarlo sube\n" +
                    "#               fps de forma proporcional.\n" +
                    "secondscreen=" + SecondScreen + "\n" +
                    "showfps=" + ShowFps + "\n" +
                    "fpslimit=" + FpsLimit + "\n" +
                    "fpsscale=" + FpsScale + "\n" +
                    "gamescale=" + GameScale + "\n" +
                    "nouiblur=" + NoUiBlur + "\n" +
                    "renderscale=" + RenderScale + "\n" +
                    "#\n" +
                    "# --- Flip Grip: consola en vertical ---   (L + R + X)\n" +
                    "# flip:          1 = lienzo vertical 720x1280, juego arriba y companion\n" +
                    "#                abajo, como una DS. Ademas es MAS RAPIDO: la ranura del\n" +
                    "#                juego son 720x720 frente a 1280x720, un 44% menos.\n" +
                    "# (flipgamescale ya no existe: en FLIP la escala de camara es FIJA a 4)\n" +
                    "# flipgap:       hueco entre juego y companion (20 da reparto exacto).\n" +
                    "# hidehud:       1 = ocultar el HUD del juego en FLIP (la barra lateral del\n" +
                    "#                panel ya lleva corazones, objetos, rupias y llaves).\n" +
                    "flip=" + Flip + "\n" +
                    "flipgap=" + AleksLayout.FlipGap + "\n" +
                    "hidehud=" + HideHud + "\n" +
                    "# optimized:     1 = preset 'ajustes optimizados' aplicado desde el panel\n" +
                    "optimized=" + Optimized + "\n" +
                    "# render:        1080 o 720 = resolucion de render del juego, FLIP y apaisado (720 = 60 fps)\n" +
                    "render=" + Render + "\n" +
                    "# panelhudlarge: 1 = HUD del panel (corazones, objetos, rupias) grande\n" +
                    "panelhudlarge=" + PanelHudLarge + "\n" +
                    "# uilarge:       1 = UI del juego (menus, dialogos) un paso mas grande a 720\n" +
                    "uilarge=" + UiLarge + "\n");
            }
            catch { }
        }
    }
}
#endif
