using System.Windows.Input;

namespace AorusControl.App.Infrastructure;

/// <summary>Parameterised sibling of <see cref="AsyncRelayCommand"/>, for XAML
/// CommandParameter bindings (fan profile name, power mode, effect, and so on)
/// instead of code-behind Click handlers with a Tag lookup.</summary>
public sealed class AsyncRelayCommand<T>(Func<T, Task> execute) : ICommand
{
    private bool _running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_running;
    public async void Execute(object? parameter)
    {
        if (parameter is T value) await ExecuteAsync(value);
    }

    public async Task ExecuteAsync(T parameter)
    {
        if (_running) return;
        _running = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await execute(parameter); }
        finally { _running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
}
