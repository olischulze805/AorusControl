using AorusControl.App.Infrastructure;

namespace AorusControl.App.Localization;

/// <summary>One entry in the language drop-down.</summary>
public sealed record LanguageChoice(AppLanguage Value, string Text);

/// <summary>
/// The language picker.
///
/// The entries are deliberately written in their own language - "Deutsch", not "German" -
/// because somebody hunting for their language in a window they cannot read is looking for
/// the word they know, not for its translation.
/// </summary>
public sealed class LanguageViewModel : ObservableObject
{
    private AppLanguage _selected;

    /// <summary>Reads back what <see cref="Apply"/> already put in force. It does not apply
    /// anything itself: by the time this is built the first view models have long since
    /// produced their first sentences, and they have to be in the right language already.</summary>
    public LanguageViewModel() => _selected = Strings.Current.Language;

    /// <summary>Puts the stored choice in force. Called once, before anything is built.</summary>
    public static void Apply() => Strings.Current.Use(LanguageSetting.Load());

    public IReadOnlyList<LanguageChoice> Choices { get; } =
    [
        new(AppLanguage.System, SystemText()),
        new(AppLanguage.German, "Deutsch"),
        new(AppLanguage.English, "English")
    ];

    public AppLanguage Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            Strings.Current.Use(value);
            LanguageSetting.Save(value);
        }
    }

    /// <summary>The "follow Windows" entry names what it will actually do, so the choice is
    /// not between a language and a promise.</summary>
    private static string SystemText() =>
        Strings.FromWindows() == AppLanguage.German ? "Windows (Deutsch)" : "Windows (English)";
}
