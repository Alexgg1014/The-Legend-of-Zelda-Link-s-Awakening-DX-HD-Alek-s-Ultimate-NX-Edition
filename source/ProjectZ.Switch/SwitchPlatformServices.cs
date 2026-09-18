using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ
{
    /// <summary>
    /// Switch implementations of the v2.0.5+ platform abstraction.
    ///
    /// Up to v2.0.4 the Switch port carried ~10 <c>#if SWITCH</c> blocks patched directly into
    /// ProjectZ.Core. v2.0.5 removed every platform compilation constant and replaced them with
    /// the interfaces in ProjectZ.Core/PlatformServices.cs, so the whole platform layer now lives
    /// here and Core stays pristine.
    ///
    /// Baseline: on v2.0.4 the Switch build compiled with the DESKTOPGL/LINUX profile, so it took
    /// every <c>#if !ANDROID</c> path. The values below reproduce that known-good desktop profile,
    /// except where a comment states a deliberate deviation.
    /// </summary>
    internal sealed class SwitchDisplayConfiguration : IPlatformDisplayConfiguration
    {
        public SwitchDisplayConfiguration()
        {
            // Switch panels are exactly 1280x720 handheld and 1920x1080 docked. Trust the reported
            // display mode when it is sane, otherwise fall back to 720p.
            var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            if (mode != null && mode.Width >= 1280 && mode.Height >= 720)
            {
                PreferredBackBufferWidth = mode.Width;
                PreferredBackBufferHeight = mode.Height;
            }
            else
            {
                PreferredBackBufferWidth = 1280;
                PreferredBackBufferHeight = 720;
            }
        }

        public int PreferredBackBufferWidth { get; }
        public int PreferredBackBufferHeight { get; }
    }

    /// <summary>
    /// Exclusive fullscreen must stay off: SDL2 already fills the panel natively on Switch, and
    /// forcing a mode switch caused zooming plus a crash during the v2.0.4 spike.
    /// </summary>
    internal sealed class SwitchPlatformWindow : IPlatformWindow
    {
        public bool SupportsFullscreen => false;
        public bool SupportsFullscreenConfiguration => false;

        // The Switch has no window focus concept; always process input.
        public bool SupportsInactiveWindowInput => true;
        public bool ForceFullscreen => false;

        // Matches the DesktopGL profile the v2.0.4 spike ran under.
        public bool VerticalFlipBlur => true;

        public void Initialize(Game game) { }
        public void OnGraphicsDeviceReset(Game game) { }
        public void ApplyPendingChanges(Game game) { }

        // Never hand a mode switch to SDL; the panel is already native size.
        public bool TrySetFullscreen(Game game, int screenMode) => false;

        public Rectangle GetDesktopBounds()
        {
            var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            return mode != null
                ? new Rectangle(0, 0, mode.Width, mode.Height)
                : new Rectangle(0, 0, 1280, 720);
        }

        public bool TryCenterWindow(Game game) => false;
        public void Exit(Game game) => game.Exit();
    }

    /// <summary>
    /// Portable layout rooted at the NRO directory on the SD card, mirroring LocalUserDataPaths
    /// in portable mode.
    ///
    /// Every path here derives from AppContext.BaseDirectory, which on Switch resolves to
    /// "/switch/zelda-ladxhd/" WITHOUT an "sdmc:" prefix. That distinction matters: managed file
    /// APIs are safe on plain absolute paths, and fatal on "sdmc:"-prefixed ones (.NET treats
    /// those as relative, prepends the cwd, and newlib then dereferences a null devoptab).
    /// So letting Core create the mods directories is safe.
    /// </summary>
    internal sealed class SwitchUserDataPaths : IUserDataPaths
    {
        public SwitchUserDataPaths()
        {
            UserDataRoot = AppContext.BaseDirectory;
            ModsRoot = Path.Combine(UserDataRoot, "Mods");
        }

        /// <summary>
        /// v2.0.8: la carga de mapas en hilo pasa a ser un ajuste del juego (ThreadMapLoad) con
        /// valor inicial por plataforma. En Switch false: los recursos GL se crean en el hilo GL
        /// (misma razon que CanCreateGraphicsResourcesOnWorkerThread = false).
        /// </summary>
        public bool DefaultMapThreadLoad => false;
        public bool ShouldCreateModsDirs => true;
        public string UserDataRoot { get; }
        public string ModsRoot { get; }
        public string InternalModsRoot => null;
        public string SaveDirectory => Path.Combine(UserDataRoot, "SaveFiles");
        public string SettingsFilePath => Path.Combine(UserDataRoot, "settings");
        public string AdvancedFilePath => Path.Combine(UserDataRoot, "advanced");
        public string AchievementsFilePath => Path.Combine(UserDataRoot, "achievements");
    }

    internal static class SwitchPlatformServices
    {
        /// <summary>
        /// Registers the Switch platform layer. Call before Game1.Run().
        /// </summary>
        public static void Register(Game game)
        {
            game.Services.AddService(typeof(IPlatformDisplayConfiguration), new SwitchDisplayConfiguration());
            game.Services.AddService(typeof(IPlatformFileSystem), new LocalPlatformFileSystem());
            game.Services.AddService(typeof(IUserDataPaths), new SwitchUserDataPaths());
            game.Services.AddService(typeof(ISharedSaveService), new UnavailableSharedSaveService());

            // Controllers reach the game through MonoGame's GamePad, fed by the SDL_JOYDEVICEADDED
            // event the native shim injects; no separate platform input source is needed.
            game.Services.AddService(typeof(IPlatformInput), new NullPlatformInput());
            game.Services.AddService(typeof(ITextInputService), new NullTextInputService());
            game.Services.AddService(typeof(IPlatformWindow), new SwitchPlatformWindow());

            game.Services.AddService(typeof(IGraphicsCapabilities), new GraphicsCapabilities(
                // DesktopGL profile: size comes from the viewport, as in the v2.0.4 spike.
                usePresentationParametersForSize: false,

                // DELIBERATE DEVIATION from the v2.0.4 spike (which loaded on a worker thread and
                // inherited "true" from the desktop profile). The prime suspect for the hang inside
                // Draw is map objects being loaded on a secondary thread, which creates GL resources
                // off the GL thread. Android sets this to false for exactly that reason. Loading
                // synchronously costs a longer initial load and removes the race.
                canCreateGraphicsResourcesOnWorkerThread: false,

                supportsBlendFunctionMax: true,

                // Medido en hardware: el juego va limitado por GPU (subir CPU de 1020 a 1500
                // MHz solo da ~2 fps; subir GPU a 460 MHz lo lleva cerca de 60). El filtrado
                // anisotropico en el sampler del mapa (MapManager.cs:267) es caro en un
                // Tegra X1 y no aporta nada a un juego de pixel art. Android lo pone en false
                // por este mismo motivo.
                useAnisotropicFiltering: false));

            game.Services.AddService(typeof(IPlatformPresentation), new PlatformPresentation(
                minimumHeight: 256,
                useCompactMenus: false,
                useFullWindowHud: false,
                defaultSequenceScaleAmplify: 0));

            game.Services.AddService(typeof(IFileDialogService), new UnavailableFileDialogService());
            game.Services.AddService(typeof(IPlatformVideoOptions), new SwitchVideoOptions());
        }
    }
}
