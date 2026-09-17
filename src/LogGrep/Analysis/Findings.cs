using LogGrep.Models;

namespace LogGrep.Analysis;

/// <summary>
/// Runs every detector over every encounter and hands back what they found, heaviest first.
///
/// Detectors know nothing about each other and nothing about the window. Adding one is adding a
/// class to the list below, and because they all answer in the same shape, what comes out of two
/// of them can be sorted against each other - which is the whole reason the shape exists.
/// </summary>
public static class Findings
{
    public static IReadOnlyList<Finding> In(IEnumerable<PullRecord> pulls)
        => In(pulls, new MechanicDetector(), new AvoidableDamageDetector(), new InterruptDetector(),
            new DeathDetector(), new IdleDetector(), new CooldownDetector(),
            new UptimeDetector(), new BuildDetector(), new StackDetector(),
            new FirstDeathDetector());

    public static IReadOnlyList<Finding> In(IEnumerable<PullRecord> pulls, params IDetector[] detectors)
    {
        var found = new List<Finding>();

        foreach (var encounter in pulls.GroupBy(p => p.GroupKey, StringComparer.Ordinal))
        {
            var attempts = new Attempts(encounter.OrderBy(p => p.StartOffset).ToList());

            foreach (var detector in detectors) found.AddRange(detector.Look(attempts));
        }

        return found
            .OrderByDescending(f => f.Cost.Weight)
            .ThenBy(f => f.PullNumber)
            .ThenBy(f => f.At)
            .ToList();
    }
}
