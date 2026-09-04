namespace AorusControl.Core.Models;

public sealed record KeyboardRgbState(IReadOnlyList<KeyboardRgbZoneState> Zones)
{
    public bool IsEnabled => Zones.Any(zone => zone.Brightness > 0);

    /// <summary>
    /// The brightness level the zones currently hold. Zones are always written
    /// together, so the first zone is representative.
    /// </summary>
    public KeyboardBrightnessLevel Brightness =>
        KeyboardBrightnessLevels.FromRawValue(Zones.Count > 0 ? Zones[0].Brightness : (byte)0);

    public KeyboardRgbZoneState GetZone(int zone) =>
        Zones.Single(item => item.Zone == zone);
}
