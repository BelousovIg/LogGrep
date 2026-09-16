using LogGrep.Models;

namespace LogGrep.Analysis;

/// <summary>
/// Finds casts that went off where they usually do not.
///
/// The same argument once more, and again with nothing written down about any boss: a cast the
/// group stops eleven times out of thirteen is one the group has decided to stop, and the two that
/// went through are the two worth talking about. A cast nobody ever stops is either unstoppable or
/// not worth stopping, and either way this says nothing about it.
///
/// It is addressed to whoever usually stops it. That is not the same as knowing whose turn it was -
/// the log does not carry a kick rotation - but somebody who lands most of the interrupts on a
/// spell all night is the person who will recognise the one that got away.
/// </summary>
public sealed class InterruptDetector : IDetector
{
    /// <summary>Below this many attempts an encounter has shown noise, not a habit.</summary>
    private const int MinimumAttempts = 10;

    /// <summary>How much of the time a cast has to be stopped before letting one through is a miss.</summary>
    private const double UsuallyStopped = 0.85;

    /// <summary>
    /// How much of the stopping one person has to be doing, strictly, before the miss is put to
    /// them. Two people splitting a kick evenly land exactly half each, and that is the case this
    /// number exists to refuse: the log carries no kick rotation, so a finding sent to one of them
    /// would be a coin toss, and a finding addressed to the wrong person is worse than none.
    /// </summary>
    private const double TheirJob = 0.5;

    public string Category => "interrupts";

    public IEnumerable<Finding> Look(Attempts attempts)
    {
        foreach (var spell in Casts(attempts).GroupBy(c => c.Cast.SpellId))
        {
            var casts = spell.ToList();

            // Counted in attempts, not in casts: a spell that goes out eight times on one attempt
            // has still only been seen once.
            if (casts.Select(c => c.Pull).Distinct().Count() < MinimumAttempts) continue;

            var stopped = casts.Where(c => c.Cast.Stopped).ToList();
            if (stopped.Count / (double)casts.Count < UsuallyStopped) continue;

            var regular = stopped
                .GroupBy(c => c.Cast.By, StringComparer.Ordinal)
                .OrderByDescending(g => g.Count())
                .First();

            bool theirs = regular.Count() / (double)stopped.Count > TheirJob;

            string evidence = theirs
                ? stopped.Count + " of " + casts.Count + " were stopped, " + regular.Count() + " of them by you"
                : stopped.Count + " of " + casts.Count + " were stopped, and no one person does most " +
                  "of the stopping";

            foreach (var missed in casts.Where(c => !c.Cast.Stopped))
            {
                yield return Report(attempts, missed, theirs ? regular.Key : string.Empty, evidence);
            }
        }
    }

    private Finding Report(Attempts attempts, Seen missed, string player, string evidence)
        => new(
            Category,
            missed.Cast.Spell + " - interrupt missed",
            evidence,
            player.Length > 0
                ? "This one is usually stopped, and mostly by you. Whatever it does afterwards is " +
                  "not the healers' to undo."
                : "This one is usually stopped. Whoever was due to take it did not, and whatever it " +
                  "does afterwards is not the healers' to undo.",
            Cost.Nothing("the cast went off"),
            attempts.NumberOf(missed.Pull),
            missed.Pull,
            player,
            attempts.SpecOf(missed.Pull, player),
            missed.Cast.At);

    private static IEnumerable<Seen> Casts(Attempts attempts)
    {
        foreach (var pull in attempts.Pulls)
        {
            foreach (var cast in pull.Casts) yield return new Seen(pull, cast);
        }
    }

    private readonly record struct Seen(PullRecord Pull, CastRecord Cast);
}
