using AorusControl.Core.Models;

namespace AorusControl.Core.Features.Keyboard;

/// <summary>Session-local cache of successfully sent zones, not a hardware readback.</summary>
public sealed class KeyboardFrameWriter(Action<int, KeyboardRgbColor, byte> writeZone)
{
    private readonly (KeyboardRgbColor Color, byte Brightness)?[] _sent = new (KeyboardRgbColor, byte)?[3];

    public int WriteFrame(IReadOnlyList<KeyboardRgbColor> colors, byte brightness, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if (colors.Count != 3) throw new ArgumentException("Genau drei RGB-Zonen erforderlich.", nameof(colors));
        if (!KeyboardBrightnessLevels.IsSupportedRawValue(brightness))
            throw new ArgumentOutOfRangeException(nameof(brightness));
        int writes = 0;
        for (int index = 0; index < 3; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var desired = (colors[index], brightness);
            if (_sent[index] == desired) continue;
            writeZone(index + 1, desired.Item1, brightness);
            _sent[index] = desired; // Failed writes must remain retryable.
            writes++;
        }
        return writes;
    }
}
