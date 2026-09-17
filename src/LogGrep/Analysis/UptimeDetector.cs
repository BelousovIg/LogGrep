using LogGrep.Models;
using LogGrep.ViewModels;

namespace LogGrep.Analysis;

/// <summary>
/// Finds a buff somebody normally keeps up and did not.
///
/// The same argument a third time, and it is the one that makes this whole milestone possible: read
/// against themselves, a player needs no class to be judged. A buff held for nine tenths of every
/// attempt is one they mean to hold; the attempt where it was up for a third is the finding. What
/// the buff does, whether it stacks, which spec it belongs to - none of that has to be known, and
/// none of it can go out of date.
///
/// Only buffs a player put on themselves count. A raid buff somebody else maintains is not theirs
/// to answer for.
/// </summary>
public sealed class UptimeDetector : IDetector
{
    /// <summary>Attempts the buff has to appear in before there is a normal to depart from.</summary>
    private const int MinimumAttempts = 5;

    /// <summary>
    /// How much of a typical attempt they hold it before it counts as something they mean to keep
    /// up. Most of what a player carries is a proc or a trinket whose uptime is luck rather than a
    /// choice - on the real log, a looser bar here produced five hundred and fifty-one findings in
    /// one evening, which is not a report, it is a wall.
    /// </summary>
    private const double Maintained = 0.7;

    /// <summary>
    /// How far below their own usual the attempt has to fall. Measured against the median rather
    /// than their best attempt as the plan had it: a best is an outlier by construction, and every
    /// ordinary attempt falls short of one.
    /// </summary>
    private const double Dropped = 0.6;

    /// <summary>An attempt shorter than this says nothing about holding anything up.</summary>
    private static readonly TimeSpan LongEnough = TimeSpan.FromSeconds(60);

    public string Category => "uptime";

    public IEnumerable<Finding> Look(Attempts attempts)
    {
        foreach (var buff in Holdings(attempts).GroupBy(h => h.SpellId))
        {
            var held = buff.ToList();
            var yardstick = Yardstick.Of(
                held.Select(h => new Measured(h.Pull, h.Player, h.SpecId, h.Share)), MinimumAttempts);

            foreach (var holding in held)
            {
                var usual = yardstick.For(holding.Player, holding.SpecId);
                if (!usual.Exists || usual.Value < Maintained) continue;
                if (holding.Share >= usual.Value * Dropped) continue;

                yield return Report(attempts, holding, usual);
            }
        }
    }

    private Finding Report(Attempts attempts, Holding holding, Normal usual)
        => new(
            Category,
            holding.Spell + " up " + Display.Percent(holding.Share) + " of it",
            usual.Whose + " " + Display.Percent(usual.Value) + " of an attempt on this fight",
            "Nothing here knows what this buff does - only that you keep it up when things go " +
            "well, and on this attempt you did not.",
            Cost.Nothing("no damage to put on it"),
            attempts.NumberOf(holding.Pull),
            holding.Pull,
            holding.Player,
            holding.SpecId,
            TimeSpan.Zero);

    /// <summary>
    /// Every buff every player held, as a share of the attempt. Time after a death is left out of
    /// the denominator for the same reason it is left out of idleness: a corpse holds nothing, and
    /// counting that would turn one death into a page of findings.
    /// </summary>
    private static IEnumerable<Holding> Holdings(Attempts attempts)
    {
        foreach (var pull in attempts.Pulls)
        {
            if (pull.Duration < LongEnough) continue;

            foreach (var player in pull.Roster)
            {
                var up = player.Deaths.Count > 0 ? player.Deaths[0].At : pull.Duration;
                if (up < LongEnough) continue;

                foreach (var buff in player.Buffs)
                {
                    double share = Math.Min(1, buff.Held / up);
                    yield return new Holding(pull, player.Name, player.SpecId, buff.SpellId, buff.Spell, share);
                }
            }
        }
    }

    private readonly record struct Holding(
        PullRecord Pull, string Player, int SpecId, int SpellId, string Spell, double Share);
}
