using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LogGrep.Models;
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

    private static readonly Brush Bone = Frozen(new SolidColorBrush(Color.FromRgb(0xE0, 0x70, 0x6D)));

    private static readonly Brush Socket = Frozen(new SolidColorBrush(Color.FromRgb(0x17, 0x18, 0x1B)));

    /// <summary>The kill mark. Light blue, which is the one colour no line on this chart uses.</summary>
    private static readonly Brush Crown = Frozen(new SolidColorBrush(Color.FromRgb(0x8F, 0xC7, 0xF0)));

    /// <summary>A phase boundary: a dotted upright, so it reads as a divider and not as a line.</summary>
    private static readonly Pen Divide = Frozen(new Pen(
        new SolidColorBrush(Color.FromRgb(0x6A, 0x6E, 0x78)), 1) { DashStyle = new DashStyle(new double[] { 3, 3 }, 0) });

    private static readonly Brush Marker = Frozen(new SolidColorBrush(Color.FromRgb(0x9A, 0x9F, 0xAA)));

    public static readonly DependencyProperty TracesProperty = DependencyProperty.Register(
        nameof(Traces), typeof(IReadOnlyList<Trace>), typeof(FightShape),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnTracesChanged));

    public static readonly DependencyProperty DeathsProperty = DependencyProperty.Register(
        nameof(Deaths), typeof(IReadOnlyList<int>), typeof(FightShape),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty KillsProperty = DependencyProperty.Register(
        nameof(Kills), typeof(IReadOnlyList<BossKill>), typeof(FightShape),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PhasesProperty = DependencyProperty.Register(
        nameof(Phases), typeof(IReadOnlyList<PhaseStart>), typeof(FightShape),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
        nameof(From), typeof(int), typeof(FightShape),
        new FrameworkPropertyMetadata(0,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To), typeof(int), typeof(FightShape),
        new FrameworkPropertyMetadata(int.MaxValue,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    private static readonly Brush Shade = Frozen(new SolidColorBrush(Color.FromArgb(0x30, 0x4C, 0x8F, 0xD8)));

    private double _dragFrom = -1;
    private double _dragTo = -1;

    /// <summary>The stretch of the fight being read, in seconds. Dragging across the chart sets it.</summary>
    public int From
    {
        get => (int)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public int To
    {
        get => (int)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

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

    /// <summary>Where each phase after the first began.</summary>
    public IReadOnlyList<PhaseStart>? Phases
    {
        get => (IReadOnlyList<PhaseStart>?)GetValue(PhasesProperty);
        set => SetValue(PhasesProperty, value);
    }

    /// <summary>Every boss that went down in this attempt - one for a boss pull, several for a key.</summary>
    public IReadOnlyList<BossKill>? Kills
    {
        get => (IReadOnlyList<BossKill>?)GetValue(KillsProperty);
        set => SetValue(KillsProperty, value);
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

        // Where the fight changed, under everything else: a phase is the ground the lines are drawn
        // on rather than one of them.
        if (seconds > 0)
        {
            foreach (var phase in Phases ?? Array.Empty<PhaseStart>())
            {
                double x = Math.Round(Math.Clamp(phase.Second / seconds, 0, 1) * width) + 0.5;
                dc.DrawLine(Divide, new Point(x, 0), new Point(x, height));
                dc.DrawText(Numbered("P" + phase.Number), new Point(x + 3, 1));
            }
        }

        for (double t = 60; t < seconds; t += 60)
        {
            double x = Math.Round(t / seconds * width) + 0.5;
            dc.DrawLine(Grid, new Point(x, 0), new Point(x, height));
            dc.DrawText(Small(Display.Clock(TimeSpan.FromSeconds(t))), new Point(x + 3, height - 14));
        }

        // What is being read, shaded. Everything outside it is still drawn, because a stretch of a
        // fight only means something against the fight it came out of.
        if (seconds > 0)
        {
            double a = _dragFrom >= 0 ? Math.Min(_dragFrom, _dragTo) : From / seconds * width;
            double b = _dragFrom >= 0
                ? Math.Max(_dragFrom, _dragTo)
                : (To >= seconds ? width : To / seconds * width);

            if (b - a > 1 && (a > 0 || b < width)) dc.DrawRectangle(Shade, null, new Rect(a, 0, b - a, height));
        }

        var deaths = Deaths;
        if (deaths != null && seconds > 0)
        {
            foreach (int at in deaths) Skull(dc, Math.Clamp(at / seconds, 0, 1) * width, height - 7);
        }

        foreach (var trace in Traces ?? Array.Empty<Trace>())
        {
            if (trace.IsOn) Draw(dc, trace, width, height, seconds);
        }

        // The kill, on top of everything else. A wipe and a kill are the same shape until the last
        // few seconds of them, and this is the one mark that says which of the two somebody is
        // looking at without them reading the lines first.
        if (seconds > 0)
        {
            foreach (var kill in Kills ?? Array.Empty<BossKill>())
            {
                Star(dc, Math.Clamp(Math.Clamp(kill.Second / seconds, 0, 1) * width, 7, width - 7), 8);
            }
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

        if (_dragFrom >= 0)
        {
            _dragTo = e.GetPosition(this).X;
            InvalidateVisual();
        }

        int at = (int)Math.Round(Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1) * seconds);

        ToolTip = string.Join(Environment.NewLine, ChartReadout.At(traces, Deaths, Kills, at, Phases));
    }

    /// <summary>
    /// Dragging across the chart picks a stretch of the fight to read. A double-click puts it back,
    /// because the way out has to be as cheap as the way in or people stop using the way in.
    /// </summary>
    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (e.ClickCount == 2)
        {
            From = 0;
            To = int.MaxValue;
            _dragFrom = _dragTo = -1;
            InvalidateVisual();
            return;
        }

        _dragFrom = _dragTo = e.GetPosition(this).X;
        CaptureMouse();
    }

    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_dragFrom < 0) return;

        ReleaseMouseCapture();

        double seconds = Longest();
        double a = Math.Min(_dragFrom, _dragTo);
        double b = Math.Max(_dragFrom, _dragTo);
        _dragFrom = _dragTo = -1;

        // A click rather than a drag. Picking a stretch two pixels wide is nobody's intention, and
        // leaving the window alone is what they meant.
        if (seconds <= 0 || ActualWidth <= 2 || b - a < 4)
        {
            InvalidateVisual();
            return;
        }

        From = (int)Math.Round(Math.Clamp(a / ActualWidth, 0, 1) * seconds);
        To = (int)Math.Round(Math.Clamp(b / ActualWidth, 0, 1) * seconds);
    }

    /// <summary>
    /// A small skull where somebody went down. Drawn rather than set as a character, because a glyph
    /// at this size depends on whichever font happens to carry it and half of them do not.
    /// </summary>
    private static void Skull(DrawingContext dc, double x, double y)
    {
        // A cranium, a jaw under it, and two sockets cut back out - four shapes, and it reads at
        // nine pixels, which a cross also did but said nothing about what it was.
        dc.DrawEllipse(Bone, null, new Point(x, y - 1), 4, 3.6);
        dc.DrawRectangle(Bone, null, new Rect(x - 2.2, y + 1.6, 4.4, 2.6));
        dc.DrawEllipse(Socket, null, new Point(x - 1.6, y - 1.2), 1.2, 1.3);
        dc.DrawEllipse(Socket, null, new Point(x + 1.6, y - 1.2), 1.2, 1.3);
    }

    /// <summary>
    /// A star where the enemy went down. Drawn rather than set as a glyph for the same reason the
    /// skull is: at this size it would depend on whichever font happened to carry it.
    /// </summary>
    private static void Star(DrawingContext dc, double x, double y)
    {
        const int Points = 5;
        var figure = new PathFigure { IsClosed = true, IsFilled = true };

        // Ten corners alternating between the outer and inner radius, starting at the top.
        for (int i = 0; i < Points * 2; i++)
        {
            double radius = i % 2 == 0 ? 6.5 : 2.8;
            double angle = -Math.PI / 2 + i * Math.PI / Points;
            var corner = new Point(x + Math.Cos(angle) * radius, y + Math.Sin(angle) * radius);

            if (i == 0) figure.StartPoint = corner;
            else figure.Segments.Add(new LineSegment(corner, true));
        }

        var path = new PathGeometry();
        path.Figures.Add(figure);
        path.Freeze();

        dc.DrawGeometry(Crown, null, path);
    }

    private static void Draw(DrawingContext dc, Trace trace, double width, double height, double seconds)
    {
        var line = trace.Drawn;
        if (line.Count < 2) return;

        double peak = trace.Peak;
        var pen = new Pen(trace.Paint, 1.5);
        pen.Freeze();

        // One figure per unbroken stretch. A gap means the line is about something that was not
        // happening then, and drawing across it would invent a fight between two bosses.
        var path = new PathGeometry();
        PathFigure? figure = null;

        for (int i = 0; i < line.Count; i++)
        {
            if (double.IsNaN(line[i]))
            {
                figure = null;
                continue;
            }

            var point = At(line, i, width, height, peak, seconds);
            if (figure == null)
            {
                figure = new PathFigure { StartPoint = point };
                path.Figures.Add(figure);
            }
            else
            {
                figure.Segments.Add(new LineSegment(point, true));
            }
        }

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

        return traces.Max(t => t.Drawn.Count) - 1;
    }

    /// <summary>A phase's number, in the one colour on this chart that belongs to no line.</summary>
    private static FormattedText Numbered(string text)
        => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 10, Marker, 96);

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
