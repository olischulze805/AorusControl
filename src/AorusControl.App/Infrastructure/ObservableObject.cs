using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AorusControl.App.Infrastructure;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Announces every property at once - WPF's meaning for the empty name.
    ///
    /// Used when the language changes: sentences the view model computes rather than stores,
    /// like "Fans on Normal", are not bound through the string table and would otherwise keep
    /// the old language until something else happened to touch them.
    /// </summary>
    internal void RefreshAllProperties() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
