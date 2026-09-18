using System.Windows.Media;

namespace LogGrep.ViewModels;

/// <summary>
/// One line on the shape of an attempt, and whether it is being drawn.
///
/// Four of them share a clock and nothing else: a share of a pool, a count of people, and two rates.
/// They are drawn each against its own high point, because they have no common unit and forcing one
/// would make three of them unreadable to flatter the fourth. Which means the picture says <em>when</em>
/// and <em>what shape</em>, and the hover says how much - the numbers are in the tooltip precisely
/// because the axis cannot carry four of them.
/// </summary>
public sealed class Trace : ObservableObject
{
    private bool _on;

    public Trace(string name, Color colour, IReadOnlyList<double> values, Func<double, string> say, bool on)
    {
        Name = name;
        Colour = colour;
        Values = values;
        Say = say;
        _on = on;

        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        Paint = brush;
    }

    public string Name { get; }

    public Color Colour { get; }

    public Brush Paint { get; }

    /// <summary>The line itself, one point a second, in whatever unit it is about.</summary>
    public IReadOnlyList<double> Values { get; }

    /// <summary>Turns one of its values into the words the hover shows.</summary>
    public Func<double, string> Say { get; }

    /// <summary>The highest it ever reaches, which is what it is drawn against.</summary>
    public double Peak => Values.Count == 0 ? 0 : Math.Max(Values.Max(), double.Epsilon);

    public bool IsOn
    {
        get => _on;
        set => Set(ref _on, value);
    }

    /// <summary>What this line says at that second, or nothing when it does not reach that far.</summary>
    public string At(int second)
        => Values.Count == 0 ? string.Empty : Name + " " + Say(Values[Math.Clamp(second, 0, Values.Count - 1)]);
}
