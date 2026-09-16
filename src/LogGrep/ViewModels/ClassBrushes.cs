using System.Windows;
using System.Windows.Media;

namespace LogGrep.ViewModels;

/// <summary>
/// Turns the class colours into brushes, one per colour rather than one per row. They are frozen,
/// so the same instance is reused by every table without any threading care.
/// </summary>
public static class ClassBrushes
{
    private static readonly Dictionary<string, Brush> Cache = new(StringComparer.Ordinal);

    public static Brush For(string color)
    {
        if (string.IsNullOrEmpty(color)) return Unknown;

        if (Cache.TryGetValue(color, out var brush)) return brush;

        try
        {
            brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
        }
        catch (FormatException)
        {
            brush = Unknown;
        }

        Cache[color] = brush;
        return brush;
    }

    /// <summary>A player whose spec never showed up keeps the muted colour of the placeholder dash.</summary>
    private static Brush Unknown =>
        Application.Current?.TryFindResource("MutedBrush") as Brush ?? Brushes.Gray;
}
