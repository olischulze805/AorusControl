using AorusControl.Core.Models;

namespace AorusControl.Core.Features.Keyboard;

public interface IKeyboardLightingTransport
{
    KeyboardRgbState ReadState();
    KeyboardRgbState ApplyState(KeyboardRgbState state);
    Task PlayEffectAsync(KeyboardRgbEffect effect, KeyboardEffectSpeed speed, CancellationToken cancellationToken);
}
