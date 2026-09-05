namespace AorusControl.App.Infrastructure;

/// <summary>
/// One attachable feature of the app: it reads its own device state on start and releases
/// the hardware on dispose. The shell only knows this contract, so adding a feature means
/// adding a class and a section - not editing a god object.
/// </summary>
public interface IFeatureModule : IDisposable
{
    /// <summary>Reads the current device state. Must not throw: a feature whose hardware is
    /// missing reports that in its own status text and leaves the rest of the app usable.</summary>
    Task StartAsync();

    /// <summary>True while a device write is in flight. The shell waits for every module to
    /// go idle before it hands the hardware back on close.</summary>
    bool IsBusy { get; }
}
