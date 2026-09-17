using LogGrep.Analysis;
using LogGrep.Models;

namespace LogGrep.ViewModels;

/// <summary>
/// What kind of thing a mark is. Shape carries this as well as colour, so the lane still reads for
/// somebody who cannot tell the colours apart.
/// </summary>
public enum MarkKind
{
    Mechanic,
    Interrupt,
    Idle,
    Opening,
    Death,

    /// <summary>Something the enemy did, which only ever appears on the lane above the group.</summary>
    Cast,
}

/// <summary>
/// One thing on a lane: when it happened, what it cost, and what it was.
///
/// <see cref="Size"/> is in health pools, the one unit that compares a rogue's mistake with a
/// tank's. <see cref="Collective"/> is set when the same moment caught much of the group, because
/// that is a different conversation from a personal mistake and must not look like one.
/// </summary>
public readonly record struct LaneMark(
    double At, double Size, MarkKind Kind, bool Collective, string Text)
{
    /// <summary>
    /// Everything that happened to one player in one attempt, as marks.
    ///
    /// Deaths are drawn from the roster rather than from the findings, because the findings are what
    /// is worth saying and the deaths are what happened - a death nobody has a lesson about still
    /// belongs on the lane. Where a finding already explains one, the finding's own words win.
    /// </summary>
    public static IReadOnlyList<LaneMark> For(
        PlayerStats player, IReadOnlyList<Finding> mine, IReadOnlyCollection<TimeSpan> collective)
    {
        var marks = new List<LaneMark>(mine.Count + player.Deaths.Count);

        foreach (var finding in mine)
        {
            marks.Add(new LaneMark(
                finding.At.TotalSeconds,
                Pools(finding.Cost, player.MaxHealth),
                Of(finding.Category),
                Near(collective, finding.At),
                Words(finding)));
        }

        foreach (var death in player.Deaths)
        {
            if (mine.Any(f => f.Category == "deaths" && Close(f.At, death.At))) continue;

            marks.Add(new LaneMark(
                death.At.TotalSeconds,
                1,
                MarkKind.Death,
                Near(collective, death.At),
                Display.Clock(death.At) + " died" + (death.Causes.Count == 0
                    ? string.Empty
                    : " - " + death.Causes[0].Label)));
        }

        return marks.OrderBy(m => m.At).ToArray();
    }

    /// <summary>
    /// The moments a single thing caught much of the group at once. One event that hit five people
    /// is not five mistakes, and the lane has to be able to say so.
    /// </summary>
    public static IReadOnlyList<TimeSpan> Shared(PullRecord pull, IReadOnlyList<Finding> all)
        => Analysis.Collective.In(pull, all);

    /// <summary>What the enemy did, drawn above the group so a cause sits over its consequence.</summary>
    public static IReadOnlyList<LaneMark> Enemy(PullRecord pull)
        => pull.Casts
            .Select(c => new LaneMark(c.At.TotalSeconds, 0, MarkKind.Cast, false,
                Display.Clock(c.At) + " " + c.Spell + (c.Stopped
                    ? " - stopped" + (c.By.Length > 0 ? " by " + PlayerName.Character(c.By) : string.Empty)
                    : string.Empty)))
            .OrderBy(m => m.At)
            .ToArray();

    private static string Words(Finding finding)
    {
        string line = finding.Line;
        return finding.Cost.Text.Length == 0 ? line : line + " - " + finding.Cost.Text;
    }

    /// <summary>
    /// What a finding cost, in the player's own health pools. Output that never happened is real and
    /// is not a health pool, so it draws at the smallest size the lane has rather than pretending to
    /// a unit it does not belong to.
    /// </summary>
    private static double Pools(Cost cost, long pool) => cost.Toll switch
    {
        Toll.Death => 1,
        Toll.Damage when pool > 0 => cost.Amount / (double)pool,
        _ => 0,
    };

    private static bool Near(IReadOnlyCollection<TimeSpan> moments, TimeSpan at)
        => Analysis.Collective.Covers(moments, at);

    private static bool Close(TimeSpan one, TimeSpan other)
        => Math.Abs((one - other).TotalSeconds) <= 2;

    private static MarkKind Of(string category) => category switch
    {
        "interrupts" => MarkKind.Interrupt,
        "deaths" => MarkKind.Death,
        "the pull" => MarkKind.Opening,
        "idle" or "cooldowns" or "uptime" or "builds" => MarkKind.Idle,
        _ => MarkKind.Mechanic,
    };
}
