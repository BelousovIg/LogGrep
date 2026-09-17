using LogGrep.Models;
using LogGrep.ViewModels;

namespace LogGrep.Analysis;

/// <summary>
/// Finds attempts where somebody stood there doing nothing, by their own standard.
///
/// This knows nothing about any class and never will. It does not say what should have been cast -
/// it says that on this attempt you cast nothing for far longer than you usually do on this fight,
/// which is a question worth asking of a rogue and a priest in exactly the same words.
///
/// Everything is measured against the same player on the same boss. A spec that spends time waiting
/// on a proc has a high idle share every attempt, and that is its normal; what shows here is the
/// attempt that broke from it.
/// </summary>
public sealed class IdleDetector : IDetector
{
    /// <summary>Attempts needed before a player's own normal is a thing that exists.</summary>
    private const int MinimumAttempts = 5;

    /// <summary>
    /// How far past their own usual share the idle time has to run. A half again is deliberately
    /// generous: the measure is coarse, and an app that calls a bad minute "standing about" on thin
    /// evidence earns nothing but an argument.
    /// </summary>
    private const double Worse = 1.5;

    /// <summary>Below this the difference is not worth a sentence, however it compares.</summary>
    private static readonly TimeSpan Noticeable = TimeSpan.FromSeconds(15);

    /// <summary>An attempt shorter than this says nothing about anybody's rotation.</summary>
    private static readonly TimeSpan LongEnough = TimeSpan.FromSeconds(60);

    public string Category => "idle";

    public IEnumerable<Finding> Look(Attempts attempts)
    {
        var shown = Showings(attempts).ToList();
        var yardstick = Yardstick.Of(
            shown.Select(s => new Measured(s.Pull, s.Player, s.SpecId, s.Share)), MinimumAttempts);

        foreach (var showing in shown)
        {
            var usual = yardstick.For(showing.Player, showing.SpecId);
            if (!usual.Exists || usual.Value <= 0) continue;

            if (showing.Share < usual.Value * Worse) continue;
            if (showing.Idle < Noticeable) continue;

            yield return Report(attempts, showing, usual);
        }
    }

    private Finding Report(Attempts attempts, Showing showing, Normal usual)
        => new(
            Category,
            "cast nothing for " + Display.Clock(showing.Idle) + " of it",
            "that is " + Display.Percent(showing.Share) + " of the attempt against the " +
            Display.Percent(usual.Value) + " " + usual.Whose + " on this fight",
            "Nothing here says what you should have cast - only that this attempt had far more " +
            "standing about in it than your others did. Movement, a death you were waiting out, or " +
            "a rotation that fell apart all look like this.",
            Cost.Nothing("no damage to put on it"),
            attempts.NumberOf(showing.Pull),
            showing.Pull,
            showing.Name,
            showing.SpecId,
            TimeSpan.Zero);

    /// <summary>
    /// One player on one attempt, as long as the attempt is worth reading at all. Time after a
    /// death does not count: a corpse casting nothing is not a rotation problem, and including it
    /// would make every death read as idleness on top of everything else.
    /// </summary>
    private static IEnumerable<Showing> Showings(Attempts attempts)
    {
        foreach (var pull in attempts.Pulls)
        {
            if (pull.Duration < LongEnough) continue;

            foreach (var player in pull.Roster)
            {
                var up = player.Deaths.Count > 0 ? player.Deaths[0].At : pull.Duration;
                if (up < LongEnough) continue;

                var idle = TimeSpan.FromSeconds(Math.Min(player.DeadSeconds, up.TotalSeconds));
                yield return new Showing(pull, player.Name, player.SpecId, idle, idle / up);
            }
        }
    }

    private readonly record struct Showing(
        PullRecord Pull, string Name, int SpecId, TimeSpan Idle, double Share)
    {
        public string Player => Name;
    }
}
