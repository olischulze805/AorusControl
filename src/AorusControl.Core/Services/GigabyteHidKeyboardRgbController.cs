using System.Text.RegularExpressions;
using System.Diagnostics;
using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Models;
using HidSharp;

namespace AorusControl.Core.Services;

public sealed class GigabyteHidKeyboardRgbController : IAorusKeyboardRgbController, AorusControl.Core.Features.Keyboard.IKeyboardLightingTransport, ILiveEffectBrightness
{
    private const int VendorId = 0x1044;
    private const int ProductId = 0x7A41;
    private const int FeatureLength = 9;
    private const byte QueryCommand = 0x88;
    private const byte WriteCommand = 0x08;
    private const int CommandDelayMilliseconds = 65;
    private const int EffectWriteDelayMilliseconds = 5;
    private static readonly TimeSpan EffectFrameInterval = Features.Keyboard.KeyboardEffectFrames.FrameInterval;
    private readonly object _sync = new();
    private int _effectBrightness = -1;

    public void UpdateEffectBrightness(KeyboardBrightnessLevel level)
    {
        if (!KeyboardBrightnessLevels.All.Contains(level)) throw new ArgumentOutOfRangeException(nameof(level));
        Volatile.Write(ref _effectBrightness, (byte)level);
    }

    public KeyboardRgbState ReadState()
    {
        lock (_sync)
        {
            using HidStream stream = OpenExactDevice();
            return ReadState(stream);
        }
    }

    public KeyboardRgbState SetLighting(bool enabled) =>
        SetBrightness(enabled ? KeyboardBrightnessLevel.High : KeyboardBrightnessLevel.Off);

    public KeyboardRgbState ApplyState(KeyboardRgbState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        KeyboardRgbZoneState[] desired = state.Zones.OrderBy(zone => zone.Zone).ToArray();
        if (desired.Length != 3 || !desired.Select(zone => zone.Zone).SequenceEqual(new[] { 1, 2, 3 }) ||
            desired.Any(zone => !KeyboardBrightnessLevels.IsSupportedRawValue(zone.Brightness)))
            throw new ArgumentException("Genau drei Zonen mit geprüften Helligkeitswerten erforderlich.", nameof(state));
        lock (_sync)
        {
            using HidStream stream = OpenExactDevice();
            return WriteAndVerify(stream, ReadState(stream), desired);
        }
    }

    public KeyboardRgbState SetBrightness(KeyboardBrightnessLevel level)
    {
        if (!KeyboardBrightnessLevels.All.Contains(level))
        {
            throw new ArgumentOutOfRangeException(
                nameof(level),
                level,
                "Nur die vier geprüften Helligkeitsstufen sind zugelassen.");
        }

        lock (_sync)
        {
            using HidStream stream = OpenExactDevice();
            KeyboardRgbState original = ReadState(stream);
            KeyboardRgbZoneState[] desired = original.Zones
                .Select(zone => zone with { Brightness = (byte)level })
                .ToArray();
            return WriteAndVerify(stream, original, desired);
        }
    }

    public KeyboardRgbState SetColor(int zone, KeyboardRgbColor color, bool applyToAllZones)
    {
        if (zone is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(zone), "Die RGB-Zone muss zwischen 1 und 3 liegen.");
        }

