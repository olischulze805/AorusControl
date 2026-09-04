namespace AorusControl.Core.Models;

public sealed record BatteryChargeChangeResult(
    BatteryChargeState OriginalState,
    BatteryChargeState VerifiedState);
