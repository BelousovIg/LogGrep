using LogGrep.Models;

namespace LogGrep.Analysis;

/// <summary>A spell that turned out to belong to one role, learned from how it behaved in the log.</summary>
public sealed record MechanicRule(
    int SpellId,
    string Spell,
    string Encounter,
    Role Owner,
    int OwnerHits,
    int TotalHits,
    int Attempts)
{
    public double Share => TotalHits > 0 ? OwnerHits / (double)TotalHits : 0;

    /// <summary>
    /// "69 of 71 hit a tank, over 13 attempts" - the evidence the rule rests on. The number of
    /// attempts belongs here because a pattern drawn from one pull is a far weaker claim: a single
    /// attempt where the same mistake happened repeatedly hides it, the wrong targets having become
    /// the majority within that one sample.
    /// </summary>
    public string Evidence => OwnerHits + " of " + TotalHits + " hit a " + Specs.NameOf(Owner) +
        ", over " + Attempts + (Attempts == 1 ? " attempt" : " attempts");
}

/// <summary>One application of a mechanic to somebody whose role does not own it.</summary>
public sealed record Finding(
    MechanicRule Rule,
    int PullNumber,
    PullRecord Pull,
    string Player,
    int SpecId,
    TimeSpan At)
{
    public Role Role => Specs.RoleOf(SpecId);
}

/// <summary>
/// Works out which debuffs belong to which role, and then reports the times somebody else took
/// one. Nothing about any boss is written down here: a mechanic is recognised by the company it
/// keeps across the pulls of that encounter, which is the only approach that survives a patch.
/// </summary>
public static class MechanicAnalyzer
{
    /// <summary>Below this many applications an encounter has not shown a pattern, only noise.</summary>
    private const int MinimumApplications = 8;

    /// <summary>
    /// And below this many attempts it has not shown one either, however many applications there
    /// were. A single attempt where a mistake repeated makes the mistake the majority of the
    /// sample and hides itself; a run of attempts is what separates a habit from an accident. The
    /// price is that a short log finds nothing at all, which is the honest answer for a short log.
    /// </summary>
    private const int MinimumAttempts = 10;

    /// <summary>How much of a spell one role has to take before it counts as theirs.</summary>
    private const double OwnerShare = 0.85;

    /// <summary>
    /// How far above its share of the group that role has to land. Without this a raid-wide debuff
    /// would read as a damage mechanic purely because most of a raid is damage, and every healer
    /// who caught it would be reported as a mistake.
    /// </summary>
    private const double Enrichment = 1.5;

    public static IReadOnlyList<Finding> Analyse(IEnumerable<PullRecord> pulls)
    {
        var findings = new List<Finding>();

        foreach (var encounter in pulls.GroupBy(p => p.GroupKey, StringComparer.Ordinal))
        {
            findings.AddRange(AnalyseEncounter(encounter.OrderBy(p => p.StartOffset).ToList()));
        }

        return findings;
    }

    private static IEnumerable<Finding> AnalyseEncounter(IReadOnlyList<PullRecord> pulls)
    {
        var baseline = Composition(pulls);
        var hits = Collect(pulls);

        foreach (var spell in hits.GroupBy(h => h.SpellId))
        {
            var applications = spell.ToList();
            if (applications.Count < MinimumApplications) continue;

            int attempts = applications.Select(a => a.PullNumber).Distinct().Count();
            if (attempts < MinimumAttempts) continue;

            var byRole = applications.GroupBy(a => a.Role).OrderByDescending(g => g.Count()).First();
            double share = byRole.Count() / (double)applications.Count;
            if (share < OwnerShare) continue;

            // A role that already makes up most of the group proves nothing by taking most of a spell.
            double expected = baseline.TryGetValue(byRole.Key, out double value) ? value : 0;
            if (expected > 0 && share < expected * Enrichment) continue;

            var rule = new MechanicRule(
                spell.Key,
                applications[0].Spell,
                pulls[0].EncounterName,
                byRole.Key,
                byRole.Count(),
                applications.Count,
                attempts);

            foreach (var off in applications.Where(a => a.Role != byRole.Key))
            {
                yield return new Finding(rule, off.PullNumber, off.Pull, off.Player, off.SpecId, off.At);
            }
        }
    }

    /// <summary>What share of the group each role made up, averaged over the attempts.</summary>
    private static Dictionary<Role, double> Composition(IReadOnlyList<PullRecord> pulls)
    {
        var counts = new Dictionary<Role, int>();
        int total = 0;

        foreach (var pull in pulls)
        {
            foreach (var player in pull.Roster)
            {
                var role = Specs.RoleOf(player.SpecId);
                counts.TryGetValue(role, out int seen);
                counts[role] = seen + 1;
                total++;
            }
        }

        return total == 0
            ? new Dictionary<Role, double>()
            : counts.ToDictionary(e => e.Key, e => e.Value / (double)total);
    }

    /// <summary>Every debuff application, carried alongside the spec the target had at the time.</summary>
    private static List<Application> Collect(IReadOnlyList<PullRecord> pulls)
    {
        var all = new List<Application>();

        for (int i = 0; i < pulls.Count; i++)
        {
            var pull = pulls[i];
            var specs = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var player in pull.Roster) specs[player.Name] = player.SpecId;

            foreach (var debuff in pull.Debuffs)
            {
                if (!specs.TryGetValue(debuff.Player, out int specId)) continue;
                all.Add(new Application(debuff.SpellId, debuff.Spell, i + 1, pull, debuff.Player, specId, debuff.At));
            }
        }

        return all;
    }

    private readonly record struct Application(
        int SpellId,
        string Spell,
        int PullNumber,
        PullRecord Pull,
        string Player,
        int SpecId,
        TimeSpan At)
    {
        public Role Role => Specs.RoleOf(SpecId);
    }
}
