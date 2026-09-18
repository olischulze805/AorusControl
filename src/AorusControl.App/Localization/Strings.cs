using System.ComponentModel;
using System.Globalization;
using Binding = System.Windows.Data.Binding;
using BindingMode = System.Windows.Data.BindingMode;
using System.Windows.Markup;

namespace AorusControl.App.Localization;

/// <summary>Which language the interface speaks.</summary>
public enum AppLanguage
{
    /// <summary>Follow Windows: German on a German system, English everywhere else.</summary>
    System,
    German,
    English
}

/// <summary>
/// The app's text, in one place per language.
///
/// Plain dictionaries rather than .resx, deliberately. Two languages and one developer do not
/// need satellite assemblies and a designer file, and this way the whole vocabulary is two
/// readable files that a test can compare key by key - which .resx cannot do at all. A missing
/// translation is then a failing test rather than a German word showing up in an English
/// window six months later.
///
/// Switching is live: the indexer is bound, and raising PropertyChanged for "Item[]" makes
/// every bound string in every open window re-read itself. No restart, no reload.
/// </summary>
public sealed class Strings : INotifyPropertyChanged
{
    public static Strings Current { get; } = new();

    private IReadOnlyDictionary<string, string> _table = GermanStrings.Table;
    private AppLanguage _language = AppLanguage.System;

    private Strings() => Use(AppLanguage.System);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The text for a key, or the key itself when there is none. Showing the key is
    /// on purpose: a blank label hides the mistake, a visible "Nav_Dashboard" reports it.</summary>
    public string this[string key] => _table.TryGetValue(key, out string? text) ? text : key;

    public AppLanguage Language => _language;

    /// <summary>What <see cref="AppLanguage.System"/> resolves to right now.</summary>
    public static AppLanguage FromWindows() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.German
            : AppLanguage.English;

    public void Use(AppLanguage language)
    {
        _language = language;
        AppLanguage effective = language == AppLanguage.System ? FromWindows() : language;
        _table = effective == AppLanguage.German ? GermanStrings.Table : EnglishStrings.Table;
        // The empty name is WPF's way of saying "all of them"; "Item[]" is the indexer.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    /// <summary>A formatted string, for the places that build a sentence around a number.</summary>
    public string Format(string key, params object?[] values) =>
        string.Format(CultureInfo.CurrentCulture, this[key], values);
}

/// <summary>
/// <c>Text="{loc:T Nav_Dashboard}"</c> - shorter than spelling out the indexer binding, and
/// a binding rather than a fixed value so the language can change while the window is open.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension : MarkupExtension
{
    public TExtension() { }
    public TExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object? ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]")
        {
            Source = Strings.Current,
            Mode = BindingMode.OneWay
        }.ProvideValue(serviceProvider);
}
