using AorusControl.Core.Models;
using HidSharp;

namespace AorusControl.Core.Features.Keyboard;

/// <summary>Reads only the proven brightness notification collection, never key presses.</summary>
public sealed class KeyboardBrightnessNotifications
{
    public static bool TryParse(ReadOnlySpan<byte> report, out KeyboardBrightnessLevel level)
    {
        level = default;
        if (report.Length != 4 || report[0] != 0x04 || report[1] != 0x01 || report[3] != 0 ||
            !KeyboardBrightnessLevels.IsSupportedRawValue(report[2])) return false;
        level = (KeyboardBrightnessLevel)report[2];
        return true;
    }

    public Task RunAsync(Action<KeyboardBrightnessLevel> onBrightness, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onBrightness);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            HidDevice[] devices = DeviceList.Local.GetHidDevices(0x1044, 0x7A41).Where(IsAllowed).ToArray();
            if (devices.Length != 1) throw new InvalidOperationException("Genau eine freigegebene Helligkeits-Collection MI_02/COL_04 erforderlich.");
            using HidStream stream = devices[0].Open();
            stream.ReadTimeout = 1000;
            byte[] report = new byte[4];
            while (!cancellationToken.IsCancellationRequested)
            {
                int count;
                try { count = stream.Read(report, 0, report.Length); }
                catch (TimeoutException) { continue; }
                if (count == 0) throw new IOException("Helligkeits-Ereigniskanal geschlossen.");
                if (!cancellationToken.IsCancellationRequested && TryParse(report.AsSpan(0, count), out var level))
                    onBrightness(level);
            }
        }, CancellationToken.None);
    }

    private static bool IsAllowed(HidDevice device)
    {
        string path = device.DevicePath;
        if (!path.Contains("&mi_02", StringComparison.OrdinalIgnoreCase) ||
            !path.Contains("&col04", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("\\kbd", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            if (device.GetMaxInputReportLength() != 4 || device.GetMaxOutputReportLength() != 0 || device.GetMaxFeatureReportLength() != 0)
                return false;
            var descriptor = device.GetReportDescriptor();
            // Reject keyboard usage declarations even if the path happens to match.
            return !descriptor.DeviceItems.SelectMany(item => item.Usages.GetAllValues())
                .Any(usage => usage >> 16 == 7 || usage == 0x00010006) &&
                !descriptor.DeviceItems.SelectMany(item => item.Reports).SelectMany(report => report.DataItems)
                .SelectMany(item => item.Usages.GetAllValues()).Any(usage => usage >> 16 == 7);
        }
        catch { return false; } // Unreadable descriptor is not permission to listen.
    }
}
