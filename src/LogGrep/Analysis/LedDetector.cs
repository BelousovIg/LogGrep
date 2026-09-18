using LogGrep.Models;
using LogGrep.ViewModels;

namespace LogGrep.Analysis;

/// <summary>
/// Counts what somebody led across the evening.
///
/// The counterweight the report needs. Everything else here looks for what went wrong, and a list
/// that only ever accuses is a list people stop opening - but praise has to be held to the same
/// standard as blame, so this counts and does not judge. "Top damage in nine of thirteen" is a
/// fact; "best player" is an opinion and has no place in it.
/// </summary>
public sealed class LedDetector : IDetector
{
    /// <summary>Attempts before leading any of them means anything.</summary>
    private const int MinimumAttempts = 5;

    /// <summary>What share of the evening somebody has to lead before it is worth saying.</summary>
    private const double Most = 0.5;

    /// <summary>An attempt shorter than this says nothing about anybody's output.</summary>
    private static readonly TimeSpan LongEnough = TimeSpan.FromSeconds(60);

    public string Category => "the night";

    public IEnumerable<Finding> Look(Attempts attempts)
    {
        var counted = attempts.Pulls.Where(p => p.Duration >= LongEnough).ToList();
        if (counted.Count < MinimumAttempts) return Array.Empty<Finding>();

        return Leaders(counted, "damage", p => p.Damage)
            .Concat(Leaders(counted, "healing", p => p.Healing))
            .Select(led => Report(attempts, led, counted.Count))
            .ToList();
    }

    private static IEnumerable<Led> Leaders(List<PullRecord> pulls, string what, Func<PlayerStats, long> of)
    {
        var tops = new List<Led>();

        foreach (var pull in pulls)
        {
            var best = pull.Roster.Where(p => of(p) > 0).OrderByDescending(of).FirstOrDefault();
            if (best != null) tops.Add(new Led(pull, best.Name, best.SpecId, what));
        }

        return tops
            .GroupBy(t => t.Player, StringComparer.Ordinal)
            .Where(g => g.Count() >= pulls.Count * Most)
            .Select(g => g.Last() with { Times = g.Count() });
    }

    private Finding Report(Attempts attempts, Led led, int of)
        => new(
            Category,
            "top " + led.What + " in " + Display.Count(led.Times) + " of " + Display.Count(of) + " attempts",
            "counted across the evening rather than within any one attempt",
            "A count, not a verdict. What it is worth depends on the fight and on what else you " +
            "were doing, and the app has no opinion about either.",
            Cost.Nothing("counted, not costed"),
            attempts.NumberOf(led.Pull),
            led.Pull,
            led.Player,
            led.SpecId,
            TimeSpan.Zero) { Timeless = true };

    private readonly record struct Led(PullRecord Pull, string Player, int SpecId, string What, int Times = 0);
}
