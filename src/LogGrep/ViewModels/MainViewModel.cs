using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using LogGrep.Analysis;
using LogGrep.Export;
using LogGrep.Models;
using LogGrep.Parsing;
using Microsoft.Win32;

namespace LogGrep.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly Dictionary<string, EncounterViewModel> _groups = new(StringComparer.Ordinal);
    private readonly ListCollectionView _encountersView;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly IFileSystem _fileSystem;
    private readonly LogExporter _exporter;
    private Reading _reading = Reading.Nothing;
    private CancellationTokenSource? _cancellation;

    private string _logPath = string.Empty;
    private string _status = "Open a World of Warcraft combat log to begin.";
    private double _progress;
    private bool _isBusy;
    private bool _asSingleFile = true;

    /// <summary>The real disk. Tests hand in a fake one instead.</summary>
    public MainViewModel() : this(new FileSystem())
    {
    }

    public MainViewModel(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _exporter = new LogExporter(fileSystem);

        OpenCommand = new RelayCommand(Open, () => !IsBusy);
        ExportCommand = new RelayCommand(Export, () => !IsBusy && SelectedPullCount > 0);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        SelectAllCommand = new RelayCommand(() => SetAll(true), () => !IsBusy && Encounters.Count > 0);
        SelectNoneCommand = new RelayCommand(() => SetAll(false), () => !IsBusy && Encounters.Count > 0);
        ExpandAllCommand = new RelayCommand(() => SetExpanded(true), () => Encounters.Count > 0);
        CollapseAllCommand = new RelayCommand(() => SetExpanded(false), () => Encounters.Count > 0);
        ExportFindingsCommand = new RelayCommand(ExportFindings, () => !IsBusy && Findings.Count > 0);

        _encountersView = new ListCollectionView(Encounters);
        Sorting.Encounters.Changed += (_, _) => _encountersView.CustomSort = Sorting.Encounters.Comparer;
        Sorting.Pulls.Changed += (_, _) => ApplySorting();
        Sorting.Players.Changed += (_, _) => ApplySorting();
    }

    public ObservableCollection<EncounterViewModel> Encounters { get; } = new();

    /// <summary>Sort state of all three tables, handed down to the encounter and pull rows.</summary>
    public Sorting Sorting { get; } = new();

    /// <summary>What the encounter rows are bound to; the collection keeps the order of the log.</summary>
    public ICollectionView EncountersView => _encountersView;

    /// <summary>
    /// Everything the detectors found, heaviest first. It is the whole list rather than a view of
    /// it, because what a person is shown is their own share of it and the rows work that out for
    /// themselves.
    /// </summary>
    public IReadOnlyList<Finding> Findings { get; private set; } = Array.Empty<Finding>();

    public bool HasFindings => Findings.Count > 0;

    /// <summary>The files that are open and what came out of them, joined.</summary>
    public Reading Reading => _reading;

    public string FindingsSummary
    {
        get
        {
            if (_reading.Sources.Count == 0) return "Open a log to look for mistakes.";
            if (Findings.Count == 0)
            {
                return "Nothing found. A mechanic only shows whose it is once it has been applied " +
                       "often enough, so a short attempt finds nothing - and so does one where the " +
                       "same mistake happened again and again, the wrong targets having become the " +
                       "majority within that single sample. More attempts of the same fight help.";
            }

            int players = Findings.Select(f => f.Player).Distinct().Count();
            return Findings.Count + (Findings.Count == 1 ? " mistake" : " mistakes") +
                   " across " + players + (players == 1 ? " player." : " players.");
        }
    }

    public RelayCommand OpenCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectNoneCommand { get; }
    public RelayCommand ExpandAllCommand { get; }
    public RelayCommand CollapseAllCommand { get; }
    public RelayCommand ExportFindingsCommand { get; }

    public string LogPath
    {
        get => _logPath;
        private set => Set(ref _logPath, value);
    }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public double Progress
    {
        get => _progress;
        private set => Set(ref _progress, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!Set(ref _isBusy, value)) return;
            RaiseCommandStates();
        }
    }

    public bool AsSingleFile
    {
        get => _asSingleFile;
        set => Set(ref _asSingleFile, value);
    }

    public int SelectedPullCount => _groups.Values.Sum(e => e.SelectedCount);

    public string SelectionText
    {
        get
        {
            int pulls = SelectedPullCount;
            return pulls == 0 ? "Nothing selected" : "Selected pulls: " + pulls;
        }
    }

    /// <summary>Opens a log that came from the command line or was dropped on the window.</summary>
    public void Load(string path) => _ = LoadAsync(path);

    /// <summary>The same, awaitable, which is how a test knows the scan has finished.</summary>
    public Task LoadAsync(params string[] paths)
    {
        // The same file named twice is one file. Reading it twice would cost a second pass over a
        // gigabyte and then have to be undone at the other end, and nothing is learned by it.
        var present = paths
            .Where(_fileSystem.File.Exists)
            .Select(_fileSystem.Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return IsBusy || present.Count == 0 ? Task.CompletedTask : ScanAsync(present);
    }

    private void Open()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose one or more combat log files",
            Filter = "WoW combat logs (*.txt)|*.txt|All files (*.*)|*.*",
            CheckFileExists = true,

            // A tier is fought over several nights and several files, and the app needs ten
            // attempts at a boss before it will say anything. One night's file often cannot reach
            // that on its own; the same three files read together clear it without trying.
            Multiselect = true,
        };

        if (dialog.ShowDialog() != true) return;
        _ = ScanAsync(dialog.FileNames);
    }

    private async Task ScanAsync(IReadOnlyList<string> paths)
    {
        Encounters.Clear();
        _groups.Clear();
        Findings = Array.Empty<Finding>();
        _reading = Reading.Nothing;
        LogPath = paths.Count == 1
            ? paths[0]
            : paths.Count + " logs, starting with " + _fileSystem.Path.GetFileName(paths[0]);
        Progress = 0;
        IsBusy = true;
        RaiseSelectionChanged();

        long size = paths.Sum(p => _fileSystem.FileInfo.New(p).Length);
        Status = "Reading " + FormatSize(size) + "…";

        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        var progress = new Reports(this);

        try
        {
            var started = DateTime.UtcNow;
            var reading = await Task.Run(() => Read(paths, progress, token), token);

            _reading = reading;
            Rebuild(reading.Pulls);
            BuildFindings(reading.Pulls);
            Progress = 100;
            var elapsed = DateTime.UtcNow - started;
            Status = reading.IsEmpty
                ? "No fights found (no ENCOUNTER_START / CHALLENGE_MODE_START)."
                : Describe(reading, elapsed);

            // Anything about the reading itself rather than about the fights: a file whose date
            // does not match what is inside it, attempts that were in two of the files at once.
            foreach (string note in reading.Notes) Status += "  " + note;
        }
        catch (OperationCanceledException)
        {
            Status = "Reading cancelled.";
        }
        catch (Exception ex)
        {
            Status = "Read error: " + ex.Message;
            Complain(ex.Message, "Could not read the log");
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
            IsBusy = false;
            RaiseSelectionChanged();
        }
    }

    /// <summary>
    /// Each file on its own, then joined. The scanner keeps no state between files, and the whole
    /// of the joining - the order, the duplicates, what to say about either - is in one place.
    /// </summary>
    private Reading Read(IReadOnlyList<string> paths, IProgress<ScanProgress> progress, CancellationToken ct)
    {
        var scans = new List<ScanResult>(paths.Count);
        var scanner = new CombatLogScanner(_fileSystem);

        var sizes = paths.Select(p => _fileSystem.FileInfo.New(p).Length).ToList();
        long total = sizes.Sum();
        long done = 0;

        for (int i = 0; i < paths.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            scans.Add(scanner.Scan(paths[i], new Share(progress, done, sizes[i], total), ct));
            done += sizes[i];
        }

        return Reading.Of(scans);
    }

    /// <summary>
    /// One file's progress as a share of all of them. A scanner only knows how far through its own
    /// file it is, and four files each reporting their own hundred percent would run the bar to the
    /// end four times.
    /// </summary>
    private sealed class Share : IProgress<ScanProgress>
    {
        private readonly IProgress<ScanProgress> _whole;
        private readonly long _before;
        private readonly long _size;
        private readonly long _total;

        public Share(IProgress<ScanProgress> whole, long before, long size, long total)
        {
            _whole = whole;
            _before = before;
            _size = size;
            _total = total;
        }

        public void Report(ScanProgress value)
        {
            double bytes = _before + value.Percent / 100.0 * _size;
            _whole.Report(new ScanProgress(_total > 0 ? bytes * 100.0 / _total : 0, value.Pull));
        }

    }
    private string Describe(Reading reading, TimeSpan elapsed)
    {
        string files = reading.Sources.Count == 1
            ? string.Empty
            : " across " + reading.Sources.Count + " files";

        return "Done: " + Encounters.Count + " encounters, " + reading.Pulls.Count + " pulls" + files +
               " in " + elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s.";
    }


    /// <summary>

    /// <summary>
    /// A dialog is only worth raising when there is an application showing windows. Under a test
    /// runner there is none, and a modal box there would hang the run behind something nobody is
    /// looking at. The status line carries the same message either way.
    /// </summary>
    private static void Complain(string message, string title)
    {
        if (Application.Current == null) return;
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
    /// Runs the action on the thread that built the collection views, which is the only thread
    /// allowed to change what they are watching. A scan reports from a worker, and the thread its
    /// continuations land on depends on whichever synchronization context happened to be current
    /// when the scan started - which, early in startup, can be none at all.
    /// </summary>
    private void OnUi(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.Invoke(action);
    }

    /// <summary>Marshals scan reports onto that same thread, whatever context the scan began under.</summary>
    private sealed class Reports : IProgress<ScanProgress>
    {
        private readonly MainViewModel _owner;

        public Reports(MainViewModel owner) => _owner = owner;

        public void Report(ScanProgress value) => _owner.OnUi(() => _owner.OnScanProgress(value));
    }
    private void OnScanProgress(ScanProgress report)
    {
        Progress = report.Percent;
        if (report.Pull is { } pull) Place(pull);
    }

    private void Place(PullRecord pull)
    {
        if (!_groups.TryGetValue(pull.GroupKey, out var encounter))
        {
            encounter = new EncounterViewModel(pull, Sorting, RaiseSelectionChanged, message => Status = message);
            _groups[pull.GroupKey] = encounter;
            Encounters.Add(encounter);
            RaiseCommandStates();
        }

        encounter.Add(pull);
    }

    /// <summary>
    /// The tree fills as the scan runs, so a large log shows its fights while it is still being
    /// read. What arrives that way is one file at a time, in the order the files were handed over,
    /// and it holds twice over whatever a pair of overlapping files both contain. Once the reading
    /// is joined the tree is built again from it - the version that is right rather than early.
    /// </summary>
    private void Rebuild(IReadOnlyList<PullRecord> pulls)
    {
        Encounters.Clear();
        _groups.Clear();

        foreach (var pull in pulls) Place(pull);
    }

    private void Cancel() => _cancellation?.Cancel();

    /// <summary>Pushes the pull and player sort into every table that has already been built.</summary>
    private void ApplySorting()
    {
        foreach (var encounter in Encounters) encounter.ApplySorting();
    }

    private void SetAll(bool selected)
    {
        foreach (var encounter in Encounters) encounter.SetAll(selected);
        RaiseSelectionChanged();
    }

    private void SetExpanded(bool expanded)
    {
        foreach (var encounter in Encounters)
        {
            if (encounter.IsExpandable) encounter.IsExpanded = expanded;
        }
    }

    private void Export()
    {
        var pulls = Encounters
            .SelectMany(e => e.Pulls)
            .Where(p => p.IsSelected)
            .Select(p => p.Record)
            .ToList();

        if (pulls.Count == 0) return;

        // The selection can now straddle several files, so the names offered are the first file's.
        // Each attempt still goes back to its own source when the bytes are copied.
        var first = _reading.Sources.Count > 0 ? _reading.Sources[0] : pulls[0].Source;
        string stem = _fileSystem.Path.GetFileNameWithoutExtension(first.Path);
        string folder = _fileSystem.Path.GetDirectoryName(first.Path) ?? string.Empty;
        if (AsSingleFile)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save the selected pulls into one file",
                Filter = "WoW combat logs (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt",
                FileName = stem + "_export.txt",
                InitialDirectory = folder,
            };

            if (dialog.ShowDialog() != true) return;
            RunExport(() =>
            {
                _exporter.ExportSingle(pulls, dialog.FileName);
                return "Wrote " + pulls.Count + " pulls → " + dialog.FileName;
            });
        }
        else
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Folder for the per-pull files",
                InitialDirectory = folder,
            };

            if (dialog.ShowDialog() != true) return;
            RunExport(() =>
            {
                var files = _exporter.ExportSeparate(pulls, dialog.FolderName);
                return "Wrote " + files.Count + " files → " + dialog.FolderName;
            });
        }
    }

    private async void RunExport(Func<string> work)
    {
        IsBusy = true;
        Status = "Exporting…";
        try
        {
            Status = await Task.Run(work);
        }
        catch (Exception ex)
        {
            Status = "Export error: " + ex.Message;
            Complain(ex.Message, "Export failed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RaiseSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedPullCount));
        OnPropertyChanged(nameof(SelectionText));
        ExportCommand.RaiseCanExecuteChanged();
    }

    private void RaiseCommandStates()
    {
        OpenCommand.RaiseCanExecuteChanged();
        ExportCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        SelectAllCommand.RaiseCanExecuteChanged();
        SelectNoneCommand.RaiseCanExecuteChanged();
        ExpandAllCommand.RaiseCanExecuteChanged();
        CollapseAllCommand.RaiseCanExecuteChanged();
        ExportFindingsCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Works out which mechanics belong to which role and collects the times somebody else took
    /// one. It runs on what the scan already gathered, so the file is not read a second time.
    /// </summary>
    private void BuildFindings(IReadOnlyList<PullRecord> pulls)
    {
        Findings = Analysis.Findings.In(pulls);

        // Pushed down the tree so every row can show its own share of them.
        var byPull = Findings.ToLookup(f => f.Pull);
        foreach (var encounter in Encounters) encounter.ApplyMistakes(byPull);

        OnPropertyChanged(nameof(Findings));
        OnPropertyChanged(nameof(HasFindings));
        OnPropertyChanged(nameof(FindingsSummary));
        ExportFindingsCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Writes the findings out as plain text. The whole point of this app is handing a fight to a
    /// chat, and a list of mistakes travels better as text than as a screenshot of a table.
    /// </summary>
    private void ExportFindings()
    {
        if (_reading.Sources.Count == 0 || Findings.Count == 0) return;

        var first = _reading.Sources[0];
        var dialog = new SaveFileDialog
        {
            Title = "Save the findings",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = _fileSystem.Path.GetFileNameWithoutExtension(first.Path) + "_findings.txt",
            InitialDirectory = _fileSystem.Path.GetDirectoryName(first.Path) ?? string.Empty,
        };

        if (dialog.ShowDialog() != true) return;

        var text = new StringBuilder();
        text.AppendLine("Mistakes found in " + string.Join(", ", _reading.Sources.Select(s => s.Name)));
        text.AppendLine();

        foreach (var player in Findings.GroupBy(f => f.Player).OrderByDescending(g => g.Sum(f => f.Cost.Weight)))
        {
            text.AppendLine(PlayerName.Format(player.Key));

            foreach (var finding in player)
            {
                text.AppendLine("  " + finding.Line + "  (pull " + finding.PullNumber + ")");
                text.AppendLine("    " + finding.Evidence);
                text.AppendLine("    " + finding.Cost.Text);
                text.AppendLine("    " + finding.Advice);
            }

            text.AppendLine();
        }

        try
        {
            _fileSystem.File.WriteAllText(dialog.FileName, text.ToString());
            Status = "Wrote the findings → " + dialog.FileName;
        }
        catch (Exception ex)
        {
            Status = "Export error: " + ex.Message;
            Complain(ex.Message, "Export failed");
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => (bytes / (double)(1L << 30)).ToString("0.0", CultureInfo.InvariantCulture) + " GB",
        >= 1L << 20 => (bytes / (double)(1L << 20)).ToString("0", CultureInfo.InvariantCulture) + " MB",
        _ => (bytes / 1024.0).ToString("0", CultureInfo.InvariantCulture) + " KB",
    };
}
