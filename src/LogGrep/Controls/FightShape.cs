using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LogGrep.ViewModels;

namespace LogGrep.Controls;

/// <summary>
/// The shape of an attempt: the enemy going down, the group thinning out, and what the group was
/// putting out while both happened.
///
/// Four lines on one clock, and between them they tell the story a table cannot. An enemy line that
/// stops at forty per cent while the group line falls off a cliff is a raid that melted; a damage
/// line that drops away before either of them is a raid that lost the people doing the damage. A
/// cross marks where somebody died, because that is the moment the other three lines bend around.
///
/// They have no common unit, so each is drawn against its own high point and the axis carries only
/// the clock and a quarter scale. That is the trade: the picture says when and what shape, and the
/// hover says how much - for every line at once, at the second under the pointer.
/// </summary>
public sealed class FightShape : FrameworkElement
{
    private static readonly Pen Grid =
        Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x32)), 1));

    private static readonly Brush Label = Frozen(new SolidColorBrush(Color.FromRgb(0x62, 0x66, 0x70)));

    private static readonly Pen DeathMark =
        Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0xE0, 0x70, 0x6D)), 1.5));

    public static readonly DependencyProperty TracesProperty = DependencyProperty.Register(
        nameof(Traces), typeof(IReadOnlyList<Trace>), typeof(FightShape),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnTracesChanged));

    public static readonly DependencyProperty DeathsProperty = DependencyProperty.Register(
        nameof(Deaths), typeof(IReadOnlyList<int>), typeof(FightShape),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public FightShape()
    {
        ToolTipService.SetInitialShowDelay(this, 120);
        ToolTipService.SetShowDuration(this, 30000);
    }

    /// <summary>The lines, each with its own unit and its own switch.</summary>
    public IReadOnlyList<Trace>? Traces
    {
        get => (IReadOnlyList<Trace>?)GetValue(TracesProperty);
        set => SetValue(TracesProperty, value);
    }

    /// <summary>The seconds somebody went down, marked with a cross.</summary>
    public IReadOnlyList<int>? Deaths
    {
        get => (IReadOnlyList<int>?)GetValue(DeathsProperty);
        set => SetValue(DeathsProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 2 || height <= 2) return;

        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));

        // Quarters across and minutes along, both labelled. A line with no numbers anywhere near it
        // is a mood rather than a measurement.
        for (int i = 1; i < 4; i++)
        {
            double y = Math.Round(height * i / 4.0) + 0.5;
            dc.DrawLine(Grid, new Point(0, y), new Point(width, y));
            dc.DrawText(Small(Display.Percent(1 - i / 4.0) + " of peak"), new Point(2, y - 7));
        }

        double seconds = Longest();
        for (double t = 60; t < seconds; t += 60)
        {
            double x = Math.Round(t / seconds * width) + 0.5;
            dc.DrawLine(Grid, new Point(x, 0), new Point(x, height));
            dc.DrawText(Small(Display.Clock(TimeSpan.FromSeconds(t))), new Point(x + 3, height - 14));
        }

        var deaths = Deaths;
        if (deaths != null && seconds > 0)
        {
            foreach (int at in deaths)
            {
                double x = Math.Clamp(at / seconds, 0, 1) * width;
                dc.DrawLine(DeathMark, new Point(x - 3, height - 9), new Point(x + 3, height - 3));
                dc.DrawLine(DeathMark, new Point(x - 3, height - 3), new Point(x + 3, height - 9));
            }
        }

        foreach (var trace in Traces ?? Array.Empty<Trace>())
        {
            if (trace.IsOn) Draw(dc, trace, width, height, seconds);
        }
    }

    /// <summary>
    /// Every line at the second under the pointer. A chart somebody can read the shape of but not
    /// the value at is one they end up guessing from, and four lines against four different scales
    /// leave no honest way to put the numbers on an axis.
    /// </summary>
    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var traces = Traces;
        double seconds = Longest();

        if (traces == null || traces.Count == 0 || seconds <= 0 || ActualWidth <= 2)
        {
            ToolTip = null;
            return;
        }

        int at = (int)Math.Round(Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1) * seconds);
        var said = new List<string> { "at " + Display.Clock(TimeSpan.FromSeconds(at)) };

        said.AddRange(traces.Where(t => t.Values.Count > 0).Select(t => t.At(at)));

        var deaths = Deaths?.Where(d => Math.Abs(d - at) <= 1).ToList();
        if (deaths is { Count: > 0 }) said.Add(deaths.Count == 1 ? "somebody died here" : deaths.Count + " died here");

        ToolTip = string.Join(Environment.NewLine, said);
    }

    private static void Draw(DrawingContext dc, Trace trace, double width, double height, double seconds)
    {
        var line = trace.Values;
        if (line.Count < 2) return;

        double peak = trace.Peak;
        var pen = new Pen(trace.Paint, 1.5);
        pen.Freeze();

        var figure = new PathFigure { StartPoint = At(line, 0, width, height, peak, seconds) };
        for (int i = 1; i < line.Count; i++)
        {
            figure.Segments.Add(new LineSegment(At(line, i, width, height, peak, seconds), true));
        }

        var path = new PathGeometry();
        path.Figures.Add(figure);
        path.Freeze();

        dc.DrawGeometry(null, pen, path);
    }

    private static Point At(IReadOnlyList<double> line, int i, double width, double height,
        double peak, double seconds)
        => new(seconds <= 0 ? 0 : Math.Clamp(i / seconds, 0, 1) * width,
            height - Math.Clamp(line[i] / peak, 0, 1) * (height - 18) - 16);

    /// <summary>The longest line there is, which is what the clock along the bottom counts off.</summary>
    private double Longest()
    {
        var traces = Traces;
        if (traces == null || traces.Count == 0) return 0;

        return traces.Max(t => t.Values.Count) - 1;
    }

    private static FormattedText Small(string text)
        => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 10, Label, 96);

    /// <summary>Redraws when a line is switched on or off, which is the whole point of the switches.</summary>
    private static void OnTracesChanged(DependencyObject where, DependencyPropertyChangedEventArgs e)
    {
        if (where is not FightShape shape) return;

        foreach (var trace in (IReadOnlyList<Trace>?)e.NewValue ?? Array.Empty<Trace>())
        {
            trace.PropertyChanged += (_, _) => shape.InvalidateVisual();
        }
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
