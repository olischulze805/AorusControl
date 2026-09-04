namespace AorusControl.Core.Models;

public sealed record BatteryChargeState(byte PolicyRaw, byte StoredStopPercent)
{
    public bool IsStandardMode => PolicyRaw == 0;

    public bool IsCustomMode => PolicyRaw == 4;

    public int? EffectiveCustomLimitPercent => IsCustomMode ? StoredStopPercent : null;
}
