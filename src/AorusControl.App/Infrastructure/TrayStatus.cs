namespace AorusControl.App.Infrastructure;

/// <summary>
/// The line under the mouse pointer when it rests on the tray icon.
///
/// It used to read "Öffnen oder Beenden per Rechtsklick", which explains the menu to someone
/// who has already opened it once and says nothing at all afterwards. The two things worth
/// knowing without opening the window are which fan profile is in force and whether the
/// charge limit is on - both of which the app knows without asking the hardware anything, so
/// a hidden window still costs nothing.
///
/// Deliberately no temperature: the telemetry clock stops when the window is hidden, and a
/// tooltip that quotes a reading from an hour ago would be the one lie this app cannot tell.
/// </summary>
public static class TrayStatus
{
    public const string Name = "AORUS Control";

    /// <summary>Windows' own tooltip buffer. Anything longer is refused outright by the
    /// notification area, which is a poor way to find out.</summary>
    public const int MaxLength = 63;

    public static string Build(string? fanProfile, int? chargeLimitPercent, bool batteryKnown = true)
    {
        string[] parts =
        [
            Name,
            string.IsNullOrWhiteSpace(fanProfile) ? null! : Localization.Strings.Current.Format("Tray_Fans", fanProfile.Trim()),
            !batteryKnown ? null! : chargeLimitPercent is { } limit ? Localization.Strings.Current.Format("Tray_Limit", limit) : Localization.Strings.Current["Bat_StandardCharging"]
        ];

        string text = string.Join(" · ", parts.Where(part => !string.IsNullOrEmpty(part)));
        // Cut at the last separator that still fits rather than mid-word: half a label reads
        // like a bug, one label fewer reads like a decision.
        if (text.Length <= MaxLength) return text;
        int cut = text.LastIndexOf(" · ", MaxLength, StringComparison.Ordinal);
        return cut > 0 ? text[..cut] : text[..MaxLength];
    }
}
