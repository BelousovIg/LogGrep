using LogGrep.ViewModels;

namespace LogGrep.Analysis;

/// <summary>
/// Notices when somebody stopped making their mistakes.
///
/// Four mistakes in the first six attempts and none in the last seven is the good news of the
/// evening, and until this existed it read exactly the same as four mistakes spread evenly through
/// it - a player who fixed something and a player who never did, described identically. A report
/// that cannot tell those apart is worse than unhelpful; it is discouraging to the person who did
/// the work.
///
/// It reads the mistakes rather than the log, because "did this stop happening" is a question about
/// the findings themselves and no single detector can see past its own.
/// </summary>
public sealed class ImprovementReview : IReview
{
    /// <summary>Attempts before an evening has halves worth comparing.</summary>
    private const int MinimumAttempts = 6;

    /// <summary>Mistakes in the first half before stopping is a change rather than a coincidence.</summary>
    private const int Enough = 3;

    /// <summary>How much of it has to go. Fewer is not news; almost all of it is.</summary>
    private const double Stopped = 0.25;

    public IEnumerable<Finding> Look(Attempts attempts, IReadOnlyList<Finding> found)
    {
        if (attempts.Pulls.Count < MinimumAttempts) yield break;

        // The halves are of the evening, not of the mistakes: a night is read front to back, and
        // where the middle falls does not depend on who was making them.
        int middle = attempts.Pulls.Count / 2;

        foreach (var player in found
            .Where(f => f.Player.Length > 0 && f.Category != "the night")
            .GroupBy(f => f.Player, StringComparer.Ordinal))
        {
            int before = player.Count(f => f.PullNumber <= middle);
            int after = player.Count(f => f.PullNumber > middle);

            if (before < Enough || after > before * Stopped) continue;

            yield return Report(attempts, player.Key, player.First().SpecId, before, after, middle);
        }
    }

    private static Finding Report(Attempts attempts, string player, int specId,
        int before, int after, int middle)
    {
        int rest = attempts.Pulls.Count - middle;

        return new Finding(
            "the night",
            Say(before) + " in the first " + Display.Count(middle) + " attempts, " +
            Say(after) + " in the last " + Display.Count(rest),
            "counted across the evening rather than within any one attempt",
            "Whatever you changed partway through, it held. Nothing here knows what it was.",
            Cost.Nothing("counted, not costed"),
            attempts.NumberOf(attempts.Pulls[^1]),
            attempts.Pulls[^1],
            player,
            specId,
            TimeSpan.Zero);
    }

    private static string Say(int count) => count switch
    {
        0 => "none",
        1 => "one mistake",
        _ => Display.Count(count) + " mistakes",
    };
}
