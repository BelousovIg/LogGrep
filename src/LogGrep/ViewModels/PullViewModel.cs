using System.Windows.Media;
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
    private IReadOnlyList<Trace>? _traces;
    private readonly Dictionary<string, bool> _switches = new(StringComparer.Ordinal);
    private int _from;
    private int _to = int.MaxValue;

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

        // The chart is about the same people as the rows under it. Narrowing to the healers and
        // leaving a damage line drawn over everybody would put the group's answer next to one
        // person's question, which is the one thing a chart beside a table must not do.
        Remember();
        _traces = null;

        OnPropertyChanged(nameof(PlayersView));
        OnPropertyChanged(nameof(Traces));
        OnPropertyChanged(nameof(Deaths));
        OnPropertyChanged(nameof(ShownText));
    }

    /// <summary>
    /// Which lines were switched on, so that rebuilding them does not quietly turn every switch back
    /// on behind somebody who had just turned three of them off.
    /// </summary>
    private void Remember()
    {
        if (_traces == null) return;

        foreach (var trace in _traces) _switches[trace.Name] = trace.IsOn;
    }

    private bool IsOn(string name, bool byDefault)
        => _switches.TryGetValue(name, out bool on) ? on : byDefault;

    /// <summary>Whoever the rows are showing, which is who the chart is about.</summary>
    private IReadOnlyList<PlayerStats> Shown => Record.Roster
        .Where(stats => _role == null || Specs.RoleOf(stats.SpecId) == _role)
        .ToArray();

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

    /// <summary>
    /// How many of the group the rows are showing. The totals above them are over everybody who was
    /// there, because that is what happened - so when the rows are narrowed the screen has to say
    /// that the two are counting different people.
    /// </summary>
    public string ShownText
    {
        get
        {
            int shown = PlayersView.Cast<PlayerRowViewModel>().Count();
            return shown == Record.Roster.Count
                ? string.Empty
                : "showing " + Display.Count(shown) + " of " + Display.Count(Record.Roster.Count) +
                  " who were there; the totals above are over all of them";
        }
    }

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

    /// <summary>
    /// Which stretch of the fight the rates are read over. Dragging across the chart sets it; the
    /// whole attempt is where it starts and what the reset goes back to.
    /// </summary>
    public int From
    {
        get => _from;
        set => SetWindow(value, _to);
    }

    public int To
    {
        get => _to;
        set => SetWindow(_from, value);
    }

    public bool IsWindowed => _from > 0 || _to < (int)Record.Duration.TotalSeconds;

    public string WindowText => IsWindowed
        ? "reading " + Display.Clock(TimeSpan.FromSeconds(_from)) + " to " +
          Display.Clock(TimeSpan.FromSeconds(Math.Min(_to, (int)Record.Duration.TotalSeconds)))
        : "reading the whole attempt";

    /// <summary>Back to the whole attempt, which is where every rate on this screen starts.</summary>
    public void ResetWindow() => SetWindow(0, int.MaxValue);

    private void SetWindow(int from, int to)
    {
        int length = (int)Record.Duration.TotalSeconds;
        from = Math.Clamp(from, 0, Math.Max(0, length));
        to = to >= length ? int.MaxValue : Math.Clamp(to, from, length);

        if (_from == from && _to == to) return;

        _from = from;
        _to = to;
        _playersView = null;

        OnPropertyChanged(nameof(From));
        OnPropertyChanged(nameof(To));
        OnPropertyChanged(nameof(IsWindowed));
        OnPropertyChanged(nameof(WindowText));
        OnPropertyChanged(nameof(PlayersView));
        OnPropertyChanged(nameof(ShownText));
    }

    /// <summary>
    /// The lines that make up the shape of this attempt. Built once and kept, because each carries
    /// its own switch and rebuilding the list would turn every switch back on behind somebody.
    /// </summary>
    public IReadOnlyList<Trace> Traces => _traces ??= BuildTraces();

    /// <summary>The seconds somebody went down, which is where the other lines bend.</summary>
    public IReadOnlyList<int> Deaths
        => Shown.SelectMany(p => p.Deaths).Select(d => (int)d.At.TotalSeconds).OrderBy(s => s).ToArray();

    /// <summary>What the chart says at one second, which is what its hover shows.</summary>
    public string Readout(int second)
        => string.Join(Environment.NewLine, ChartReadout.At(Traces, Deaths, Kills, second));

    /// <summary>
    /// Every boss that went down in this attempt, marked on the chart where it happened.
    ///
    /// One for a boss pull, at the end of the fight - the game closes the encounter the moment the
    /// last of it dies, and that is the one second a kill is certainly at. Several for a keystone
    /// run, which is one row holding a whole dungeon and would otherwise be half an hour of line
    /// with nothing on it.
    ///
    /// One per encounter rather than per creature: a council is several corpses and one ending, and
    /// the log never says which of them fell last. So two bosses fought together carry one mark
    /// under the name of the fight, which is also how somebody sees that they were one fight.
    /// </summary>
    public IReadOnlyList<BossKill> Kills => Record.Kills;

    private IReadOnlyList<Trace> BuildTraces()
    {
        var traces = new List<Trace>();
        bool everybody = _role == null;
        var shown = Shown;
        int seconds = (int)Record.Duration.TotalSeconds;

        // The enemy is the enemy whoever is being looked at, so this line never narrows.
        if (Record.EnemyHealth.Count > 0)
        {
            traces.Add(new Trace("enemy", Color.FromRgb(0xE0, 0x70, 0x6D),
                Record.EnemyHealth.ToArray(), v => Display.Percent(v), IsOn("enemy", true)));
        }

        // The rest are about people, so they are about whoever the rows are showing. Unnarrowed they
        // are the scan's own lines rather than a sum of the roster: the scan also counted what the
        // group's pets put out, and nobody's row owns a pet.
        var standing = everybody
            ? Record.Standing.Select(v => (double)v).ToArray()
            : OnTheirFeet(shown, seconds);

        if (standing.Length > 0)
        {
            traces.Add(new Trace("standing", Color.FromRgb(0x69, 0xC0, 0x7A), standing,
                v => Display.Count((int)v) + " up", IsOn("standing", true)));
        }

        var damage = everybody
            ? Record.DamageLine.Select(v => (double)v).ToArray()
            : Summed(shown, p => p.DamageLine, seconds);

        if (damage.Any(v => v > 0))
        {
            traces.Add(new Trace("damage", Color.FromRgb(0xE0, 0xA5, 0x54), damage,
                v => Display.Rate(v) + "/s", IsOn("damage", false)));
        }

        var healing = everybody
            ? Record.HealingLine.Select(v => (double)v).ToArray()
            : Summed(shown, p => p.HealingLine, seconds);

        if (healing.Any(v => v > 0))
        {
            traces.Add(new Trace("healing", Color.FromRgb(0x8E, 0x9B, 0xE8), healing,
                v => Display.Rate(v) + "/s", IsOn("healing", false)));
        }

        return traces;
    }

    /// <summary>
    /// How many of them were up, second by second. A death is not the end of somebody's fight, so
    /// this walks their ups and downs rather than counting the ones who never went down.
    /// </summary>
    private static double[] OnTheirFeet(IReadOnlyList<PlayerStats> shown, int seconds)
    {
        var line = new double[Math.Max(0, seconds) + 1];

        foreach (var player in shown)
        {
            int next = 0;
            bool up = true;

            for (int second = 0; second < line.Length; second++)
            {
                while (next < player.Flips.Count && player.Flips[next].Second <= second)
                {
                    up = player.Flips[next].Up;
                    next++;
                }

                if (up) line[second]++;
            }
        }

        return line;
    }

    private static double[] Summed(IReadOnlyList<PlayerStats> shown,
        Func<PlayerStats, IReadOnlyList<long>> pick, int seconds)
    {
        var line = new double[Math.Max(0, seconds) + 1];

        foreach (var player in shown)
        {
            var own = pick(player);
            for (int second = 0; second < own.Count && second < line.Length; second++) line[second] += own[second];
        }

        return line;
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
                mistakes[stats.Name].ToArray(), _cards?.For(Record, stats), _collective, _from, _to))
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
