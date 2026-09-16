using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using LogGrep.Analysis;
using LogGrep.Models;

namespace LogGrep.ViewModels;

/// <summary>One attempt inside an encounter row.</summary>
public sealed class PullViewModel : ObservableObject
{
    private bool _isSelected;
    private bool _isExpanded;
    private ListCollectionView? _playersView;
    private IReadOnlyList<Finding> _mistakes = Array.Empty<Finding>();

    public PullViewModel(PullRecord record, EncounterViewModel owner)
    {
        Record = record;
        Owner = owner;
    }

    public PullRecord Record { get; }

    public EncounterViewModel Owner { get; }


    /// <summary>Every mistake made in this attempt, counting each player's separately.</summary>
    public int MistakeCount => _mistakes.Count;

    public bool HasMistakes => _mistakes.Count > 0;

    public string MistakesText => _mistakes.Count == 0 ? "—" : _mistakes.Count.ToString();

    /// <summary>
    /// Hands the attempt what the analysis found. The player rows are dropped rather than patched:
    /// they are built on demand anyway, and nothing has opened them this early in a scan.
    /// </summary>
    internal void SetMistakes(IEnumerable<Finding> mistakes)
    {
        _mistakes = mistakes.OrderBy(f => f.At).ToArray();
        _playersView = null;

        OnPropertyChanged(nameof(MistakeCount));
        OnPropertyChanged(nameof(HasMistakes));
        OnPropertyChanged(nameof(MistakesText));
        OnPropertyChanged(nameof(PlayersView));
    }
    /// <summary>Shared sort state, reached through the owner so the player headers can bind to it.</summary>
    public Sorting Sorting => Owner.Sorting;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value)) Owner.OnPullSelectionChanged();
        }
    }

    /// <summary>Whether the per-player table under this attempt is open.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public bool HasPlayers => Record.Roster.Count > 0;

    /// <summary>
    /// The per-player table, built the first time it is shown. A log can hold hundreds of pulls
    /// and most are never opened, so there is no point building all of them up front.
    /// </summary>
    public ICollectionView PlayersView => _playersView ??= CreatePlayersView();

    /// <summary>Re-orders the player rows, but only for a table that has actually been opened.</summary>
    public void ApplyPlayerSorting()
    {
        if (_playersView != null) _playersView.CustomSort = Sorting.Players.Comparer;
    }

    /// <summary>Sets the flag without bubbling back up, used when the parent drives the change.</summary>
    internal void SetSelectedSilently(bool value)
    {
        if (_isSelected == value) return;
        _isSelected = value;
        OnPropertyChanged(nameof(IsSelected));
    }

    public bool IsSuccess => Record.Success;

    public string ResultText => Record.Success ? "win" : "wiped";

    public string StartText => Record.StartTime.ToString("MMM dd HH:mm:ss", CultureInfo.InvariantCulture);

    public string DurationText => Display.Duration(Record.Duration);

    public string ParticipantsText => Record.Participants > 0 ? Record.Participants.ToString() : "—";

    public string DpsText => Display.Rate(Record.Dps);

    public string HpsText => Display.Rate(Record.Hps);


    /// <summary>
    /// The order a group is read in: the tanks, then the healers, then everyone else. It is how a
    /// raid is talked about, and it keeps the two rows that matter most out of the middle of a
    /// list of twenty.
    /// </summary>
    private static int Group(PlayerRowViewModel player) => player.IsTank ? 0 : player.IsHealer ? 1 : 2;

    /// <summary>Within a group, by what that group is there to do: healing for the healers, damage for the rest.</summary>
    private static double Score(PlayerRowViewModel player) => player.IsHealer ? player.HpsValue : player.DpsValue;

    private ListCollectionView CreatePlayersView()
    {
        var mistakes = _mistakes.ToLookup(m => m.Player, StringComparer.Ordinal);
        var rows = Record.Roster
            .Select(stats => new PlayerRowViewModel(stats, Record.Duration, Owner.Report, mistakes[stats.Name].ToArray()))
            .OrderBy(Group)
            .ThenByDescending(Score)
            .ThenBy(p => p.Name, StringComparer.CurrentCulture)
            .ToList();

        return new ListCollectionView(rows) { CustomSort = Sorting.Players.Comparer };
    }
}
