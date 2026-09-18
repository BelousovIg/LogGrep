using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using LogGrep.Controls;
using LogGrep.Interop;
using LogGrep.Models;
using LogGrep.ViewModels;

namespace LogGrep;

public partial class MainWindow : Window
{
    /// <summary>
    /// How often the open logs are looked at to see whether they have grown.
    ///
    /// Five seconds, because the answer only has to arrive before somebody thinks to ask for it, and
    /// the question is one file length per open log. A watcher on the folder would be cheaper to
    /// wait on and far more expensive to serve: the game writes to it several times a second all
    /// evening, and every one of those would wake this up.
    /// </summary>
    private static readonly TimeSpan Look = TimeSpan.FromSeconds(5);

    private readonly DispatcherTimer _clock = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        _clock.Interval = Look;
        _clock.Tick += (_, _) => Model.CheckForNewContent();
    }

    /// <summary>Reads whatever has been added to the open logs since they were last read.</summary>
    private void OnRefreshLogs(object sender, RoutedEventArgs e) => _ = Model.RefreshAsync();

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

        _clock.Start();
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

    /// <summary>Climbs back up the trail to whichever piece of it was clicked.</summary>
    private void OnClimbTo(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Crumb crumb }) Model.Analysis.GoTo(crumb.Depth);
    }

    /// <summary>
    /// The side buttons of a mouse, which every other window on the machine uses for this. Handled
    /// as a preview so a button press anywhere in the window works, rather than only over whatever
    /// happens not to swallow it.
    /// </summary>
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);

        switch (e.ChangedButton)
        {
            case MouseButton.XButton1 when Model.BackCommand.CanExecute(null):
                Model.BackCommand.Execute(null);
                e.Handled = true;
                break;
            case MouseButton.XButton2 when Model.ForwardCommand.CanExecute(null):
                Model.ForwardCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// The two selectors. Narrowing the sample is a claim about which attempts count, so it re-runs
    /// the rules over exactly what is left - the numbers and the sentence above them always agree.
    /// </summary>
    private void OnAllAttempts(object sender, RoutedEventArgs e) => Model.Analysis.Narrow(Outcome.All);

    private void OnKillsOnly(object sender, RoutedEventArgs e) => Model.Analysis.Narrow(Outcome.Kills);

    private void OnWipesOnly(object sender, RoutedEventArgs e) => Model.Analysis.Narrow(Outcome.Wipes);

    private void OnOnlyTanks(object sender, RoutedEventArgs e) => Model.Analysis.Narrow(Role.Tank);

    private void OnOnlyHealers(object sender, RoutedEventArgs e) => Model.Analysis.Narrow(Role.Healer);

    private void OnOnlyDamage(object sender, RoutedEventArgs e) => Model.Analysis.Narrow(Role.Damage);

    private void OnAnalyseSelected(object sender, RoutedEventArgs e) => Model.AnalyseSelected();

    /// <summary>Puts the chart's window back to the whole attempt, and the rates with it.</summary>
    private void OnResetWindow(object sender, RoutedEventArgs e) => Model.Analysis.Pull?.ResetWindow();

    /// <summary>
    /// Opens one cell of the report grid: that person, in that attempt. A row click would change who
    /// and a column click when; a cell is both at once, which is the shortest way in.
    /// </summary>
    private void OnOpenCell(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GridCell { Present: true } cell })
        {
            Model.Analysis.Open(cell.Attempt, cell.Player);
        }
    }

    /// <summary>
    /// Opens one attempt from the row of tags above the grid. The same thing a cell click does,
    /// less the person: a tag is about the attempt and nobody in particular.
    /// </summary>
    private void OnOpenAttemptTag(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AttemptTag tag })
        {
            Model.Analysis.Open(tag.Index, string.Empty);
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
