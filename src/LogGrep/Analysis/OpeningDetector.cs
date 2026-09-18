using LogGrep.Models;

namespace LogGrep.Analysis;

/// <summary>
/// Finds a pull that started with the wrong person.
///
/// A pull belongs to the tank at both ends: they land the first blow, and they take it. Whoever
/// strikes first has the threat, and whoever the enemy hits first had it - so if either of those is
/// not a tank, the fight opened wrong, and everything that follows in the next few seconds follows
/// from that.
///
/// This is the one rule here that needs no run of attempts and no baseline at all. It is not a
/// habit being learned from a night; it is a fact about one moment, and the moment is the first
/// one. Outside the opening it means nothing - threat changes hands all fight long for good
/// reasons - so nothing later is read this way.
/// </summary>
public sealed class OpeningDetector : IDetector
{
    /// <summary>
    /// How far into a fight the opening lasts. Threat changes hands all fight long for good reasons
    /// - a tank swap, a second boss, somebody picking up an add - and reading any of that as a bad
    /// pull would put a finding on every attempt of every fight.
    /// </summary>
    private static readonly TimeSpan Opening = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How far ahead of the tank somebody has to be before they were early rather than alongside.
    ///
    /// Everybody starts a pull at once, so the opening of one is a scramble: a pre-cast lands at
    /// 0.0 while the tank is still closing the distance and connects at 0.2. On the real log that
    /// scramble is the usual case - on twenty-four attempts the gaps ran from -1.4 to 1.1 seconds
    /// and sat inside half a second either way - so reading it as a mistake put a finding on
    /// nearly every attempt, which is not a report, it is a shrug. The other end is the same
    /// story from the other side: an untargeted pulse hits the whole group inside one event, and
    /// which name the log happens to write first in it means nothing.
    ///
    /// So the measure is a global cooldown, the time it takes anybody to act once. Less than that
    /// and the tank had not yet had a turn; being ahead of somebody who has not moved is not being
    /// early. More than that and you acted, they could have acted, and they had not.
    /// </summary>
    private static readonly TimeSpan Ahead = TimeSpan.FromSeconds(1.5);

    /// <summary>How long a death has to follow for it to have followed from the pull going wrong.</summary>
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(15);

    public string Category => "the pull";

    public IEnumerable<Finding> Look(Attempts attempts)
    {
        foreach (var pull in attempts.Pulls)
        {
            // Without a tank in the group there is nobody the pull belonged to, and a five-player
            // party that brought none is not making this mistake.
            var tanks = pull.Roster.Where(p => Specs.RoleOf(p.SpecId) == Role.Tank).ToList();
            if (tanks.Count == 0) continue;

            var opened = Early(pull, tanks, p => p.Struck);
            if (opened != null)
            {
                yield return Report(attempts, pull, opened,
                    "opened the pull",
                    "you hit the enemy a whole action before any tank did",
                    "Threat starts with whoever lands the first blow. Opening ahead of the tank " +
                    "hands it to you, and everything the boss does for the next few seconds is " +
                    "aimed at you rather than at them.",
                    // Serious: a pull that opens in the wrong hands is not a shaved percent, it is
                    // the first seconds of the fight spent putting it where it should have started.
                    serious: true);
            }

            var hit = Early(pull, tanks, p => p.WasHit);
            if (hit != null && !string.Equals(hit.Name, opened?.Name, StringComparison.Ordinal))
            {
                yield return Report(attempts, pull, hit,
                    "took the first hit of the pull",
                    "the enemy hit you a whole action before it hit any tank",
                    "The boss hits whoever holds its attention, and at the start of a fight that " +
                    "should be a tank. Being first means the pull began with the threat in the " +
                    "wrong place.");
            }
        }
    }

    /// <summary>
    /// Whoever this happened to first, if it happened to them inside the opening and a clear step
    /// ahead of every tank. A tank it never happened to at all counts as never: somebody taking the
    /// opening while the tanks stand out of it is the same mistake, only more so.
    /// </summary>
    private static PlayerStats? Early(
        PullRecord pull, List<PlayerStats> tanks, Func<PlayerStats, TimeSpan?> moment)
    {
        var first = pull.Roster
            .Where(p => Specs.RoleOf(p.SpecId) != Role.Tank && moment(p) != null)
            .OrderBy(p => moment(p)!.Value)
            .FirstOrDefault();

        if (first == null || moment(first)!.Value > Opening) return null;

        var tank = tanks.Select(moment).Where(m => m != null).Select(m => m!.Value).DefaultIfEmpty(TimeSpan.MaxValue).Min();

        return moment(first)!.Value + Ahead <= tank ? first : null;
    }

    private Finding Report(Attempts attempts, PullRecord pull, PlayerStats player,
        string headline, string evidence, string advice, bool serious = false)
    {
        var death = attempts.DeathAfter(pull, player.Name, TimeSpan.Zero, Soon);

        return new Finding(
            Category,
            headline,
            evidence,
            advice,
            death == null ? Cost.Nothing("the pull settled") : Cost.Death(death.At),
            attempts.NumberOf(pull),
            pull,
            player.Name,
            player.SpecId,
            TimeSpan.Zero) { Serious = serious };
    }
}
