namespace AorusControl.Core.Features.PowerProfiles;

/// <summary>What the saver sets, and what that is worth on this machine.</summary>
/// <param name="Percent">The value written into the copied scheme.</param>
/// <param name="MeasuredWatts">What it saves here, from this project's own measurements -
/// null where nothing was measured. A lever with no number beside it is honest; a lever with
/// an invented one is not.</param>
public sealed record SaverLever(PowerSetting Setting, uint Percent, double? MeasuredWatts);

/// <summary>
/// The battery saver: what it changes, and why each part is in it.
///
/// Gigabyte's version capped the processor at 50 % and the screen at 30 % on battery, wrote
/// both into the user's own Balanced plan, and never touched the discrete graphics card. Our
/// own measurements say that of those two, only the screen does anything at idle - the panel
/// costs 5 W from minimum to maximum, while the cores draw 4 W of the 24,4 W this machine
/// uses doing nothing. The card, which Gigabyte left alone, is the biggest item of all.
///
/// So the numbers here are the same two, but they are not the whole of the feature: the
/// interface says what each one is worth, and the graphics page next door is where the
/// largest saving actually lives. See research/ULTRA-BATTERY-SAVER.md.
/// </summary>
public static class BatterySaverPlan
{
    public const string SchemeName = "AORUS Control · Akku sparen";
    public const string SchemeDescription =
        "Von AORUS Control angelegt. Wird beim Ausschalten des Sparmodus wieder verlassen; " +
        "der eigene Energiesparplan bleibt unverändert.";

    /// <summary>
    /// Below this the machine stops being usable, which does not save runtime - it moves work
    /// to later. Gigabyte's own floor was 50 %, and there is no reason to undercut it.
    /// </summary>
    public const uint LowestProcessorCap = 30;

    /// <summary>A screen too dark to read gets turned back up, so the saving lasts a minute.</summary>
    public const uint LowestBrightness = 10;

    public static IReadOnlyList<SaverLever> Levers(uint processorCap, uint brightness) =>
    [
        // Measured 2026-09-15: the panel costs about 5 W from minimum to maximum, so going
        // from full to 30 % is worth roughly 3.
        new(PowerSetting.DisplayBrightness, Clamp(brightness, LowestBrightness), 3.0),
        // Nothing at idle, where the cores are already at 4 W. Under load it is the larger
        // of the two - but this mode is for a machine that is mostly waiting.
        new(PowerSetting.MaximumProcessorState, Clamp(processorCap, LowestProcessorCap), null)
    ];

    public static uint Clamp(uint value, uint lowest) => Math.Clamp(value, lowest, 100);
}

/// <summary>What was found before the saver changed anything, so it can be handed back.</summary>
public sealed record SaverBaseline(Guid PreviousScheme);
