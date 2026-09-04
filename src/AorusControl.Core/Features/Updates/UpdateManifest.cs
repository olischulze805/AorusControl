namespace AorusControl.Core.Features.Updates;

/// <summary>
/// The expected shape of the JSON document a feed URL must serve for update checks to
/// work. Field names are case-insensitive on read (System.Text.Json default). Example:
/// <code>
/// {
///   "version": "0.2.0",
///   "title": "AORUS Control 0.2.0",
///   "notes": "Vier Helligkeitsstufen für die Tastatur, absturzsicherer Fixed-Modus.",
///   "downloadUrl": "https://example.invalid/aorus-control/releases/0.2.0/setup.exe"
/// }
/// </code>
/// </summary>
public sealed record UpdateManifest(string Version, string Title, string Notes, string DownloadUrl);
