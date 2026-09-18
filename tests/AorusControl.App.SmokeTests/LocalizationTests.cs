using System.Reflection;
using AorusControl.App.Localization;

internal static class LocalizationTests
{
    /// <summary>
    /// The reason the vocabulary is two dictionaries rather than two .resx files: a test can
    /// compare them key by key. A missing translation is a failure here instead of a German
    /// word turning up in an English window months later, and nothing else would catch it -
    /// the lookup falls back to the key, which looks deliberate enough to survive a review.
    /// </summary>
    public static void Run()
    {
        IReadOnlyDictionary<string, string> german = Table("GermanStrings");
        IReadOnlyDictionary<string, string> english = Table("EnglishStrings");

        Check(german.Count > 100, $"the vocabulary should cover the interface, got {german.Count} keys");

        string[] onlyGerman = german.Keys.Except(english.Keys).Order().ToArray();
        string[] onlyEnglish = english.Keys.Except(german.Keys).Order().ToArray();
        Check(onlyGerman.Length == 0, $"untranslated: {string.Join(", ", onlyGerman)}");
        Check(onlyEnglish.Length == 0, $"English keys with no German original: {string.Join(", ", onlyEnglish)}");

        string[] empty = german.Concat(english).Where(pair => string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => pair.Key).Distinct().Order().ToArray();
        Check(empty.Length == 0, $"empty text for: {string.Join(", ", empty)}");

        // A translation that is the German word again is usually a forgotten one. The few
        // that genuinely match - product names, "Normal", "Gaming" - are named here so the
        // check keeps working rather than being switched off.
        string[] sameOnPurpose =
        [
            "Nav_Dashboard", "Cool_Normal", "Cool_Gaming", "Cool_Live", "Pwr_ModeBalanced",
            "About_Version", "About_Updates", "About_Log", "Key_EffectManual",
            // A product name and a word English borrowed unchanged.
            "Gpu_ChipNvidia", "Tray_Limit"
        ];
        string[] suspicious = german
            .Where(pair => english[pair.Key] == pair.Value && !sameOnPurpose.Contains(pair.Key))
            .Select(pair => pair.Key).Order().ToArray();
        Check(suspicious.Length == 0, $"same text in both languages, probably untranslated: {string.Join(", ", suspicious)}");

        // Switching has to reach bound text, which happens through the indexer.
        Strings.Current.Use(AppLanguage.English);
        Check(Strings.Current["Nav_Cooling"] == "Cooling", "English is served after switching to it");
        Strings.Current.Use(AppLanguage.German);
        Check(Strings.Current["Nav_Cooling"] == "Kühlung", "and German after switching back");
        Strings.Current.Use(AppLanguage.System);
        Check(Strings.Current["Nav_Cooling"].Length > 0, "System resolves to one of them");

        Check(Strings.Current["Gibt_Es_Nicht"] == "Gibt_Es_Nicht",
            "an unknown key shows itself - a blank label would hide the mistake");

        Console.WriteLine($"PASS: both languages cover the same {german.Count} keys, and switching reaches the indexer");
    }

    private static IReadOnlyDictionary<string, string> Table(string typeName)
    {
        Type type = typeof(Strings).Assembly.GetType($"AorusControl.App.Localization.{typeName}")
            ?? throw new InvalidOperationException($"{typeName} nicht gefunden");
        return (IReadOnlyDictionary<string, string>)type
            .GetField("Table", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
