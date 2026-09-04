using AorusControl.Core.Models;

namespace AorusControl.Core.Features.Keyboard;

public interface ILiveEffectBrightness
{
    // Queue a value for the running renderer AND its eventual restoration.
    // This is not a synchronous hardware readback.
    void UpdateEffectBrightness(KeyboardBrightnessLevel level);
}
