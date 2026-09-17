using LogGrep.Models;
using LogGrep.ViewModels;

namespace LogGrep.Analysis;

/// <summary>
/// Finds whoever keeps dying first.
///
/// The first death of an attempt is often the one that caused the rest - a healer down, a tank
/// down, and the wipe that follows reads as everybody's fault. Counted across an evening it stops
/// being an anecdote: first in nine attempts out of thirteen is a fact about the night, and it is
/// the fact a raid leader is trying to remember when they argue about it afterwards.
///
/// This is not the same question as whether any one of those deaths was avoidable, which the death
/// rules already ask one attempt at a time. It is about the shape of the evening.
/// </summary>
public sealed class FirstDeathDetector : IDetector
{
    /// <summary>Attempts with a death in them before being first in one means anything.</summary>
    private const int MinimumAttempts = 5;

    /// <summary>
    /// What share of those attempts somebody has to open before it is worth saying. In a group of
    /// twenty, being first twice is the dice; being first most of the night is not.
    /// </summary>
    private const double Often = 0.5;

    public string Category => "the night";

    public IEnumerable<Finding> Look(Attempts attempts)
    {
        var opened = Openings(attempts).ToList();
        if (opened.Count < MinimumAttempts) return Array.Empty<Finding>();

        return opened
            .GroupBy(o => o.Player, StringComparer.Ordinal)
            .Where(g => g.Count() >= opened.Count * Often)
            .Select(g => Report(attempts, g.Last(), g.Count(), opened.Count))
            .ToList();
    }

    private Finding Report(Attempts attempts, Opening last, int times, int of)
        => new(
            Category,
            "first to die in " + Display.Count(times) + " of " + Display.Count(of) + " attempts",
            "counted across the evening rather than within any one attempt",
            "The first death is often the one the rest followed from. Whether each was avoidable is " +
            "a separate question - this one is about how often it starts with you.",
            Cost.Nothing("counted, not costed"),
            attempts.NumberOf(last.Pull),
            last.Pull,
            last.Player,
            last.SpecId,
            last.At);

    /// <summary>Who went down first on each attempt that had a death at all.</summary>
    private static IEnumerable<Opening> Openings(Attempts attempts)
    {
        foreach (var pull in attempts.Pulls)
        {
            var first = pull.Roster
                .SelectMany(p => p.Deaths.Select(d => new Opening(pull, p.Name, p.SpecId, d.At)))
                .OrderBy(o => o.At)
                .FirstOrDefault();

            if (first.Player != null) yield return first;
        }
    }

    private readonly record struct Opening(PullRecord Pull, string Player, int SpecId, TimeSpan At);
}
