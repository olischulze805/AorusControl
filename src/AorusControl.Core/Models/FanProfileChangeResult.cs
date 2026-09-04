namespace AorusControl.Core.Models;

public sealed record FanProfileChangeResult(
    FanControlState OriginalState,
    FanControlState VerifiedState);
