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

        // A file on the command line is what somebody asked for now; the list is what they were
        // reading last time. Asking for one replaces neither - it is added to the other.
        if (path != null) Model.Load(path);
        else _ = Model.RestoreAsync();
    }

    /// <summary>Opens the settings, which is where a key and the folder everything lives in are set.</summary>
    private void OnOpenSettings(object sender, RoutedEventArgs e)
        => new SettingsWindow { Owner = this }.ShowDialog();

    /// <summary>
    /// Opens the report on whatever row the button sits in. The same gesture at all three levels of
    /// the tree, and the same rule behind it: the sample is the encounter entire, and what was
    /// clicked is only what the screen is pointed at.
    /// </summary>
    private void OnAnalyse(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;

        switch (element.DataContext)
        {
            case EncounterViewModel encounter:
                Model.Analyse(encounter);
                break;
            case PullViewModel pull:
                Model.Analyse(pull.Owner, pull);
                break;
            case PlayerRowViewModel player when element.Tag is PullViewModel inside:
                Model.Analyse(inside.Owner, inside, player.RawName);
                break;
        }
    }

    /// <summary>Takes one file out of the list, which re-reads whatever is left of it.</summary>
    private void OnRemoveLog(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LogRowViewModel log } && DataContext is MainViewModel main)
        {
            _ = main.RemoveAsync(log);
        }
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
