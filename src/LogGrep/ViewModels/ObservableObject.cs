using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace LogGrep.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>Shared number/duration formatting for the grid.</summary>
public static class Display
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Rate(double value) => value switch
    {
        >= 1_000_000_000 => (value / 1_000_000_000).ToString("0.##", Inv) + "B",
        >= 1_000_000 => (value / 1_000_000).ToString("0.##", Inv) + "M",
        >= 1_000 => (value / 1_000).ToString("0.#", Inv) + "K",
        > 0 => value.ToString("0", Inv),
        _ => "—",
    };

    /// <summary>A raw total, scaled the same way as a rate.</summary>
    public static string Amount(long value) => Rate(value);

    /// <summary>
    /// A share, whole-numbered. A hit can land for more than a full health pool, and rounding that
    /// down to 100% would hide exactly how far past survivable it was.
    /// </summary>
    public static string Percent(double value) => Math.Round(value * 100).ToString("0", Inv) + "%";

    /// <summary>
    /// A small number with at most one decimal. Invariant, like everything else here: the app is
    /// read on a machine whose locale writes 2,5 and copied into a chat where that is a second
    /// number.
    /// </summary>
    public static string Decimal(double value) => value.ToString("0.#", Inv);

    /// <summary>Time inside a pull as "m:ss", the shape death times are read in.</summary>
    public static string Clock(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        return (int)value.TotalMinutes + ":" + value.Seconds.ToString("00", Inv);
    }

    public static string Duration(TimeSpan value)
    {
        if (value <= TimeSpan.Zero) return "—";
        return value.TotalHours >= 1
            ? ((int)value.TotalHours) + ":" + value.ToString(@"mm\:ss")
            : ((int)value.TotalMinutes) + ":" + value.ToString(@"ss", Inv);
    }
}
