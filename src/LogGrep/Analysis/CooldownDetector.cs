using LogGrep.ViewModels;
using LogGrep.Models;

namespace LogGrep.Analysis;

/// <summary>
/// Finds an attempt where somebody got far fewer uses out of one of their own spells than they
/// usually do on that fight.
///
/// The plan for this was to read a cooldown off the log - the shortest gap ever seen between two
/// casts - and count uses against the length of the fight. The real log says that does not hold:
/// one spell showed a shortest gap of 0.0 seconds, another showed 15 where the game's own number is
/// 90, and two entirely different abilities shared a name. Resets, charges and procs all shorten a
/// gap, and a single one of them poisons the estimate for the whole evening.
///
/// So this asks the question the log can actually answer, which is the same question the idle rule
/// asks: against your own attempts at this boss, this one was different. No cooldown is claimed, no
/// class is known, and nothing here says which spell mattered - only that you got one where you
/// normally get three.
/// </summary>
public sealed class CooldownDetector : IDetector
{
    /// <summary>Attempts the spell has to appear in before there is a normal to depart from.</summary>
    private const int MinimumAttempts = 5;

    /// <summary>
    /// Above this many uses a minute it is a filler, not a cooldown, and one fewer of it means
    /// nothing. Cooldowns are the spells worth counting because missing one is a real loss.
    /// </summary>
    private const double Filler = 4;

    /// <summary>
    /// How much of a player's own output a spell has to account for before missing it is worth a
    /// sentence.
    ///
    /// Counting uses alone reads a spell nobody has a reason to press as a cooldown, because a
    /// spell nobody has a reason to press is used rarely. On the real log that put Holy Nova in the
    /// list - 3830 casts for five million healing across an evening, half a percent of what those
    /// healers did, 1427 a cast - beside Death Pact, which did nine million in twenty-five. And
    /// Charge, Fel Rush, Demonic Circle and Angelic Feather, which do nothing measurable at all and
    /// cannot be under-used in any sense this app can support.
    /// </summary>
    private const double Worthwhile = 0.02;

    /// <summary>How far below their own usual rate the attempt has to fall.</summary>
    private const double Short = 0.5;

    /// <summary>
    /// And by at least this many uses. One fewer than usual is the length of the attempt, the phase
    /// it reached, or a pull that went badly early - on the real log that alone produced a hundred
    /// and twenty-nine findings in an evening, which is a list nobody reads.
    /// </summary>
    private const double Missing = 2;

    /// <summary>An attempt shorter than this says nothing about how often anything was used.</summary>
    private static readonly TimeSpan LongEnough = TimeSpan.FromSeconds(60);

    public string Category => "cooldowns";

    public IEnumerable<Finding> Look(Attempts attempts)
    {
        // A baseline per spell: how often this person - or, failing enough attempts of their own,
        // somebody else of their spec - gets this particular spell out in a minute.
        foreach (var spell in Uses(attempts).GroupBy(u => u.SpellId))
        {
            // Some of them are fillers that hit hard enough to pass every test below. Pressing one
            // of those less often is what a better attempt looks like, and no rule drawn from a log
            // can see the difference - so the list says which they are.
            if (Fillers.NotWorthCounting(spell.Key)) continue;

            var seen = spell.ToList();
            var yardstick = Yardstick.Of(
                seen.Select(u => new Measured(u.Pull, u.Player, u.SpecId, u.PerMinute)), MinimumAttempts);

            // What this spell is worth to the people who cast it, over the whole evening. A spell
            // that does nothing, or almost nothing, is not a cooldown however rarely it goes out.
            long output = seen.Sum(u => u.Output);
            long theirs = seen.Sum(u => u.Whole);
            // A spell that did nothing fails this on its own - nothing is less than any share of
            // something - so there is no separate test for it.
            if (theirs <= 0 || output < theirs * Worthwhile) continue;

            long each = output / Math.Max(1, seen.Sum(u => u.Count));

            foreach (var use in seen)
            {
                var usual = yardstick.For(use.Player, use.SpecId);
                if (!usual.Exists || usual.Value <= 0 || usual.Value > Filler) continue;
                if (use.PerMinute > usual.Value * Short) continue;

                double expected = usual.Value * use.Minutes;
                if (expected - use.Count < Missing) continue;

                yield return Report(attempts, use with { Usual = usual.Value }, expected, usual, each);
            }
        }
    }

    private Finding Report(Attempts attempts, Use use, double expected, Normal usual, long each)
        => new(
            Category,
            "used " + use.Spell + " " + Times(use.Count) + " where " + Times(Math.Round(expected)) +
            " is usual",
            "over " + attempts.Pulls.Count + " attempts at this fight " + usual.Whose + " " +
            Display.Decimal(usual.Value) + " a minute of it",
            "Nothing here knows what this spell does or when it should go out - only that you " +
            "normally get more of it out of an attempt this long than you did here.",
            Cost.Missed((long)((expected - use.Count) * each)),
            attempts.NumberOf(use.Pull),
            use.Pull,
            use.Player,
            use.SpecId,
            TimeSpan.Zero) { Timeless = true };

    private static string Times(double count) => count switch
    {
        0 => "none",
        1 => "once",
        2 => "twice",
        _ => Display.Decimal(count) + " times",
    };

    private static IEnumerable<Use> Uses(Attempts attempts)
    {
        // The rate has to be worked out per attempt first, then compared - a long attempt naturally
        // holds more of everything, and counting raw uses would call every short pull a failure.
        foreach (var pull in attempts.Pulls)
        {
            if (pull.Duration < LongEnough) continue;

            double minutes = pull.Duration.TotalMinutes;

            foreach (var player in pull.Roster)
            {
                foreach (var spell in player.Spells)
                {
                    yield return new Use(pull, player.Name, player.SpecId, spell.SpellId, spell.Spell,
                        spell.Uses, minutes, spell.Uses / minutes, 0, spell.Output,
                        player.Damage + player.Healing);
                }
            }
        }
    }

    private readonly record struct Use(
        PullRecord Pull, string Player, int SpecId, int SpellId, string Spell,
        int Count, double Minutes, double PerMinute, double Usual, long Output = 0, long Whole = 0);
}
