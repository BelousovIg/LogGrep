using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace LogGrep.Controls;

/// <summary>
/// The small "copied" bubble that appears under the cursor and fades out. It is deliberately not
/// hit testable and never waits for a click: it confirms something that already happened, and the
/// person is on their way to the next row before it has gone.
/// </summary>
public static class CopyToast
{
    private static readonly TimeSpan Life = TimeSpan.FromSeconds(1);

    /// <summary>Shows the bubble just below a point given in <paramref name="owner"/> coordinates.</summary>
    public static void ShowAt(UIElement owner, Point where, string text = "copied")
    {
        var bubble = new Border
        {
            Child = new TextBlock { Text = text },
            Style = Application.Current?.TryFindResource("ToastBubble") as Style,
        };

        var popup = new Popup
        {
            Child = bubble,
            PlacementTarget = owner,
            Placement = PlacementMode.Relative,
            HorizontalOffset = where.X - 12,
            VerticalOffset = where.Y + 16,
            AllowsTransparency = true,
            IsHitTestVisible = false,
            StaysOpen = true,
        };

        popup.IsOpen = true;

        // Holds its colour for the first part of the second and then goes quickly, which reads as
        // an acknowledgement rather than as something drifting off the screen.
        var fade = new DoubleAnimation(1, 0, Life)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
        };

        fade.Completed += (_, _) =>
        {
            popup.IsOpen = false;
            popup.Child = null;
        };

        bubble.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}
