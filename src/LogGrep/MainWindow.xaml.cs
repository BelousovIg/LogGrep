using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LogGrep.Controls;
using LogGrep.Interop;
using LogGrep.ViewModels;

namespace LogGrep;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private MainViewModel Model => (MainViewModel)DataContext;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this,
            caption: Brush("PanelBrush"),
            border: Brush("LineBrush"),
            text: Brush("FgBrush"));
    }

    private Color Brush(string key) => ((SolidColorBrush)FindResource(key)).Color;

    /// <summary>Lets the log be passed on the command line, or dropped on the exe.</summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        string? path = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(File.Exists);
        if (path != null) Model.Load(path);
    }


    /// <summary>
    /// Copies the full name of the player that was clicked, and acknowledges it where the click
    /// landed rather than in the status bar at the bottom - the eye is on the row, not down there.
    /// </summary>
    private void OnCopyPlayerName(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement cell || cell.DataContext is not PlayerRowViewModel player) return;
        if (!player.CopyName()) return;

        CopyToast.ShowAt(this, Mouse.GetPosition(this));
    }
    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedFile(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (TryGetDroppedFile(e) is { } path) Model.Load(path);
        e.Handled = true;
    }

    private static string? TryGetDroppedFile(DragEventArgs e)
        => e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files
            ? files.FirstOrDefault(File.Exists)
            : null;
}
