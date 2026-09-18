using LogGrep.Models;
using LogGrep.ViewModels;

namespace LogGrep.Analysis;

/// <summary>
/// Says how much somebody's output moved across the evening.
///
/// Steady at a fair number beats spiky at a high one, and an average hides which it was: two people
/// with the same number for the night can have got there from 90K every attempt or from 40K and
/// 140K. The second is worth knowing about - it usually means the attempts went differently, not
/// the player - and it is invisible in any single figure.
///
/// This is a fact, not a fault. It says what the range was and leaves the reading of it alone.
/// </summary>
public sealed class SpreadDetector : IDetector
{
    /// <summary>Attempts before a spread means anything.</summary>
    private const int MinimumAttempts = 5;

    /// <summary>
    /// How far the spread has to reach before it is worth a sentence, as a share of the middle.
    /// Everybody's output moves; this is looking for the nights that swung.
    /// </summary>
    private const double Wide = 0.6;

    /// <summary>An attempt shorter than this says nothing about anybody's output.</summary>
    private static readonly TimeSpan LongEnough = TimeSpan.FromSeconds(60);

    public string Category => "the night";

    public IEnumerable<Finding> Look(Attempts attempts)
    {
        var counted = attempts.Pulls.Where(p => p.Duration >= LongEnough).ToList();
        if (counted.Count < MinimumAttempts) yield break;

        foreach (var player in counted
            .SelectMany(p => p.Roster.Select(r => (Pull: p, Player: r)))
            .GroupBy(x => x.Player.Name, StringComparer.Ordinal))
        {
            var showings = player.ToList();
            if (showings.Count < MinimumAttempts) continue;

            bool heals = Specs.RoleOf(showings[0].Player.SpecId) == Role.Healer;
            var rates = showings
                .Select(s => (heals ? s.Player.Healing : s.Player.Damage) / s.Pull.Duration.TotalSeconds)
                .Where(r => r > 0)
                .ToList();

            if (rates.Count < MinimumAttempts) continue;

            double middle = Yardstick.Median(rates);
            if (middle <= 0) continue;

            double low = rates.Min();
            double high = rates.Max();
            if ((high - low) / middle < Wide) continue;

            yield return Report(attempts, showings[^1].Pull, showings[^1].Player,
                heals ? "healing" : "damage", low, high, middle);
        }
    }

    private Finding Report(Attempts attempts, PullRecord pull, PlayerStats player,
        string what, double low, double high, double middle)
        => new(
            Category,
            what + " swung between " + Display.Rate(low) + " and " + Display.Rate(high) + " a second",
            "the middle of the evening was " + Display.Rate(middle) + ", over " +
            Display.Count(attempts.Pulls.Count) + " attempts",
            "A spread, not a fault. Attempts that end early, a phase nobody reached twice, or a " +
            "night that went badly in patches all look like this - and so does a rotation that " +
            "only works sometimes.",
            Cost.Nothing("counted, not costed"),
            attempts.NumberOf(pull),
            pull,
            player.Name,
            player.SpecId,
            TimeSpan.Zero) { Timeless = true };
}
