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
        foreach (var spell in Uses(attempts).GroupBy(u => (u.Player, u.SpellId)))
        {
            var seen = spell.ToList();
            if (seen.Count < MinimumAttempts) continue;

            double usual = Median(seen.Select(u => u.PerMinute).ToList());
            if (usual <= 0 || usual > Filler) continue;

            foreach (var use in seen)
            {
                if (use.PerMinute > usual * Short) continue;

                double expected = usual * use.Minutes;
                if (expected - use.Count < Missing) continue;

                yield return Report(attempts, use, expected);
            }
        }
    }

    private Finding Report(Attempts attempts, Use use, double expected)
        => new(
            Category,
            "used " + use.Spell + " " + Times(use.Count) + " where " + Times(Math.Round(expected)) +
            " is your usual",
            "over " + attempts.Pulls.Count + " attempts at this fight you average " +
            use.Usual.ToString("0.#") + " a minute of it",
            "Nothing here knows what this spell does or when it should go out - only that you " +
            "normally get more of it out of an attempt this long than you did here.",
            Cost.Nothing("no damage to put on it"),
            attempts.NumberOf(use.Pull),
            use.Pull,
            use.Player,
            use.SpecId,
            TimeSpan.Zero);

    private static string Times(double count) => count switch
    {
        0 => "none",
        1 => "once",
        2 => "twice",
        _ => count.ToString("0") + " times",
    };

    private static IEnumerable<Use> Uses(Attempts attempts)
    {
        // The rate has to be worked out per attempt first, then compared - a long attempt naturally
        // holds more of everything, and counting raw uses would call every short pull a failure.
        var rates = new List<Use>();

        foreach (var pull in attempts.Pulls)
        {
            if (pull.Duration < LongEnough) continue;

            double minutes = pull.Duration.TotalMinutes;

            foreach (var player in pull.Roster)
            {
                foreach (var spell in player.Spells)
                {
                    rates.Add(new Use(pull, player.Name, player.SpecId, spell.SpellId, spell.Spell,
                        spell.Uses, minutes, spell.Uses / minutes, 0));
                }
            }
        }

        // Second pass fills in each player's own average for the spell, which is what the sentence
        // quotes back at them.
        foreach (var spell in rates.GroupBy(u => (u.Player, u.SpellId)))
        {
            double usual = Median(spell.Select(u => u.PerMinute).ToList());
            foreach (var use in spell) yield return use with { Usual = usual };
        }
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        int middle = values.Count / 2;

        return values.Count % 2 == 1
            ? values[middle]
            : (values[middle - 1] + values[middle]) / 2;
    }

    private readonly record struct Use(
        PullRecord Pull, string Player, int SpecId, int SpellId, string Spell,
        int Count, double Minutes, double PerMinute, double Usual);
}
