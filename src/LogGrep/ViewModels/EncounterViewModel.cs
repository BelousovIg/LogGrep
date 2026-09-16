using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using LogGrep.Models;

namespace LogGrep.ViewModels;

/// <summary>A boss (grouped over all of its attempts) or a single keystone run.</summary>
public sealed class EncounterViewModel : ObservableObject
{
    private readonly Action _selectionChanged;
    private readonly Action<string> _report;
    private readonly ListCollectionView _pullsView;
    private bool _isExpanded;
    private bool? _isChecked = false;
    private bool _suppressBubbling;

    public EncounterViewModel(PullRecord first, Sorting sorting, Action selectionChanged, Action<string> report)
    {
        _selectionChanged = selectionChanged;
        _report = report;
        Sorting = sorting;
        Name = first.EncounterName;
        Kind = first.Kind;
        DifficultyText = first.DifficultyText;
        KeystoneLevel = first.KeystoneLevel;

        _pullsView = new ListCollectionView(Pulls) { CustomSort = sorting.Pulls.Comparer };
    }

    public string Name { get; }

    public ContentKind Kind { get; }

    public string DifficultyText { get; }

    public int KeystoneLevel { get; }

    /// <summary>Shared with the window so the pull and player headers can drive the sort.</summary>
    public Sorting Sorting { get; }

    public ObservableCollection<PullViewModel> Pulls { get; } = new();

    /// <summary>What the pull rows are bound to; the collection itself keeps its natural order.</summary>
    public ICollectionView PullsView => _pullsView;

    public string PullCountText => Pulls.Count.ToString();

    public bool HasKill => Pulls.Any(p => p.Record.Success);

    public string KillText => HasKill ? "win" : "wiped";

    /// <summary>Largest group size seen, the value the party size column sorts on.</summary>
    public int PartySizeKey
    {
        get
        {
            int max = 0;
            foreach (var pull in Pulls)
            {
                if (pull.Record.Participants > max) max = pull.Record.Participants;
            }

            return max;
        }
    }

    /// <summary>Smallest and largest group size seen across the attempts.</summary>
    public string PartySizeText
    {
        get
        {
            var sizes = Pulls.Select(p => p.Record.Participants).Where(s => s > 0).ToList();
            if (sizes.Count == 0) return "—";
            int min = sizes.Min();
            int max = sizes.Max();
            return min == max ? min.ToString() : min + "–" + max;
        }
    }

    /// <summary>Raids always expand; anything else only when it actually has several attempts.</summary>
    public bool IsExpandable => Kind == ContentKind.Raid || Pulls.Count > 1;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    /// <summary>Tri-state: all attempts selected, none, or some.</summary>
    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            bool target = value ?? true; // clicking an indeterminate box selects everything
            _suppressBubbling = true;
            foreach (var pull in Pulls) pull.SetSelectedSilently(target);
            _suppressBubbling = false;
            OnPullSelectionChanged();
        }
    }

    public int SelectedCount => Pulls.Count(p => p.IsSelected);

    /// <summary>Carries a one-line message from a row up to the status bar.</summary>
    public void Report(string message) => _report(message);

    public void Add(PullRecord record)
    {
        var pull = new PullViewModel(record, this);
        if (_isChecked == true) pull.SetSelectedSilently(true);
        Pulls.Add(pull);

        OnPropertyChanged(nameof(PullCountText));
        OnPropertyChanged(nameof(HasKill));
        OnPropertyChanged(nameof(KillText));
        OnPropertyChanged(nameof(PartySizeText));
        OnPropertyChanged(nameof(PartySizeKey));
        OnPropertyChanged(nameof(IsExpandable));
        RefreshCheckState();
    }

    public void SetAll(bool selected)
    {
        _suppressBubbling = true;
        foreach (var pull in Pulls) pull.SetSelectedSilently(selected);
        _suppressBubbling = false;
        OnPullSelectionChanged();
    }

    /// <summary>Re-orders the pulls, and the players of every pull whose table has been opened.</summary>
    public void ApplySorting()
    {
        _pullsView.CustomSort = Sorting.Pulls.Comparer;
        foreach (var pull in Pulls) pull.ApplyPlayerSorting();
    }

    internal void OnPullSelectionChanged()
    {
        RefreshCheckState();
        if (!_suppressBubbling) _selectionChanged();
    }

    private void RefreshCheckState()
    {
        int selected = SelectedCount;
        bool? state = selected == 0 ? false : selected == Pulls.Count ? true : null;
        if (_isChecked != state)
        {
            _isChecked = state;
            OnPropertyChanged(nameof(IsChecked));
        }

        OnPropertyChanged(nameof(SelectedCount));
    }
}
