using LogGrep.Models;
using LogGrep.Services;

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
    public static IReadOnlyList<Finding> In(IEnumerable<PullRecord> pulls,
        IReadOnlyList<WrittenRule>? written = null)
        => In(pulls, written, new MechanicDetector(), new AvoidableDamageDetector(), new InterruptDetector(),
            new DeathDetector(), new IdleDetector(), new CooldownDetector(),
            new UptimeDetector(), new BuildDetector(), new StackDetector(),
            new FirstDeathDetector(), new LedDetector(), new SpreadDetector());

    /// <summary>
    /// The reviews, which run after the detectors and over what they found. Kept separate because
    /// they ask a different kind of question - one that needs the whole picture rather than the log.
    /// </summary>
    private static readonly IReview[] Reviews = { new ImprovementReview(), new TollReview() };

    public static IReadOnlyList<Finding> In(IEnumerable<PullRecord> pulls,
        IReadOnlyList<WrittenRule>? written, params IDetector[] detectors)
    {
        var found = new List<Finding>();

        foreach (var encounter in pulls.GroupBy(p => p.GroupKey, StringComparer.Ordinal))
        {
            var attempts = new Attempts(encounter.OrderBy(p => p.StartOffset).ToList(), written);

            var mine = new List<Finding>();
            foreach (var detector in detectors) mine.AddRange(detector.Look(attempts));

            foreach (var review in Reviews) mine.AddRange(review.Look(attempts, mine.ToList()));

            found.AddRange(mine);
        }

        return found
            .OrderByDescending(f => f.Cost.Weight)
            .ThenBy(f => f.PullNumber)
            .ThenBy(f => f.At)
            .ToList();
    }
}
