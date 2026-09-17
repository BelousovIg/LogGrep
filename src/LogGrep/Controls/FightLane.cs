using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LogGrep.ViewModels;

namespace LogGrep.Controls;

/// <summary>
/// One row's share of a fight, drawn on the same time axis as every other row.
///
/// This is what replaced a column of text. A sentence in a cell carries no size, no repetition and
/// no moment, and it cannot be compared with the sentence in the row below it without being read -
/// so the cell holds a shape instead. Position is when, area is what it cost, and colour and form
/// are what kind of thing it was.
///
/// The gain is not per row, it is down the column. Every lane shares one axis, so marks line up:
/// four of them on the same second are not four people's four mistakes, they are one thing that
/// happened to the group, and the lane says so by drawing those marks hollow.
///
/// Nothing here is clickable yet and nothing is labelled. That is deliberate - a collapsed row gets
/// one lane and one sentence, and every bit of depth lives behind a hover or an expansion. Letting
/// labels onto the first screen would rebuild the unreadable cell in a better font.
/// </summary>
public sealed class FightLane : FrameworkElement
{
    /// <summary>Half-height of the smallest mark. Below this a dot stops reading as a shape at all.</summary>
    private const double MinimumRadius = 2.5;

    /// <summary>
    /// And the largest. A death costs a whole health pool and a graze costs a twentieth, which is a
    /// ratio no lane can draw honestly - so size is squashed into a range that stays legible and
    /// the real figure lives in the hover.
    /// </summary>
    private const double MaximumRadius = 6.5;

    /// <summary>How close the pointer has to be to a mark before its hover belongs to that mark.</summary>
    private const double Grab = 7;

