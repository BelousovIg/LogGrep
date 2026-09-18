using LogGrep.ViewModels;

namespace LogGrep.Analysis;

/// <summary>
/// Says which one thing cost the evening the most.
///
/// Twenty findings spread over eight mechanics is a night with no single problem in it. Twenty
/// findings where thirteen are the same mechanic is a night with one, and that is a different
/// conversation - it is the difference between "everybody tighten up" and "we have not learned this
/// mechanic yet".
///
/// Nobody's name is on it, because it belongs to nobody: it is a fact about the fight and how the
/// group met it.
/// </summary>
public sealed class TollReview : IReview
{
    /// <summary>Findings before the split between them means anything.</summary>
    private const int Enough = 6;

    /// <summary>What share one thing has to account for before it is the story of the night.</summary>
    private const double Most = 0.4;

    public IEnumerable<Finding> Look(Attempts attempts, IReadOnlyList<Finding> found)
    {
        // Facts about the evening are not mistakes and must not be counted as the cost of one.
        var blame = found.Where(f => f.Category != "the night").ToList();
        if (blame.Count < Enough) yield break;

        var worst = blame
            .GroupBy(f => f.Headline, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .First();

        if (worst.Count() < blame.Count * Most) yield break;

        yield return new Finding(
            "the night",
            Display.Count(worst.Count()) + " of the evening's " + Display.Count(blame.Count()) +
            " mistakes were the same thing: " + worst.Key,
            "counted across the evening rather than within any one attempt",
            "One mechanic accounting for this much of a night is a different problem from twenty " +
            "separate slips. It is worth a word before the next pull rather than a word with each " +
            "of the people in it.",
            Cost.Nothing("counted, not costed"),
            attempts.NumberOf(attempts.Pulls[^1]),
            attempts.Pulls[^1],
            string.Empty,
            0,
            TimeSpan.Zero) { Timeless = true };
    }
}
