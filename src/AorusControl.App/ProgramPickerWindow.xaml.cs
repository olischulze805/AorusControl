using System.Collections.ObjectModel;
using System.Windows;
using AorusControl.App.Infrastructure;
using AorusControl.App.Localization;
using AorusControl.App.ViewModels;
using AorusControl.Core.Features.GpuPreferences;
using Wpf.Ui.Controls;

namespace AorusControl.App;

/// <summary>
/// Search for a program by name, the way the vendor tools let you.
///
/// The file dialog it replaces could only ever offer half of what is installed: Store apps
/// have no executable to point at, and their id is not something anybody types by hand. This
/// lists both kinds together and hands back whichever identity the preference has to be keyed
/// by. Picking a file is still there, for the programs no start menu knows about - a game
/// inside a launcher's library, say.
/// </summary>
public partial class ProgramPickerWindow : FluentWindow
{
    private readonly ProgramPickerViewModel _viewModel = new();

    /// <summary>The chosen program's identity: a path, or a Store app id.</summary>
    public string? Result { get; private set; }

    public ProgramPickerWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += async (_, _) =>
        {
            SearchBox.Focus();
            await _viewModel.LoadAsync();
        };
    }

    private void OnSearchChanged(object sender, RoutedEventArgs eventArgs) =>
        _viewModel.Query = SearchBox.Text;

    private void OnResultDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs eventArgs) => Accept();

    private void OnAcceptClick(object sender, RoutedEventArgs eventArgs) => Accept();

    private void OnCancelClick(object sender, RoutedEventArgs eventArgs) => Close();

    private void OnBrowseClick(object sender, RoutedEventArgs eventArgs)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Localization.Strings.Current["Dlg_ChooseProgram"],
            Filter = Strings.Current["Picker_ExeFilter"],
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        Result = dialog.FileName;
        DialogResult = true;
    }

    private void Accept()
    {
        if (_viewModel.Selected is not { } program) return;
        Result = program.Identity;
        DialogResult = true;
    }
}

/// <summary>The list and the search box behind the picker.</summary>
public sealed class ProgramPickerViewModel : ObservableObject
{
    private IReadOnlyList<InstalledProgram> _all = [];
    private InstalledProgram? _selected;
    private string _query = string.Empty;
    private string _summary = Strings.Current["Picker_Searching"];

    public ObservableCollection<InstalledProgram> Results { get; } = [];
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public bool HasSelection => _selected is not null;

    public InstalledProgram? Selected
    {
        get => _selected;
        set { if (SetProperty(ref _selected, value)) OnPropertyChanged(nameof(HasSelection)); }
    }

    public string Query
    {
        get => _query;
        set { if (SetProperty(ref _query, value)) Show(); }
    }

    /// <summary>Scanning walks the start menu and asks the shell about every shortcut, which
    /// takes about two seconds here - far too long for the UI thread.</summary>
    public async Task LoadAsync()
    {
        _all = await Task.Run(InstalledPrograms.Scan);
        Show();
    }

    private void Show()
    {
        IReadOnlyList<InstalledProgram> matches = InstalledPrograms.Search(_all, _query);
        Results.Clear();
        foreach (InstalledProgram program in matches.Take(400)) Results.Add(program);

        Summary = _all.Count == 0
            ? Strings.Current["Picker_NoneFound"]
            : matches.Count == _all.Count
                ? Strings.Current.Format("Picker_AllPrograms", _all.Count, _all.Count(program => program.IsStoreApp))
                : Strings.Current.Format("Picker_Matches", matches.Count, _all.Count);
    }
}
