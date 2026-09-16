using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
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
    private readonly ListCollectionView _rulesView;
    private ScanResult? _scan;
    private CancellationTokenSource? _cancellation;

    private string _logPath = string.Empty;
    private string _status = "Open a World of Warcraft combat log to begin.";
    private double _progress;
    private bool _isBusy;
    private bool _asSingleFile = true;
    private bool _showFindings;

    public MainViewModel()
    {
        OpenCommand = new RelayCommand(Open, () => !IsBusy);
        ExportCommand = new RelayCommand(Export, () => !IsBusy && SelectedPullCount > 0);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        SelectAllCommand = new RelayCommand(() => SetAll(true), () => !IsBusy && Encounters.Count > 0);
        SelectNoneCommand = new RelayCommand(() => SetAll(false), () => !IsBusy && Encounters.Count > 0);
        ExpandAllCommand = new RelayCommand(() => SetExpanded(true), () => Encounters.Count > 0);
        CollapseAllCommand = new RelayCommand(() => SetExpanded(false), () => Encounters.Count > 0);
        ExportFindingsCommand = new RelayCommand(ExportFindings, () => !IsBusy && Rules.Count > 0);

        _encountersView = new ListCollectionView(Encounters);
        _rulesView = new ListCollectionView(Rules) { CustomSort = new RuleOrder() };
        Sorting.Encounters.Changed += (_, _) => _encountersView.CustomSort = Sorting.Encounters.Comparer;
        Sorting.Pulls.Changed += (_, _) => ApplySorting();
        Sorting.Players.Changed += (_, _) => ApplySorting();
    }

    public ObservableCollection<EncounterViewModel> Encounters { get; } = new();

    /// <summary>Sort state of all three tables, handed down to the encounter and pull rows.</summary>
    public Sorting Sorting { get; } = new();

    /// <summary>What the encounter rows are bound to; the collection keeps the order of the log.</summary>
    public ICollectionView EncountersView => _encountersView;

    /// <summary>Mechanics taken by the wrong role, grouped by the rule that caught them.</summary>
    public ObservableCollection<RuleViewModel> Rules { get; } = new();

    /// <summary>Rules in order of weight, with the ignored ones pushed to the bottom.</summary>
    public ICollectionView RulesView => _rulesView;

    /// <summary>Whether the window is showing the findings instead of the pulls.</summary>
    public bool ShowFindings
    {
        get => _showFindings;
        set
        {
            if (Set(ref _showFindings, value)) ExportFindingsCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasFindings => Rules.Count > 0;

    public string FindingsSummary
    {
        get
        {
            if (_scan == null) return "Open a log to look for mechanics taken by the wrong role.";
            if (Rules.Count == 0)
            {
                return "Nothing found. A mechanic is recognised by how it behaves across several " +
                       "attempts of the same fight, so one or two pulls give nothing to compare.";
            }

            int players = Rules.Sum(r => r.Findings.Count);
            return players + (players == 1 ? " time a mechanic" : " times a mechanic") +
                   " was taken by the wrong role, across " + Rules.Count +
                   (Rules.Count == 1 ? " mechanic." : " mechanics.");
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
    public void Load(string path)
    {
        if (IsBusy || !File.Exists(path)) return;
        _ = ScanAsync(path);
    }

    private void Open()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a combat log file",
            Filter = "WoW combat logs (*.txt)|*.txt|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != true) return;
        _ = ScanAsync(dialog.FileName);
    }

    private async Task ScanAsync(string path)
    {
        Encounters.Clear();
        _groups.Clear();
        Rules.Clear();
        _scan = null;
        LogPath = path;
        Progress = 0;
        IsBusy = true;
        RaiseSelectionChanged();

        long size = new FileInfo(path).Length;
        Status = "Reading " + FormatSize(size) + "…";

        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        var progress = new Progress<ScanProgress>(OnScanProgress);

        try
        {
            var started = DateTime.UtcNow;
            var scanner = new CombatLogScanner();
            var result = await Task.Run(() => scanner.Scan(path, progress, token), token);

            _scan = result;
            BuildFindings(result.Pulls);
            Progress = 100;
            var elapsed = DateTime.UtcNow - started;
            Status = result.Pulls.Count == 0
                ? "No fights found in this log (no ENCOUNTER_START / CHALLENGE_MODE_START)."
                : "Done: " + Encounters.Count + " encounters, " + result.Pulls.Count + " pulls in " +
                  elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s.";
        }
        catch (OperationCanceledException)
        {
            Status = "Reading cancelled.";
        }
        catch (Exception ex)
        {
            Status = "Read error: " + ex.Message;
            MessageBox.Show(ex.Message, "Could not read the log", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
            IsBusy = false;
            RaiseSelectionChanged();
        }
    }

    private void OnScanProgress(ScanProgress report)
    {
        Progress = report.Percent;
        if (report.Pull is not { } pull) return;

        if (!_groups.TryGetValue(pull.GroupKey, out var encounter))
        {
            encounter = new EncounterViewModel(pull, Sorting, RaiseSelectionChanged);
            _groups[pull.GroupKey] = encounter;
            Encounters.Add(encounter);
            RaiseCommandStates();
        }

        encounter.Add(pull);
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
        if (_scan is not { } scan) return;

        var pulls = Encounters
            .SelectMany(e => e.Pulls)
            .Where(p => p.IsSelected)
            .Select(p => p.Record)
            .OrderBy(p => p.StartOffset)
            .ToList();

        if (pulls.Count == 0) return;

        string stem = Path.GetFileNameWithoutExtension(scan.FilePath);
        if (AsSingleFile)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save the selected pulls into one file",
                Filter = "WoW combat logs (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt",
                FileName = stem + "_export.txt",
                InitialDirectory = Path.GetDirectoryName(scan.FilePath) ?? string.Empty,
            };

            if (dialog.ShowDialog() != true) return;
            RunExport(() =>
            {
                LogExporter.ExportSingle(scan, pulls, dialog.FileName);
                return "Wrote " + pulls.Count + " pulls → " + dialog.FileName;
            });
        }
        else
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Folder for the per-pull files",
                InitialDirectory = Path.GetDirectoryName(scan.FilePath) ?? string.Empty,
            };

            if (dialog.ShowDialog() != true) return;
            RunExport(() =>
            {
                var files = LogExporter.ExportSeparate(scan, pulls, dialog.FolderName);
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
            MessageBox.Show(ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
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
        Rules.Clear();

        foreach (var group in MechanicAnalyzer.Analyse(pulls).GroupBy(f => f.Rule))
        {
            Rules.Add(new RuleViewModel(group.Key, group, () => _rulesView.Refresh()));
        }

        _rulesView.Refresh();
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
        if (_scan is not { } scan || Rules.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Title = "Save the findings",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = Path.GetFileNameWithoutExtension(scan.FilePath) + "_findings.txt",
            InitialDirectory = Path.GetDirectoryName(scan.FilePath) ?? string.Empty,
        };

        if (dialog.ShowDialog() != true) return;

        var text = new StringBuilder();
        text.AppendLine("Mechanics taken by the wrong role");
        text.AppendLine("Log: " + scan.FilePath);
        text.AppendLine();

        foreach (RuleViewModel rule in _rulesView)
        {
            if (rule.IsIgnored) continue;

            text.AppendLine(rule.Encounter + " — " + rule.Title);
            text.AppendLine("  " + rule.Evidence);
            foreach (var finding in rule.Findings)
            {
                text.AppendLine("  " + finding.Player + " (" + finding.ClassName + " " + finding.SpecName +
                                ", " + finding.RoleName + ") — " + finding.PullText + " at " + finding.AtText);
            }

            text.AppendLine();
        }

        try
        {
            File.WriteAllText(dialog.FileName, text.ToString());
            Status = "Wrote the findings → " + dialog.FileName;
        }
        catch (Exception ex)
        {
            Status = "Export error: " + ex.Message;
            MessageBox.Show(ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Heaviest rules first; an ignored one drops to the bottom instead of disappearing.</summary>
    private sealed class RuleOrder : IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (x is not RuleViewModel left || y is not RuleViewModel right) return 0;

            if (left.IsIgnored != right.IsIgnored) return left.IsIgnored ? 1 : -1;

            int byCount = right.Findings.Count.CompareTo(left.Findings.Count);
            if (byCount != 0) return byCount;

            int byShare = right.Rule.Share.CompareTo(left.Rule.Share);
            return byShare != 0 ? byShare : string.Compare(left.Title, right.Title, StringComparison.Ordinal);
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => (bytes / (double)(1L << 30)).ToString("0.0", CultureInfo.InvariantCulture) + " GB",
        >= 1L << 20 => (bytes / (double)(1L << 20)).ToString("0", CultureInfo.InvariantCulture) + " MB",
        _ => (bytes / 1024.0).ToString("0", CultureInfo.InvariantCulture) + " KB",
    };
}
