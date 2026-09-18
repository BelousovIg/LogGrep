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

    public Trace(string name, Color colour, IReadOnlyList<double> values, Func<double, string> say, bool on,
        int smooth = 1)
    {
        Name = name;
        Colour = colour;
        Values = values;
        Drawn = Smoothed(values, smooth);
        Smoothing = smooth;
        Say = say;
        _on = on;

        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        Paint = brush;
    }

    public string Name { get; }

    public Color Colour { get; }

    public Brush Paint { get; }

    /// <summary>
    /// The line itself, one point a second, in whatever unit it is about. A point at 1:30 is
    /// everything that happened in the second up to 1:30, which is what somebody reading a point on
    /// a chart means by it.
    /// </summary>
    public IReadOnlyList<double> Values { get; }

    /// <summary>
    /// The same line as it is drawn.
    ///
    /// A rate measured in single seconds is spikes: one crit lands and the line jumps to three times
    /// what the fight was doing, which says something about that swing and nothing about the fight.
    /// So the rates are drawn as a rolling mean over the last few seconds, and the hover still reads
    /// the exact second - the picture is for the shape, the hover is for the number.
    ///
    /// Lines that are a state rather than a rate - the enemy's health, how many are up - are not
    /// smoothed: there is nothing noisy about them, and a mean of a headcount is not a headcount.
    /// </summary>
    public IReadOnlyList<double> Drawn { get; }

    /// <summary>Over how many seconds the drawn line is averaged; one means it is not.</summary>
    public int Smoothing { get; }

    public bool IsSmoothed => Smoothing > 1;

    /// <summary>Turns one of its values into the words the hover shows.</summary>
    public Func<double, string> Say { get; }

    /// <summary>
    /// The highest the drawn line ever reaches, which is what it is drawn against. Gaps are not
    /// values: a line that is not being drawn at that second cannot set the scale for the rest.
    /// </summary>
    public double Peak
    {
        get
        {
            double peak = 0;
            foreach (double v in Drawn)
            {
                if (!double.IsNaN(v) && v > peak) peak = v;
            }

            return Math.Max(peak, double.Epsilon);
        }
    }

    /// <summary>
    /// A trailing mean, so a point still means "the seconds up to here" and nothing is dragged
    /// earlier than it happened. The first seconds average over the few there are.
    /// </summary>
    private static IReadOnlyList<double> Smoothed(IReadOnlyList<double> values, int over)
    {
        if (over <= 1 || values.Count == 0) return values;

        var line = new double[values.Count];
        double running = 0;

        for (int i = 0; i < values.Count; i++)
        {
            running += values[i];
            if (i >= over) running -= values[i - over];

            line[i] = running / Math.Min(i + 1, over);
        }

        return line;
    }

    public bool IsOn
    {
        get => _on;
        set => Set(ref _on, value);
    }

    /// <summary>
    /// What this line says at that second, or nothing when it does not reach that far - or when it
    /// is not being drawn there at all, which is a gap rather than a zero.
    /// </summary>
    public string At(int second)
    {
        if (Values.Count == 0) return string.Empty;

        double value = Values[Math.Clamp(second, 0, Values.Count - 1)];
        return double.IsNaN(value) ? string.Empty : Name + " " + Say(value);
    }
}
