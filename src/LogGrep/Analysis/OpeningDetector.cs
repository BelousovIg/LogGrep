using LogGrep.Models;

namespace LogGrep.Analysis;

/// <summary>
/// Finds a pull that started with the wrong person.
///
/// A pull belongs to the tank at both ends: they land the first blow, and they take it. Whoever
/// strikes first has the threat, and whoever the boss hits first had it - so if either of those is
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

    /// <summary>How long a death has to follow for it to have followed from the pull going wrong.</summary>
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(15);

    public string Category => "the pull";

    public IEnumerable<Finding> Look(Attempts attempts)
    {
        foreach (var pull in attempts.Pulls)
        {
            // Without a tank in the group there is nobody the pull belonged to, and a five-player
            // party that brought none is not making this mistake.
            if (!pull.Roster.Any(p => Specs.RoleOf(p.SpecId) == Role.Tank)) continue;

            var opened = pull.OpenedAt <= Opening ? Who(pull, pull.Opened) : null;
            if (opened != null && Specs.RoleOf(opened.SpecId) != Role.Tank)
            {
                yield return Report(attempts, pull, opened,
                    "opened the pull",
                    "the first damage on the boss was yours, before any tank's",
                    "Threat starts with whoever lands the first blow. Opening ahead of the tank " +
                    "hands it to you, and everything the boss does for the next few seconds is " +
                    "aimed at you rather than at them.");
            }

            var hit = pull.FirstHitAt <= Opening ? Who(pull, pull.FirstHit) : null;
            if (hit != null && Specs.RoleOf(hit.SpecId) != Role.Tank
                && !string.Equals(hit.Name, opened?.Name, StringComparison.Ordinal))
            {
                yield return Report(attempts, pull, hit,
                    "took the first hit of the pull",
                    "the boss struck you before it struck any tank",
                    "The boss hits whoever holds its attention, and at the start of a fight that " +
                    "should be a tank. Being first means the pull began with the threat in the " +
                    "wrong place.");
            }
        }
    }

    private Finding Report(Attempts attempts, PullRecord pull, PlayerStats player,
        string headline, string evidence, string advice)
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
            TimeSpan.Zero);
    }

    private static PlayerStats? Who(PullRecord pull, string name)
        => name.Length == 0
            ? null
            : pull.Roster.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));
}
