using LogGrep.Models;
using LogGrep.Services;
using LogGrep.ViewModels;

namespace LogGrep.Analysis;

/// <summary>A spell that turned out to belong to one role, learned from how it behaved in the log.</summary>
public sealed record MechanicRule(int SpellId, string Spell, Role Owner, int OwnerHits, int TotalHits, int Attempts)
{
    public double Share => TotalHits > 0 ? OwnerHits / (double)TotalHits : 0;

    /// <summary>
    /// "69 of 71 hit a tank, over 13 attempts" - the whole of the case. The number of attempts
    /// belongs in it because a pattern drawn from one is a far weaker claim than one drawn from a
    /// run, and the reader should not have to ask.
    /// </summary>
    public string Evidence => OwnerHits + " of " + TotalHits + " hit a " + Specs.NameOf(Owner) +
        ", over " + Attempts + (Attempts == 1 ? " attempt" : " attempts");
}

/// <summary>
/// Finds mechanics taken by the wrong role. Nothing about any boss is written down here: a mechanic
/// is recognised by the company it keeps across the attempts, which is the only approach that
/// survives a patch.
/// </summary>
public sealed class MechanicDetector : IDetector
{
    /// <summary>Below this many applications an encounter has shown noise, not a pattern.</summary>
    private const int MinimumApplications = 8;

    /// <summary>
    /// And below this many attempts it has shown one either, however many applications there were.
    /// A single attempt where a mistake repeated makes the mistake the majority of its own sample.
    /// </summary>
    private const int MinimumAttempts = 10;

    /// <summary>How much of a spell one role has to take before it counts as theirs.</summary>
    private const double OwnerShare = 0.85;

    /// <summary>
    /// How far above its share of the group that role has to land. Without this a raid-wide debuff
    /// reads as a damage mechanic purely because most of a raid is damage.
    /// </summary>
    private const double Enrichment = 1.5;

    /// <summary>How long after taking a mechanic a death still counts as having followed from it.</summary>
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(15);

    public string Category => "mechanics";

    public IEnumerable<Finding> Look(Attempts attempts)
    {
        foreach (var spell in Applications(attempts).GroupBy(a => a.SpellId))
        {
            var seen = spell.ToList();
            if (seen.Count < MinimumApplications) continue;

            int runs = seen.Select(a => a.PullNumber).Distinct().Count();
            if (runs < MinimumAttempts) continue;

            var owner = seen.GroupBy(a => a.Role).OrderByDescending(g => g.Count()).First();
            double share = owner.Count() / (double)seen.Count;
            if (share < OwnerShare) continue;

            // A role that already makes up most of the group proves nothing by taking most of a spell.
            double expected = attempts.Composition.TryGetValue(owner.Key, out double value) ? value : 0;
            if (expected > 0 && share < expected * Enrichment) continue;

            var rule = new MechanicRule(spell.Key, seen[0].Spell, owner.Key, owner.Count(), seen.Count, runs);

            // A written rule and the log are two independent sources, and where they disagree the
            // honest thing is to say so and stop rather than to report against one of them. Nobody
            // else can do this check, for the plain reason that nobody else has the second source.
            if (attempts.Written.TryGetValue(spell.Key, out var written)
                && written.Roles.Count > 0
                && !written.Roles.Contains(owner.Key))
            {
                yield return Disagreement(attempts, rule, written, seen[^1]);
                continue;
            }

            foreach (var off in seen.Where(a => a.Role != owner.Key))
            {
                yield return Report(attempts, rule, off);
            }
        }
    }

    /// <summary>
    /// The file and the log do not agree about this ability. Either the file is stale, or it was
    /// never a claim about who the thing lands on - a section flagged for tanks means tanks should
    /// care, which is not the same as saying it picks one. Findings from it are muted either way:
    /// a rule naming the wrong half of an ability would flag every damage dealer in every pull, and
    /// only the log would ever notice.
    /// </summary>
    private Finding Disagreement(Attempts attempts, MechanicRule rule, WrittenRule written, Application last)
        => new(
            "rules",
            written.Spell + " - the file says " + string.Join(" and ", written.Roles.Select(Specs.NameOf)) +
            ", the log says " + Specs.NameOf(rule.Owner),
            rule.Evidence,
            "The rule is stale, or it was never about who it lands on. Findings from it are muted " +
            "until the two agree.",
            Cost.Nothing("nothing was reported from it"),
            attempts.NumberOf(last.Pull),
            last.Pull,
            string.Empty,
            0,
            TimeSpan.Zero) { Timeless = true };

    private Finding Report(Attempts attempts, MechanicRule rule, Application off)
    {
        var death = attempts.DeathAfter(off.Pull, off.Player, off.At, Soon);

        return new Finding(
            Category,
            rule.Spell + " - " + Specs.NameOf(rule.Owner) + " mechanic",
            "went to " + Specs.PersonOf(off.Role) + "; " + rule.Evidence,
            Advice(attempts, rule),
            death == null
                ? Cost.Nothing("survived it")
                : Cost.Death(death.At, Blamed(death, rule.Spell)),
            off.PullNumber,
            off.Pull,
            off.Player,
            off.SpecId,
            off.At,
            off.SpellId);
    }

    /// <summary>Names the mechanic as the killer only when it is actually among the causes.</summary>
    private static string? Blamed(DeathRecord death, string spell)
        => death.Causes.Any(c => string.Equals(c.Label, spell, StringComparison.OrdinalIgnoreCase)) ? spell : null;

    /// <summary>
    /// Blizzard's own sentence about the ability when the rules file has one, because it says what
    /// the mechanic is for - the one thing derivation will never recover. Failing that, the written
    /// line for the role, which is the same three sentences for every boss in the game.
    /// </summary>
    private static string Advice(Attempts attempts, MechanicRule rule)
        => attempts.Written.TryGetValue(rule.SpellId, out var written) && written.Advice.Length > 0
            ? written.Advice
            : Advice(rule.Owner);

    private static string Advice(Role owner) => owner switch
    {
        Role.Tank => "This one follows the tank. On anybody else it means a swap went wrong, " +
                     "or you were the nearest thing to a tank when it picked.",
        Role.Healer => "This one picks a healer. On anybody else, check where you were standing " +
                       "when it went out.",
        _ => "This one belongs to the damage. Taking it as a tank or a healer usually means " +
             "position rather than timing.",
    };

    private static IEnumerable<Application> Applications(Attempts attempts)
    {
        foreach (var pull in attempts.Pulls)
        {
            int number = attempts.NumberOf(pull);

            foreach (var debuff in pull.Debuffs)
            {
                int spec = attempts.SpecOf(pull, debuff.Player);
                if (spec == 0 && !pull.Roster.Any(p => p.Name == debuff.Player)) continue;

                yield return new Application(debuff.SpellId, debuff.Spell, number, pull,
                    debuff.Player, spec, debuff.At);
            }
        }
    }

    private readonly record struct Application(
        int SpellId, string Spell, int PullNumber, PullRecord Pull, string Player, int SpecId, TimeSpan At)
    {
        public Role Role => Specs.RoleOf(SpecId);
    }
}
