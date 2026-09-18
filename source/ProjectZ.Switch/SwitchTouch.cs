#if SWITCH
using System.Runtime.InteropServices;

namespace ProjectZ
{
    /// <summary>
    /// Puente táctil del panel.
    ///
    /// POR QUÉ NO SDL. MonoGame solo se entera de la entrada por los eventos que ve en su
    /// propio bucle de SDL_PollEvent, y este SDL ya nos dejó tirados una vez: no entregaba
    /// SDL_CONTROLLERDEVICEADDED, y hubo que inyectar un JOYDEVICEADDED sintético para que
    /// los mandos existieran. Apostar otra vez por que sí entrega SDL_FINGER* sería repetir
    /// el mismo error, así que esto lee HID de libnx directamente — igual que se acabaron
    /// leyendo los mandos. El wrapper nativo registra igualmente cualquier evento de dedo
    /// que SDL sí entregue, de modo que si resulta que funciona lo sabremos por el log y
    /// podremos simplificar sobre evidencia, no sobre intuición.
    ///
    /// EL RELEASE, NO EL PRESS. La acción se dispara al LEVANTAR el dedo, que es el
    /// contrato de los donantes: así un arrastre fuera del control lo cancela, en vez de
    /// dispararse en cuanto se roza la pantalla.
    /// </summary>
    internal static class SwitchTouch
    {
        /// <summary>Puntos de contacto activos; x/y en píxeles FÍSICOS del panel
        /// (0..1279 x 0..719), sin rotar por FLIP.</summary>
        [DllImport("*", EntryPoint = "SwitchGetTouch")]
        private static extern int SwitchGetTouch(out int x, out int y);

        private static bool _wasDown;
        private static int _lastX, _lastY;
        private static int _startX, _startY;

        /// <summary>True mientras hay un dedo en la pantalla (tras el último PollTap).</summary>
        public static bool IsDown { get; private set; }
        /// <summary>Posición física donde empezó el gesto actual (o el último).</summary>
        public static int StartX => _startX;
        public static int StartY => _startY;
        /// <summary>Última posición física conocida del dedo.</summary>
        public static int CurrentX => _lastX;
        public static int CurrentY => _lastY;

        /// <summary>
        /// Llamar UNA vez por frame. Devuelve true solo en el frame en que se levanta el
        /// dedo, con la última posición conocida en coordenadas físicas.
        /// </summary>
        public static bool PollTap(out int physX, out int physY)
        {
            var down = SwitchGetTouch(out var x, out var y) > 0;
            if (down)
            {
                if (!_wasDown)
                {
                    _startX = x;
                    _startY = y;
                }
                _lastX = x;
                _lastY = y;
            }
            IsDown = down;

            physX = _lastX;
            physY = _lastY;

            var released = _wasDown && !down;
            _wasDown = down;
            return released;
        }
    }
}
#endif
