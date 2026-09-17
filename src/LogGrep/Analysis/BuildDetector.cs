using LogGrep.Models;
using LogGrep.ViewModels;

namespace LogGrep.Analysis;

/// <summary>
/// Finds a talent change that the numbers did not agree with.
///
/// The log carries the whole tree, so what somebody ran is known exactly - and almost nothing can
/// be concluded from that on its own. The node and entry numbers name nothing a person would
/// recognise, they carry no spell, and whether a build is a good one is not answerable from one
/// raid night. What is answerable, and needs no reference data at all, is narrower: you changed
/// something between these attempts and these attempts, and your own output moved with it.
///
/// It never says which build is better in general or what to pick. It says the two performed
/// differently here, on this fight, for you - and leaves the reading of that to somebody who knows
/// what they changed.
/// </summary>
public sealed class BuildDetector : IDetector
{
    /// <summary>Attempts each build needs before its numbers mean anything.</summary>
    private const int MinimumAttempts = 3;

    /// <summary>How far apart the two have to be before the difference is worth a sentence.</summary>
    private const double Apart = 0.15;

    /// <summary>An attempt shorter than this says nothing about anybody's output.</summary>
    private static readonly TimeSpan LongEnough = TimeSpan.FromSeconds(60);

    public string Category => "builds";

    public IEnumerable<Finding> Look(Attempts attempts)
    {
        foreach (var player in Showings(attempts).GroupBy(s => s.Player, StringComparer.Ordinal))
        {
            var builds = player
                .GroupBy(s => s.Build)
                .Where(b => b.Count() >= MinimumAttempts)
                .Select(b => new
                {
                    Build = b.Key,
                    Rate = Yardstick.Median(b.Select(s => s.Rate).ToList()),
                    Attempts = b.Count(),
                    Last = b.OrderBy(s => s.Number).Last(),
                })
                .OrderBy(b => b.Last.Number)
                .ToList();

            if (builds.Count < 2) continue;

            // Only consecutive pairs: a night with three builds is two changes, not three comparisons.
            for (int i = 1; i < builds.Count; i++)
            {
                var before = builds[i - 1];
                var after = builds[i];
                if (before.Rate <= 0) continue;

                double moved = (after.Rate - before.Rate) / before.Rate;
                if (Math.Abs(moved) < Apart) continue;

                yield return Report(attempts, after.Last, before.Rate, after.Rate, moved,
                    before.Attempts, after.Attempts);
            }
        }
    }

    private Finding Report(Attempts attempts, Showing showing, double before, double after,
        double moved, int beforeCount, int afterCount)
        => new(
            Category,
            "changed talents, and your " + showing.Measure + " " +
            (moved > 0 ? "went up " : "went down ") + Display.Percent(Math.Abs(moved)),
            Display.Rate(before) + " a second over " + beforeCount + " attempts on the build before, " +
            Display.Rate(after) + " over " + afterCount + " on this one",
            "The log knows exactly which talents you ran and nothing whatever about what they are " +
            "supposed to do. This says only that the two builds did not perform the same here.",
            Cost.Nothing("no damage to put on it"),
            attempts.NumberOf(showing.Pull),
            showing.Pull,
            showing.Player,
            showing.SpecId,
            TimeSpan.Zero);

    /// <summary>
    /// One player on one attempt, measured by whichever number their role is actually judged on.
    /// A healer whose damage fell after a talent change has not told anybody anything.
    /// </summary>
    private static IEnumerable<Showing> Showings(Attempts attempts)
    {
        foreach (var pull in attempts.Pulls)
        {
            if (pull.Duration < LongEnough) continue;

            foreach (var player in pull.Roster)
            {
                if (player.Build == 0) continue;

                bool heals = Specs.RoleOf(player.SpecId) == Role.Healer;
                double rate = (heals ? player.Healing : player.Damage) / pull.Duration.TotalSeconds;
                if (rate <= 0) continue;

                yield return new Showing(pull, player.Name, player.SpecId, player.Build, rate,
                    heals ? "healing" : "damage", attempts.NumberOf(pull));
            }
        }
    }

    private readonly record struct Showing(
        PullRecord Pull, string Player, int SpecId, ulong Build, double Rate, string Measure, int Number);
}
