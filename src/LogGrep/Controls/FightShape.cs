using System.Windows;
using System.Windows.Media;

namespace LogGrep.Controls;

/// <summary>
/// The shape of an attempt: how far down the enemy went, and how many of the group were still up.
///
/// Two lines on one clock, and between them they tell the story a table cannot. A boss line that
/// falls steadily and stops at forty per cent while the group line drops off a cliff is a raid that
/// melted; one where both fall together is a fight that was close. Nothing about either line is a
/// judgement - they are what happened, and they are the first thing a person wants to see.
///
/// It is drawn rather than charted on purpose: a hundred points on three hundred pixels needs no
/// axes, no legend and no library, and adding them would only take room from the lanes underneath.
/// </summary>
public sealed class FightShape : FrameworkElement
{
    private static readonly Pen EnemyLine =
        Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0xE0, 0x70, 0x6D)), 1.5));

    private static readonly Pen GroupLine =
        Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0x69, 0xC0, 0x7A)), 1.5));

    private static readonly Pen Grid =
        Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x32)), 1));

    public static readonly DependencyProperty EnemyProperty = DependencyProperty.Register(
        nameof(Enemy), typeof(IReadOnlyList<double>), typeof(FightShape),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GroupProperty = DependencyProperty.Register(
        nameof(Group), typeof(IReadOnlyList<int>), typeof(FightShape),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The enemy's health as a share of its pool, one point a second.</summary>
    public IReadOnlyList<double>? Enemy
    {
        get => (IReadOnlyList<double>?)GetValue(EnemyProperty);
        set => SetValue(EnemyProperty, value);
    }

    /// <summary>How many of the group were standing, one point a second.</summary>
    public IReadOnlyList<int>? Group
    {
        get => (IReadOnlyList<int>?)GetValue(GroupProperty);
        set => SetValue(GroupProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 2 || height <= 2) return;

        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));

        // Quarters, so a glance can tell forty per cent from ten without anybody labelling it.
        for (int i = 1; i < 4; i++)
        {
            double y = Math.Round(height * i / 4.0) + 0.5;
            dc.DrawLine(Grid, new Point(0, y), new Point(width, y));
        }

        Draw(dc, EnemyLine, Enemy, 1.0);

        var group = Group;
        if (group is { Count: > 0 })
        {
            double most = group.Max();
            if (most > 0) Draw(dc, GroupLine, group.Select(v => v / most).ToArray(), 1.0);
        }
    }

    private void Draw(DrawingContext dc, Pen pen, IReadOnlyList<double>? line, double top)
    {
        if (line == null || line.Count < 2) return;

        double width = ActualWidth;
        double height = ActualHeight;

        var figure = new PathFigure { StartPoint = At(line, 0, width, height, top) };
        for (int i = 1; i < line.Count; i++)
        {
            figure.Segments.Add(new LineSegment(At(line, i, width, height, top), true));
        }

        var path = new PathGeometry();
        path.Figures.Add(figure);
        path.Freeze();

        dc.DrawGeometry(null, pen, path);
    }

    private static Point At(IReadOnlyList<double> line, int i, double width, double height, double top)
        => new(i / (double)(line.Count - 1) * width,
            height - Math.Clamp(line[i] / top, 0, 1) * (height - 2) - 1);

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
