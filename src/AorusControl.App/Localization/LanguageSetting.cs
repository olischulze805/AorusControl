using System.IO;
using System.Text.Json;
using AorusControl.Core.Features.Diagnostics;

namespace AorusControl.App.Localization;

/// <summary>
/// Remembers the chosen language across restarts.
///
/// One value in one small file rather than a corner of a larger one: the language has to be
/// read before anything is drawn, and a store that can fail for reasons belonging to some
/// other feature would be a poor thing to depend on that early. A missing or damaged file is
/// not an error here - it means "follow Windows", which is also the default.
/// </summary>
public static class LanguageSetting
{
    private static readonly string Path = AppData.File("language-v1.json");

    public static AppLanguage Load()
    {
        try
        {
            if (!File.Exists(Path)) return AppLanguage.System;
            Stored? stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(Path));
            return stored is { Language: { } name } && Enum.TryParse(name, out AppLanguage language)
                && Enum.IsDefined(language)
                ? language
                : AppLanguage.System;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return AppLanguage.System;
        }
    }

    public static void Save(AppLanguage language)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(new Stored(1, language.ToString())));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("language", "Sprache konnte nicht gespeichert werden.", exception);
        }
    }

    private sealed record Stored(int Version, string? Language);
}