    private static readonly Pen Baseline = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0x32, 0x35, 0x3C)), 1));

    private static readonly Brush Mechanic = Frozen(new SolidColorBrush(Color.FromRgb(0xE0, 0xA5, 0x54)));
    private static readonly Brush Interrupt = Frozen(new SolidColorBrush(Color.FromRgb(0x8E, 0x9B, 0xE8)));
    private static readonly Brush Idle = Frozen(new SolidColorBrush(Color.FromRgb(0x90, 0x96, 0xA0)));
    private static readonly Brush Opening = Frozen(new SolidColorBrush(Color.FromRgb(0x7F, 0xA8, 0xD8)));
    private static readonly Brush Death = Frozen(new SolidColorBrush(Color.FromRgb(0xE0, 0x70, 0x6D)));
    private static readonly Brush Enemy = Frozen(new SolidColorBrush(Color.FromRgb(0x6A, 0x6E, 0x78)));

    public static readonly DependencyProperty MarksProperty = DependencyProperty.Register(
        nameof(Marks), typeof(IReadOnlyList<LaneMark>), typeof(FightLane),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SecondsProperty = DependencyProperty.Register(
        nameof(Seconds), typeof(double), typeof(FightLane),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BandsProperty = DependencyProperty.Register(
        nameof(Bands), typeof(IReadOnlyList<TimeSpan>), typeof(FightLane),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush Band = Frozen(new SolidColorBrush(Color.FromArgb(0x40, 0xE0, 0xA5, 0x54)));

    public FightLane()
    {
        ToolTipService.SetInitialShowDelay(this, 120);
        ToolTipService.SetShowDuration(this, 30000);
    }

    /// <summary>What happened to this row, in the order it happened.</summary>
    public IReadOnlyList<LaneMark>? Marks
    {
        get => (IReadOnlyList<LaneMark>?)GetValue(MarksProperty);
        set => SetValue(MarksProperty, value);
    }

    /// <summary>How long the attempt ran, which is what the width stands for.</summary>
    public double Seconds
    {
        get => (double)GetValue(SecondsProperty);
        set => SetValue(SecondsProperty, value);
    }

    /// <summary>
    /// The moments one thing caught much of the group. The design calls for a band drawn down every
    /// row of the table, which this hand-built table has no layer to paint across - so the band is
    /// drawn on the enemy's lane above the group, and the marks it belongs to are drawn hollow on
    /// each player's. The alignment still reads, and no row has to know about its neighbours.
    /// </summary>
    public IReadOnlyList<TimeSpan>? Bands
    {
        get => (IReadOnlyList<TimeSpan>?)GetValue(BandsProperty);
        set => SetValue(BandsProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 1 || height <= 1) return;

        // A transparent fill over the whole element, or a hover would only land on the thin line
        // and the marks drawn on it, and the tooltip is where all the depth lives.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));

        double middle = Math.Round(height / 2) + 0.5;

        // Drawn even when there is nothing on it. An empty lane has to look deliberate - "nothing
        // was found here" - rather than like a cell that failed to load.
        dc.DrawLine(Baseline, new Point(0, middle), new Point(width, middle));

        if (Seconds <= 0) return;

        var bands = Bands;
        if (bands != null)
        {
            foreach (var at in bands)
            {
                double x = Math.Clamp(at.TotalSeconds / Seconds, 0, 1) * (width - 2) + 1;
                dc.DrawRectangle(Band, null, new Rect(x - 2, 0, 4, height));
            }
        }

        var marks = Marks;
        if (marks == null || marks.Count == 0) return;

        foreach (var mark in marks)
        {
            double x = Math.Clamp(mark.At / Seconds, 0, 1) * (width - 2) + 1;
            double radius = Radius(mark.Size);
            var brush = Paint(mark.Kind);

            switch (mark.Kind)
            {
                case MarkKind.Death:
                    Cross(dc, brush, x, middle, radius);
                    break;
                case MarkKind.Interrupt:
                    Triangle(dc, brush, x, middle, radius);
                    break;
                case MarkKind.Cast:
                    dc.DrawRectangle(brush, null, new Rect(x - 1, middle - radius, 2, radius * 2));
                    break;
                default:
                    // Hollow when the same moment caught much of the group: one event that hit five
                    // people is not five mistakes, and a row of rings lined up says that at a glance.
                    if (mark.Collective)
                    {
                        var pen = new Pen(brush, 1.5);
                        pen.Freeze();
                        dc.DrawEllipse(null, pen, new Point(x, middle), radius, radius);
                    }
                    else
                    {
                        dc.DrawEllipse(brush, null, new Point(x, middle), radius, radius);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// The nearest mark to the pointer, so a hover explains the thing under it rather than the row.
    /// Without this the lane would be a picture that cannot be questioned, and every number in this
    /// app is supposed to open.
    /// </summary>
    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var marks = Marks;
        if (marks == null || marks.Count == 0 || Seconds <= 0 || ActualWidth <= 1)
        {
            ToolTip = null;
            return;
        }

        double at = e.GetPosition(this).X;
        string? text = null;
        double best = Grab;

        foreach (var mark in marks)
        {
            double x = Math.Clamp(mark.At / Seconds, 0, 1) * (ActualWidth - 2) + 1;
            double gap = Math.Abs(x - at);
            if (gap > best) continue;

            best = gap;
            text = mark.Text;
        }

        ToolTip = text;
    }

    /// <summary>
    /// Area against cost, not radius against cost. The eye reads the blob, and a radius that
    /// doubles is a mark four times the size, which would make one death look like the whole fight.
    /// </summary>
    private static double Radius(double pools)
        => Math.Clamp(MinimumRadius + Math.Sqrt(Math.Max(pools, 0)) * 3.5, MinimumRadius, MaximumRadius);

    private static Brush Paint(MarkKind kind) => kind switch
    {
        MarkKind.Mechanic => Mechanic,
        MarkKind.Interrupt => Interrupt,
        MarkKind.Idle => Idle,
        MarkKind.Opening => Opening,
        MarkKind.Death => Death,
        _ => Enemy,
    };

    private static void Cross(DrawingContext dc, Brush brush, double x, double y, double r)
    {
        var pen = new Pen(brush, 2);
        pen.Freeze();
        dc.DrawLine(pen, new Point(x - r, y - r), new Point(x + r, y + r));
        dc.DrawLine(pen, new Point(x - r, y + r), new Point(x + r, y - r));
    }

    private static void Triangle(DrawingContext dc, Brush brush, double x, double y, double r)
    {
        var figure = new PathFigure
        {
            StartPoint = new Point(x, y - r),
            IsClosed = true,
            IsFilled = true,
        };

        figure.Segments.Add(new LineSegment(new Point(x + r, y + r), true));
        figure.Segments.Add(new LineSegment(new Point(x - r, y + r), true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();

        dc.DrawGeometry(brush, null, geometry);
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
