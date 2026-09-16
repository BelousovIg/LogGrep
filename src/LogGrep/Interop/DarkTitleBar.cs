using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LogGrep.Interop;

/// <summary>
/// Paints the non-client area (title bar, borders) to match the dark theme.
/// Everything here is best effort: older Windows builds simply ignore the attributes.
/// </summary>
internal static class DarkTitleBar
{
    private const int UseImmersiveDarkMode = 20;
    private const int BorderColor = 34;
    private const int CaptionColor = 35;
    private const int TextColor = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window, Color caption, Color border, Color text)
    {
        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;

        Set(handle, UseImmersiveDarkMode, 1);
        Set(handle, CaptionColor, ToColorRef(caption));
        Set(handle, BorderColor, ToColorRef(border));
        Set(handle, TextColor, ToColorRef(text));
    }

    private static void Set(IntPtr handle, int attribute, int value)
    {
        try
        {
            DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // Pre-Vista or a stripped down Windows; nothing to do.
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    /// <summary>COLORREF is 0x00BBGGRR, the reverse of the usual RGB order.</summary>
    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);
}
