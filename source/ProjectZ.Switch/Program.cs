using System;
using System.IO;
using ProjectZ.InGame.Things;

namespace ProjectZ
{
    public static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var baseDir = AppContext.BaseDirectory;

            // Create portable.txt so SaveManager uses local save paths.
            var portableFile = Path.Combine(baseDir, "portable.txt");
            try
            {
                if (!File.Exists(portableFile))
                    File.WriteAllText(portableFile, "");
            }
            catch { }

            // Las carpetas de mods las crea Core en Game1.LoadContent (via
            // IUserDataPaths.ShouldCreateModsDirs). NO hacerlo aqui: estas llamadas corrian
            // antes de new Game1(), cuando Game1.UserDataPaths todavia no tiene nuestros
            // servicios registrados, asi que no creaban lo que decian crear.
            // Son seguras alli porque esas rutas derivan de AppContext.BaseDirectory
            // ("/switch/zelda-ladxhd/") y NO llevan el prefijo "sdmc:", que es lo unico que
            // hace estallar a mkdir en consola real.

            // Crash handlers + hang watchdog, as early as possible. Up to v2.0.4 this was a
            // #if SWITCH block inside Game1.LoadContent; Core no longer carries platform code.
            SwitchCrash.Install();
            SwitchCrash.SetSubsystem("Startup");

            // Ajustes propios del port (second screen). Antes de construir el juego.
            SwitchSettings.Load();

            using (var game = new SwitchGame(editorMode: false, loadSave: false, loadSlot: 0))
            {
                // v2.0.5+ replaced every platform #if with this service-based abstraction.
                SwitchPlatformServices.Register(game);
                game.Run();
            }
        }
    }
}
