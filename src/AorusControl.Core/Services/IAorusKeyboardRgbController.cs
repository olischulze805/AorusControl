using AorusControl.Core.Models;

namespace AorusControl.Core.Services;

public interface IAorusKeyboardRgbController : IDisposable, AorusControl.Core.Features.Keyboard.IKeyboardLightingTransport
{
    KeyboardRgbState SetLighting(bool enabled);

    KeyboardRgbState SetBrightness(KeyboardBrightnessLevel level);

    KeyboardRgbState SetColor(int zone, KeyboardRgbColor color, bool applyToAllZones);

}
