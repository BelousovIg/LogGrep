using LogGrep.Models;
using LogGrep.ViewModels;

namespace LogGrep.Analysis;

/// <summary>
/// Finds damage that most of the group takes none of.
///
/// The same argument as the debuff rule, pointed at a different event. There is no list of which
/// spells are dodgeable anywhere in the app, and there never will be: a spell that lands on two
/// people out of twenty, attempt after attempt, is one the other eighteen are getting out of, and
/// that is a thing the log says plainly without anybody writing it down.
///
/// A spell that belongs to one role is left alone here. A tank taking every melee swing is not
/// standing in anything, and the mechanics detector already has a better sentence for that case.
/// </summary>
public sealed class AvoidableDamageDetector : IDetector
{
    /// <summary>Below this many attempts an encounter has shown noise, not a pattern.</summary>
    private const int MinimumAttempts = 10;

    /// <summary>How much of the group a spell may touch and still count as something they avoid.</summary>
    private const double CrowdShare = 0.25;

    /// <summary>
    /// How much of a spell one role has to take before it belongs to that role rather than to
    /// nobody. Looser than the mechanics rule on purpose: that rule is deciding whether to accuse
    /// somebody, and this one is deciding whether to keep quiet, so the doubt goes the other way.
    /// Two tanks eating a tank-seeking spell every attempt is 80% of its landings, and calling that
    /// avoidable damage would be telling a tank off for tanking.
    /// </summary>
    private const double RoleShare = 0.6;

    /// <summary>
    /// How far above its share of the group that role has to land before the spell is called theirs.
    /// Without this, seven damage dealers in a group of ten take seventy percent of everything that
    /// goes out raid-wide, and every raid-wide hit reads as a damage mechanic nobody should mention.
    /// </summary>
    private const double Enrichment = 1.5;

    /// <summary>How long after taking a hit a death still counts as having followed from it.</summary>
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(15);

    /// <summary>What findings of this kind are filed under; the death breakdown reads it too.</summary>
    public const string Name = "avoidable damage";

    public string Category => Name;

    public IEnumerable<Finding> Look(Attempts attempts)
    {
        foreach (var spell in Landings(attempts).GroupBy(l => l.Blow.SpellId))
        {
            var landings = spell.ToList();

            // Counted in attempts rather than in landings: an attempt where it went out ten times
            // is still one attempt, and ten of those would otherwise pass for a pattern.
            var runs = landings.GroupBy(l => l.Pull).ToList();
            if (runs.Count < MinimumAttempts) continue;

            // Averaged per attempt rather than over all the landings at once, so one attempt where
            // it went out ten times does not drown the ten where it went out once.
            double share = runs.Average(run => run.Count() / (double)Math.Max(1, run.Key.Roster.Count));
            if (share > CrowdShare) continue;

            var owner = landings.GroupBy(l => l.Role).OrderByDescending(g => g.Count()).First();
            double taken = owner.Count() / (double)landings.Count;
            double expected = attempts.Composition.TryGetValue(owner.Key, out double value) ? value : 0;
            if (taken >= RoleShare && (expected <= 0 || taken >= expected * Enrichment)) continue;

            string evidence = Evidence(runs);

            foreach (var landing in landings) yield return Report(attempts, landing, evidence);
        }
    }

    /// <summary>"2 of 20 took it on a typical attempt, over 13 attempts" - the whole of the case.</summary>
    private static string Evidence(List<IGrouping<PullRecord, Landing>> runs)
    {
        int took = (int)Math.Round(runs.Average(run => run.Count()));
        int group = (int)Math.Round(runs.Average(run => Math.Max(1, run.Key.Roster.Count)));

        return Math.Max(1, took) + " of " + group + " took it on a typical attempt, over " +
               runs.Count + (runs.Count == 1 ? " attempt" : " attempts");
    }

    private Finding Report(Attempts attempts, Landing landing, string evidence)
    {
        var blow = landing.Blow;
        var death = attempts.DeathAfter(landing.Pull, blow.Player, blow.First, Soon);

        return new Finding(
            Category,
            blow.Spell + " - avoidable",
            evidence,
            "Most of the group takes none of this. Where you were standing is the whole of the fix; " +
            "no amount of healing makes it free.",
            death == null
                ? Cost.Damage(blow.Amount)
                : Cost.Death(death.At, Blamed(death, blow.Spell)),
            attempts.NumberOf(landing.Pull),
            landing.Pull,
            blow.Player,
            attempts.SpecOf(landing.Pull, blow.Player),
            blow.First,
            blow.SpellId);
    }

    private static string? Blamed(DeathRecord death, string spell)
        => death.Causes.Any(c => string.Equals(c.Label, spell, StringComparison.OrdinalIgnoreCase)) ? spell : null;

    private static IEnumerable<Landing> Landings(Attempts attempts)
    {
        foreach (var pull in attempts.Pulls)
        {
            foreach (var blow in pull.Blows)
            {
                yield return new Landing(pull, blow, Specs.RoleOf(attempts.SpecOf(pull, blow.Player)));
            }
        }
    }

    private readonly record struct Landing(PullRecord Pull, Blow Blow, Role Role);
}
