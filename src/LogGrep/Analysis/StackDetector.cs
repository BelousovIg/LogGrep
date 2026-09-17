using LogGrep.ViewModels;
using LogGrep.Models;

namespace LogGrep.Analysis;

/// <summary>
/// Finds the number of stacks a fight stops tolerating, when there is one.
///
/// Nothing anywhere says which debuffs are dangerous or at what count. Take every player's peak
/// stacks of one debuff across the evening and split them by whether they died soon after. If the
/// dead all sat above some number and the living all below it, that number is the fight's
/// tolerance - found rather than assumed, and it moves with the fight rather than with a table.
///
/// The honest answer is usually silence. On the log this was built against, not one debuff
/// separated: people died holding three of something and lived holding twenty of it, which means
/// stacks are not what kills on that fight and there is nothing here to say. A rule that spoke
/// anyway would be inventing a threshold, and the invented one would be wrong.
/// </summary>
public sealed class StackDetector : IDetector
{
    /// <summary>How soon after reaching a peak a death still counts as having followed from it.</summary>
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How many of each outcome it takes before a split is a split. Two deaths above two survivals
    /// is a coincidence with four data points in it.
    /// </summary>
    private const int Enough = 3;

    public string Category => "stacks";

    public IEnumerable<Finding> Look(Attempts attempts)
    {
        foreach (var spell in Peaks(attempts).GroupBy(p => p.SpellId))
        {
            var seen = spell.ToList();
            var died = seen.Where(p => p.Died).Select(p => p.Count).ToList();
            var lived = seen.Where(p => !p.Died).Select(p => p.Count).ToList();

            if (died.Count < Enough || lived.Count < Enough) continue;

            // Clean separation or nothing. Anything looser is a threshold pulled out of the air and
            // dressed up as arithmetic.
            int tolerance = died.Min();
            if (tolerance <= lived.Max()) continue;

            foreach (var peak in seen.Where(p => p.Count >= tolerance))
            {
                yield return Report(attempts, peak, tolerance, lived.Max());
            }
        }
    }

    private Finding Report(Attempts attempts, Held peak, int tolerance, int survived)
        => new(
            Category,
            "carried " + Display.Count(peak.Count) + " stacks of " + peak.Spell,
            "everybody who reached " + tolerance + " died within ten seconds; nobody who stopped at " +
            survived + " did",
            "Nothing here knows what this debuff does. It knows where this fight stopped " +
            "forgiving it, because the log drew the line itself.",
            peak.Death is { } death ? Cost.Death(death, peak.Spell) : Cost.Nothing("survived it"),
            attempts.NumberOf(peak.Pull),
            peak.Pull,
            peak.Player,
            peak.SpecId,
            peak.At,
            peak.SpellId);

    private static IEnumerable<Held> Peaks(Attempts attempts)
    {
        foreach (var pull in attempts.Pulls)
        {
            foreach (var player in pull.Roster)
            {
                foreach (var stack in player.Stacks)
                {
                    var death = player.Deaths
                        .Where(d => d.At >= stack.At && d.At - stack.At <= Soon)
                        .Select(d => (TimeSpan?)d.At)
                        .FirstOrDefault();

                    yield return new Held(pull, player.Name, player.SpecId, stack.SpellId, stack.Spell,
                        stack.Peak, stack.At, death);
                }
            }
        }
    }

    private readonly record struct Held(
        PullRecord Pull, string Player, int SpecId, int SpellId, string Spell,
        int Count, TimeSpan At, TimeSpan? Death)
    {
        public bool Died => Death.HasValue;
    }
}
