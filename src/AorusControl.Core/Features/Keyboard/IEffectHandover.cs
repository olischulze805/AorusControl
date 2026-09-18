namespace AorusControl.Core.Features.Keyboard;

/// <summary>
/// A renderer that can be told to leave the lighting where it is when its effect stops.
///
/// Stopping an effect normally writes back the state from before it started. Inside a session
/// that is right and invisible: a mode change stops the effect, gets a known base, and writes
/// its new state on top of it in the same breath.
///
/// On shutdown there is no such write. The restore is then the last thing that reaches the
/// keyboard, and what it restores is whatever happened to be set before the effect began -
/// often the firmware's own colours from the last boot. The keyboard goes dark or changes
/// colour the moment the app closes, which reads as the app taking its lighting with it.
/// </summary>
public interface IEffectHandover
{
    /// <summary>From here on, stopping the running effect leaves the last rendered frame
    /// standing instead of putting back what the renderer found.</summary>
    void KeepLightingOnStop();
}