        lock (_sync)
        {
            using HidStream stream = OpenExactDevice();
            KeyboardRgbState original = ReadState(stream);
            KeyboardRgbZoneState[] desired = original.Zones
                .Select(item => applyToAllZones || item.Zone == zone
                    ? item with { Color = color }
                    : item)
                .ToArray();
            return WriteAndVerify(stream, original, desired);
        }
    }

    public Task PlayEffectAsync(
        KeyboardRgbEffect effect,
        KeyboardEffectSpeed speed,
        CancellationToken cancellationToken)
    {
        Volatile.Write(ref _effectBrightness, -1);
        return Task.Run(() => PlayEffect(effect, speed, cancellationToken), CancellationToken.None);
    }

    public void Dispose()
    {
    }

    private static HidStream OpenExactDevice()
    {
        HidDevice[] matches = DeviceList.Local
            .GetHidDevices(VendorId, ProductId)
            .Where(device =>
                GetInterfaceLabel(device.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) &&
                device.GetMaxFeatureReportLength() == FeatureLength)
            .ToArray();

        if (matches.Length != 1)
        {
            throw new AorusKeyboardRgbException(
                matches.Length == 0
                    ? "Die geprüfte AORUS-RGB-Schnittstelle 1044:7A41/MI_03 wurde nicht gefunden."
                    : "Die AORUS-RGB-Schnittstelle ist mehrfach vorhanden; aus Sicherheitsgründen wurde nichts geschrieben.");
        }

        try
        {
            return matches[0].Open();
        }
        catch (Exception exception)
        {
            throw new AorusKeyboardRgbException("Die AORUS-RGB-Schnittstelle konnte nicht geöffnet werden.", exception);
        }
    }

    private static KeyboardRgbState ReadState(HidStream stream)
    {
        var zones = new List<KeyboardRgbZoneState>(3);
        for (byte zone = 1; zone <= 3; zone++)
        {
            byte[] response = QueryZone(stream, zone);
            if (response[1] != QueryCommand || response[2] != zone)
            {
                throw new AorusKeyboardRgbException($"Die Antwort für RGB-Zone {zone} war ungültig.");
            }

            zones.Add(new KeyboardRgbZoneState(
                zone,
                new KeyboardRgbColor(response[3], response[4], response[5]),
                response[6]));
        }

        return new KeyboardRgbState(zones);
    }

    private void PlayEffect(
        KeyboardRgbEffect effect,
        KeyboardEffectSpeed speed,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            using HidStream stream = OpenExactDevice();
            KeyboardRgbState original = ReadState(stream);
            Exception? renderFailure = null;

            try
            {
                // Render at the brightness the user already chose instead of forcing
                // full brightness, now that all four steps are known to be settable.
                byte brightness = original.Brightness == KeyboardBrightnessLevel.Off
                    ? (byte)KeyboardBrightnessLevel.High
                    : (byte)original.Brightness;

                // The animation is host-rendered, so speed is simply a time scale.
                double timeScale = speed.ToTimeScale();
                var clock = Stopwatch.StartNew();
                var writer = new KeyboardFrameWriter((zone, color, level) =>
                {
                    WriteZoneFast(stream, checked((byte)zone), color, level);
                    Thread.Sleep(EffectWriteDelayMilliseconds);
                });
                while (!cancellationToken.IsCancellationRequested)
                {
                    TimeSpan frameStart = clock.Elapsed;
                    KeyboardRgbColor[] frame = Features.Keyboard.KeyboardEffectFrames.Create(
                        effect,
                        clock.Elapsed.TotalSeconds * timeScale,
                        original.GetZone(1).Color);
                    int liveBrightness = Volatile.Read(ref _effectBrightness);
                    writer.WriteFrame(frame, liveBrightness < 0 ? brightness : (byte)liveBrightness, cancellationToken);
                    // No catch-up bursts after a slow HID call, and no busy spin when
                    // an effect holds identical colors. Stop remains interruptible.
                    TimeSpan remaining = EffectFrameInterval - (clock.Elapsed - frameStart);
                    if (remaining > TimeSpan.Zero) cancellationToken.WaitHandle.WaitOne(remaining);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal stop still restores the original state below.
            }
            catch (Exception exception)
            {
                renderFailure = exception;
            }

            try
            {
                int liveBrightness = Volatile.Read(ref _effectBrightness);
                KeyboardRgbState restore = liveBrightness < 0 ? original : new(original.Zones
                    .Select(zone => zone with { Brightness = (byte)liveBrightness }).ToArray());
                RestoreAndVerify(stream, restore);
            }
            catch (Exception restoreException)
            {
                throw new AorusKeyboardRgbException(
                    $"Der RGB-Effekt wurde beendet, aber der vorherige Zustand konnte nicht sicher wiederhergestellt werden: {restoreException.Message}",
                    renderFailure ?? restoreException);
            }

            if (renderFailure is not null)
            {
                throw new AorusKeyboardRgbException(
                    $"Der RGB-Effekt wurde wegen eines Gerätefehlers beendet; der vorherige Zustand wurde wiederhergestellt: {renderFailure.Message}",
                    renderFailure);
            }
        }
    }

    private static void WriteZoneFast(
        HidStream stream,
        byte zone,
        KeyboardRgbColor color,
        byte brightness)
    {
        byte[] request = new byte[FeatureLength];
        request[1] = WriteCommand;
        request[2] = zone;
        request[3] = color.Red;
        request[4] = color.Green;
        request[5] = color.Blue;
        request[6] = brightness;
        request[8] = CalculateChecksum(request);
        stream.SetFeature(request);
    }

    private static void RestoreAndVerify(HidStream stream, KeyboardRgbState original)
    {
        foreach (KeyboardRgbZoneState zone in original.Zones)
        {
            WriteZone(stream, zone);
        }

        KeyboardRgbState restored = ReadState(stream);
        if (!original.Zones.SequenceEqual(restored.Zones))
        {
            throw new AorusKeyboardRgbException("Die Wiederherstellung nach dem Effekt wurde nicht vollständig bestätigt.");
        }
    }

    private static KeyboardRgbState WriteAndVerify(
        HidStream stream,
        KeyboardRgbState original,
        IReadOnlyList<KeyboardRgbZoneState> desired)
    {
        try
        {
            foreach (KeyboardRgbZoneState zone in desired)
            {
                KeyboardRgbZoneState before = original.GetZone(zone.Zone);
                if (zone != before)
                {
                    WriteZone(stream, zone);
                }
            }

            KeyboardRgbState readback = ReadState(stream);
            if (!desired.SequenceEqual(readback.Zones))
            {
                throw new AorusKeyboardRgbException("Die Tastatur hat die RGB-Einstellung nicht vollständig bestätigt.");
            }

            return readback;
        }
        catch (Exception exception)
        {
            try
            {
                foreach (KeyboardRgbZoneState zone in original.Zones)
                {
                    WriteZone(stream, zone);
                }

                KeyboardRgbState restored = ReadState(stream);
                if (!original.Zones.SequenceEqual(restored.Zones))
                {
                    throw new AorusKeyboardRgbException("Auch die automatische Wiederherstellung konnte nicht bestätigt werden.");
                }
            }
            catch (Exception restoreException)
            {
                throw new AorusKeyboardRgbException(
                    $"RGB-Änderung fehlgeschlagen und der vorherige Zustand konnte nicht sicher wiederhergestellt werden: {restoreException.Message}",
                    exception);
            }

            throw new AorusKeyboardRgbException(
                $"RGB-Änderung fehlgeschlagen; der vorherige Zustand wurde wiederhergestellt: {exception.Message}",
                exception);
        }
    }

    private static byte[] QueryZone(HidStream stream, byte zone)
    {
        byte[] request = new byte[FeatureLength];
        request[1] = QueryCommand;
        request[2] = zone;
        request[8] = CalculateChecksum(request);
        stream.SetFeature(request);
        Thread.Sleep(CommandDelayMilliseconds);

        byte[] response = new byte[FeatureLength];
        stream.GetFeature(response);
        return response;
    }

    private static void WriteZone(HidStream stream, KeyboardRgbZoneState zone)
    {
        byte[] request = new byte[FeatureLength];
        request[1] = WriteCommand;
        request[2] = checked((byte)zone.Zone);
        request[3] = zone.Color.Red;
        request[4] = zone.Color.Green;
        request[5] = zone.Color.Blue;
        request[6] = zone.Brightness;
        request[8] = CalculateChecksum(request);
        stream.SetFeature(request);
        Thread.Sleep(CommandDelayMilliseconds);
    }

    private static byte CalculateChecksum(ReadOnlySpan<byte> packet)
    {
        int sum = 0;
        for (int index = 1; index <= 7; index++)
        {
            sum += packet[index];
        }

        return unchecked((byte)(255 - sum));
    }

    private static string GetInterfaceLabel(string devicePath)
    {
        Match match = Regex.Match(
            devicePath,
            @"&mi_(?<interface>[0-9a-f]{2})(?:&col(?<collection>[0-9a-f]{2}))?",
            RegexOptions.IgnoreCase);
        return match.Success
            ? $"MI_{match.Groups["interface"].Value.ToUpperInvariant()}"
            : "HID collection";
    }
}
