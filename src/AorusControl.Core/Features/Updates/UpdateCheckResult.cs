namespace AorusControl.Core.Features.Updates;

public enum UpdateOutcome { UpToDate, UpdateAvailable, Failed }

public sealed record UpdateCheckResult(UpdateOutcome Outcome, string Message, UpdateManifest? Manifest = null)
{
    public static UpdateCheckResult UpToDate(string currentVersion) =>
        new(UpdateOutcome.UpToDate, $"Aktuell: Version {currentVersion} ist die neueste.");

    public static UpdateCheckResult Available(UpdateManifest manifest) =>
        new(UpdateOutcome.UpdateAvailable, $"Update verfügbar: Version {manifest.Version}.", manifest);

    public static UpdateCheckResult Failed(string message) =>
        new(UpdateOutcome.Failed, message);
}
