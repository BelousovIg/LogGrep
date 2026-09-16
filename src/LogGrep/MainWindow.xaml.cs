using System.IO;
using System.Windows;
using System.Windows.Media;
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
