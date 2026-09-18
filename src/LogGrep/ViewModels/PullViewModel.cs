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
    private Scorecards? _cards;
    private IReadOnlyList<TimeSpan> _collective = Array.Empty<TimeSpan>();
    private Role? _role;

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

    public string MistakesText => _mistakes.Count == 0 ? "—" : Display.Count(_mistakes.Count);

    /// <summary>
    /// Narrows the roster to one role, or back to all of it. A role belongs to an attempt rather
    /// than to a person, so this picks rows out of this attempt and says nothing about anybody's
    /// evening.
    /// </summary>
    internal void SetLens(Role? role)
    {
        if (_role == role) return;

        _role = role;
        _playersView = null;
        OnPropertyChanged(nameof(PlayersView));
    }

    /// <summary>
    /// Hands the attempt what the analysis found. The player rows are dropped rather than patched:
    /// they are built on demand anyway, and nothing has opened them this early in a scan.
    /// </summary>
    internal void SetMistakes(IEnumerable<Finding> mistakes, Scorecards? cards = null)
    {
        _mistakes = mistakes.OrderBy(f => f.At).ToArray();
        _cards = cards;
        _collective = Analysis.Collective.In(Record, _mistakes);
        _playersView = null;

        OnPropertyChanged(nameof(MistakeCount));
        OnPropertyChanged(nameof(HasMistakes));
        OnPropertyChanged(nameof(MistakesText));
        OnPropertyChanged(nameof(PlayersView));
        OnPropertyChanged(nameof(Axes));
        OnPropertyChanged(nameof(HasCard));
        OnPropertyChanged(nameof(PoolsText));
        OnPropertyChanged(nameof(Blame));
    }

    /// <summary>The attempt's own card - one number, then the axes under it.</summary>
    public IReadOnlyList<Score> Axes => _cards?.For(Record).Axes ?? Array.Empty<Score>();

    public bool HasCard => _cards != null;

    /// <summary>What the attempt cost, in health pools, over everybody in it.</summary>
    public string PoolsText => _cards == null
        ? "—"
        : Display.Decimal(_cards.For(Record).Pools) + " health pools lost";

    /// <summary>
    /// Who it went to. Not an average of the players - an average hides the one person who lost the
    /// attempt behind nineteen who did not - but the loss itself, named and sorted.
    /// </summary>
    public IReadOnlyList<BlameViewModel> Blame
    {
        get
        {
            if (_cards == null) return Array.Empty<BlameViewModel>();

            return Record.Roster
                .Select(p => new { Player = p, Card = _cards.For(Record, p) })
                .Where(e => e.Card.Pools > 0.01)
                .OrderByDescending(e => e.Card.Pools)
                .Take(5)
                .Select(e => new BlameViewModel(
                    PlayerName.Character(e.Player.Name),
                    Display.Decimal(e.Card.Pools) + " pools",
                    e.Card.Worst?.Headline ?? string.Empty))
                .ToArray();
        }
    }

    /// <summary>The moments one thing caught much of the group, drawn as a band on the enemy's lane.</summary>
    public IReadOnlyList<TimeSpan> Collective => _collective;

    /// <summary>What the enemy cast, so a cause can be seen standing over its consequence.</summary>
    public IReadOnlyList<LaneMark> EnemyMarks => LaneMark.Enemy(Record);

    public double Seconds => Record.Duration.TotalSeconds;
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
    private static double Rank(PlayerRowViewModel player) => player.IsHealer ? player.HpsValue : player.DpsValue;

    private ListCollectionView CreatePlayersView()
    {
        var mistakes = _mistakes.ToLookup(m => m.Player, StringComparer.Ordinal);
        var rows = Record.Roster
            .Where(stats => _role == null || Specs.RoleOf(stats.SpecId) == _role)
            .Select(stats => new PlayerRowViewModel(stats, Record.Duration, Owner.Report,
                mistakes[stats.Name].ToArray(), _cards?.For(Record, stats), _collective))
            .OrderBy(Group)
            .ThenByDescending(Rank)
            .ThenBy(p => p.Name, StringComparer.CurrentCulture)
            .ToList();

        return new ListCollectionView(rows) { CustomSort = Sorting.Players.Comparer };
    }
}

/// <summary>
/// One line of where an attempt's losses went. Named rather than averaged: a mean over a roster
/// hides the one person who lost the pull behind nineteen who did not.
/// </summary>
public sealed record BlameViewModel(string Name, string PoolsText, string Headline);
