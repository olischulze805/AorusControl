using AorusControl.Core.Features.Diagnostics;
using System.Windows;
using AorusControl.App.ViewModels;
using AorusControl.Core.Features.PowerProfiles;
using Wpf.Ui.Controls;

namespace AorusControl.App;

public partial class ProfileWindow : FluentWindow
{
    public ProfileWindow() : this(null) { }

    public ProfileWindow(ProfileEditorViewModel? viewModel)
    {
        InitializeComponent();
        if (viewModel is null)
        {
            var store = new ProfileCatalogStore(AppData.File("profiles-v1.json"));
            viewModel = new ProfileEditorViewModel(store.Load, store.Save, ConfirmDiscard);
        }
        DataContext = viewModel;
        Closing += (_, e) =>
        {
            if (DataContext is ProfileEditorViewModel { IsBusy: true }) { e.Cancel = true; return; }
            if (DataContext is ProfileEditorViewModel { HasUnsavedChanges: true } && !ConfirmDiscard("Ungespeicherte Änderungen verwerfen und schließen?"))
                e.Cancel = true;
        };
    }

    private bool ConfirmDiscard(string message) => System.Windows.MessageBox.Show(this, message,
        "Ungespeicherte Änderungen", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning,
        System.Windows.MessageBoxResult.No) == System.Windows.MessageBoxResult.Yes;

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProfileEditorViewModel { Selected: { } selected } vm &&
            System.Windows.MessageBox.Show(this, $"Profil „{selected.Name}“ und seine Netz-/Akku-Zuordnungen löschen?", "Profil löschen",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes)
            vm.DeleteCommand.Execute(null);
    }
}
