using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using LogGrep.Export;
using LogGrep.Models;
using LogGrep.Parsing;
using Microsoft.Win32;

namespace LogGrep.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly Dictionary<string, EncounterViewModel> _groups = new(StringComparer.Ordinal);
    private ScanResult? _scan;
    private CancellationTokenSource? _cancellation;

    private string _logPath = string.Empty;
    private string _status = "Open a World of Warcraft combat log to begin.";
    private double _progress;
    private bool _isBusy;
    private bool _asSingleFile = true;

    public MainViewModel()
    {
        OpenCommand = new RelayCommand(Open, () => !IsBusy);
        ExportCommand = new RelayCommand(Export, () => !IsBusy && SelectedPullCount > 0);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        SelectAllCommand = new RelayCommand(() => SetAll(true), () => !IsBusy && Encounters.Count > 0);
        SelectNoneCommand = new RelayCommand(() => SetAll(false), () => !IsBusy && Encounters.Count > 0);
        ExpandAllCommand = new RelayCommand(() => SetExpanded(true), () => Encounters.Count > 0);
        CollapseAllCommand = new RelayCommand(() => SetExpanded(false), () => Encounters.Count > 0);
    }

    public ObservableCollection<EncounterViewModel> Encounters { get; } = new();

    public RelayCommand OpenCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectNoneCommand { get; }
    public RelayCommand ExpandAllCommand { get; }
    public RelayCommand CollapseAllCommand { get; }

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
            encounter = new EncounterViewModel(pull, RaiseSelectionChanged, message => Status = message);
            _groups[pull.GroupKey] = encounter;
            Encounters.Add(encounter);
            RaiseCommandStates();
        }

        encounter.Add(pull);
    }

    private void Cancel() => _cancellation?.Cancel();

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
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => (bytes / (double)(1L << 30)).ToString("0.0", CultureInfo.InvariantCulture) + " GB",
        >= 1L << 20 => (bytes / (double)(1L << 20)).ToString("0", CultureInfo.InvariantCulture) + " MB",
        _ => (bytes / 1024.0).ToString("0", CultureInfo.InvariantCulture) + " KB",
    };
}
