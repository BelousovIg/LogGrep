using LogGrep.Models;

namespace LogGrep.Analysis;

/// <summary>
/// One encounter's attempts, with the few things every detector needs worked out once: which
/// attempt is which, what spec somebody was playing, how the group was made up, and whether a
/// death followed something.
/// </summary>
public sealed class Attempts
{
    private readonly Dictionary<PullRecord, int> _numbers = new();

    public Attempts(IReadOnlyList<PullRecord> pulls)
    {
        Pulls = pulls;
        Encounter = pulls.Count > 0 ? pulls[0].EncounterName : string.Empty;

        for (int i = 0; i < pulls.Count; i++) _numbers[pulls[i]] = i + 1;
        Composition = Shares(pulls);
    }

    public IReadOnlyList<PullRecord> Pulls { get; }

    public string Encounter { get; }

    /// <summary>What share of the group each role made up, averaged over the attempts.</summary>
    public IReadOnlyDictionary<Role, double> Composition { get; }

    public int NumberOf(PullRecord pull) => _numbers.TryGetValue(pull, out int number) ? number : 0;

    public int SpecOf(PullRecord pull, string player)
        => pull.Roster.FirstOrDefault(p => string.Equals(p.Name, player, StringComparison.Ordinal))?.SpecId ?? 0;

    /// <summary>The first death of that player within the window, if there was one.</summary>
    public DeathRecord? DeathAfter(PullRecord pull, string player, TimeSpan at, TimeSpan within)
        => pull.Roster
            .FirstOrDefault(p => string.Equals(p.Name, player, StringComparison.Ordinal))?
            .Deaths.FirstOrDefault(d => d.At >= at && d.At - at <= within);

    private static Dictionary<Role, double> Shares(IReadOnlyList<PullRecord> pulls)
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
}

/// <summary>
/// One thing worth looking for. Everything after the first milestone adds one of these rather than
/// growing a function, which is how the findings stay in one shape and remain sortable against each
/// other.
/// </summary>
public interface IDetector
{
    string Category { get; }

    IEnumerable<Finding> Look(Attempts attempts);
}
