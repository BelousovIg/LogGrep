using LogGrep.Models;
using LogGrep.ViewModels;

namespace LogGrep.Analysis;

/// <summary>
/// Finds deaths that were somebody's own.
///
/// The first question about a death is not what killed them, it is whether it was their death at
/// all. When most of the raid falls inside a few seconds the attempt ended - the damage check was
/// missed, the timer ran out - and nobody made a personal mistake worth reporting. When one player
/// dies at 8:01 and the rest fight on to 9:30, that death is theirs.
///
/// Getting this wrong in the direction of reporting too much is far worse than missing some: a tool
/// that hands out eighteen findings for one wipe gets closed and not reopened. So both tests have to
/// pass - few others died alongside, and the attempt carried on afterwards - and a death that fails
/// either one is left alone.
///
/// Unlike the other detectors this one needs no run of attempts behind it. It is not drawing a rule
/// out of a habit; it is reading one death against the attempt it happened in.
/// </summary>
public sealed class DeathDetector : IDetector
{
    /// <summary>How close in time two deaths have to be to count as the same event.</summary>
    private static readonly TimeSpan Together = TimeSpan.FromSeconds(5);

    /// <summary>What share of the group falling at once makes it the raid's death rather than one person's.</summary>
    private const double CrowdShare = 0.25;

    /// <summary>
    /// How much longer the attempt has to run before a death counts as survivable by the rest. A
    /// death twenty seconds from the end of a wipe is the wipe, whoever happened to fall first.
    /// </summary>
    private static readonly TimeSpan FoughtOn = TimeSpan.FromSeconds(20);

    /// <summary>How much of a health pool one hit has to take before it is a burst rather than a share.</summary>
    private const double Burst = 0.5;

    /// <summary>
    /// How much of a health pool can go in the last couple of seconds before the death was decided
    /// there. A whole bar at once is nobody's reaction time - it is the damage that is the question.
    /// </summary>
    private const double Sudden = 1.0;

    /// <summary>
    /// How close to the demonstrated healing ceiling the incoming damage has to run before healing
    /// was never going to cover it. Below the ceiling and the question is why little healing reached
    /// this player; above it the question moves back a step, to why that much landed at all.
    /// </summary>
    private const double PastSaving = 0.8;

    /// <summary>How long below full counts as having been ground down rather than caught out.</summary>
    private static readonly TimeSpan Ground = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How far past what the rest of the group was carrying a stack count has to be before it is
    /// worth naming as the likely reason. Everybody who dies was carrying more of something than
    /// average - that is most of what dying means - so plainly "above average" would append a guess
    /// to every death. Half again above is a number that stands out of the night rather than out of
    /// the arithmetic.
    ///
    /// Tanks are left out of the average entirely: they hold more of everything by design, and
    /// including them would raise the bar until nothing ever cleared it.
    /// </summary>
    private const double Carrying = 1.5;

    /// <summary>And below this many stacks the comparison is between two small numbers.</summary>
    private const int Noticeable = 3;

    /// <summary>How recently the stacks have to have been climbing to be part of this death.</summary>
    private static readonly TimeSpan Recently = TimeSpan.FromSeconds(20);

    public string Category => "deaths";

    public IEnumerable<Finding> Look(Attempts attempts)
    {
        foreach (var pull in attempts.Pulls)
        {
            var times = pull.Roster.SelectMany(p => p.Deaths.Select(d => d.At)).ToList();
            int group = Math.Max(1, pull.Roster.Count);

            foreach (var player in pull.Roster)
            {
                foreach (var death in player.Deaths)
                {
                    int alongside = times.Count(t => (t - death.At).Duration() <= Together);
                    if (alongside >= group * CrowdShare) continue;
                    if (pull.Duration - death.At < FoughtOn) continue;

                    yield return Report(attempts, pull, player, death, alongside - 1);
                }
            }
        }
    }

    private Finding Report(Attempts attempts, PullRecord pull, PlayerStats player, DeathRecord death, int others)
    {
        var shape = Shape(attempts, death);

        return new Finding(
            Category,
            Headline(death, shape),
            Evidence(pull, death, others, attempts.HealingCeiling, shape) + Carried(pull, player, death),
            Advice(shape),
            Cost.Death(death.At, death.Causes.Count > 0 ? death.Causes[0].Label : null),
            attempts.NumberOf(pull),
            pull,
            player.Name,
            player.SpecId,
            death.At);
    }

    /// <summary>
    /// Which of the four this death was. The order is the order of certainty: a whole health bar
    /// gone at once, or one hit taking half of it, needs no ceiling to settle. Only when neither
    /// holds does the arithmetic against what the healers have demonstrated get a say.
    /// </summary>
    private static Kind Shape(Attempts attempts, DeathRecord death)
    {
        if (death.SuddenShare >= Sudden) return Kind.Sudden;
        if (death.BiggestShare >= Burst) return Kind.Burst;

        double ceiling = attempts.HealingCeiling;
        if (ceiling > 0 && death.Rate >= ceiling * PastSaving) return Kind.PastSaving;

        return death.Span >= Ground ? Kind.Ground : Kind.Plain;
    }

    private static string Headline(DeathRecord death, Kind shape) => shape switch
    {
        Kind.Sudden => "lost " + Display.Percent(death.SuddenShare) + " in two seconds",
        Kind.Burst => (death.BiggestFrom.Length > 0 ? death.BiggestFrom : "one hit") +
                      " took " + Display.Percent(death.BiggestShare) + " in one hit",
        Kind.PastSaving => "took more than the healers have ever covered",
        Kind.Ground => "ground down over " + Display.Clock(death.Span),
        _ => "died while the raid fought on",
    };

    private enum Kind
    {
        Plain,
        Sudden,
        Burst,
        PastSaving,
        Ground,
    }

    /// <summary>
    /// Why this reads as one person's death rather than the attempt ending. Both halves of the case
    /// are in it, because either one alone would be arguable.
    /// </summary>
    private static string Evidence(PullRecord pull, DeathRecord death, int others, double ceiling, Kind shape)
    {
        string alongside = others == 0
            ? "nobody else died within five seconds"
            : others + (others == 1 ? " other died" : " others died") + " within five seconds";

        string mine = alongside + ", and the attempt ran " + Display.Clock(pull.Duration - death.At) + " longer";

        // The arithmetic only belongs in the sentence when it is what decided the answer.
        return shape == Kind.PastSaving
            ? mine + "; " + Display.Rate(death.Rate) + " a second incoming against the " +
              Display.Rate(ceiling) + " the healers have landed at their best"
            : mine;
    }

    /// <summary>
    /// A likely reason rather than a proven one, and it says so. If somebody died holding far more
    /// of a debuff than the rest of the group was carrying, that is worth naming - but one attempt
    /// cannot prove a stack count kills, which is why this reads as "likely" and why the rule that
    /// does claim a threshold needs a run of attempts and a clean split behind it.
    ///
    /// The comparison is against the damage and the healers only. A tank carrying twelve of
    /// something while everybody else carries two is a tank; it says nothing about why anybody died.
    /// </summary>
    private static string Carried(PullRecord pull, PlayerStats player, DeathRecord death)
    {
        var worst = player.Stacks
            .Where(s => s.At <= death.At && death.At - s.At <= Recently)
            .OrderByDescending(s => s.Peak)
            .FirstOrDefault();

        if (worst.Peak < Noticeable) return string.Empty;

        var group = pull.Roster
            .Where(p => Specs.RoleOf(p.SpecId) != Role.Tank && !string.Equals(p.Name, player.Name, StringComparison.Ordinal))
            .Select(p => p.Stacks.FirstOrDefault(s => s.SpellId == worst.SpellId).Peak)
            .ToList();

        if (group.Count == 0) return string.Empty;

        double usual = group.Average();
        if (worst.Peak < Math.Max(Noticeable, usual * Carrying)) return string.Empty;

        return "; likely the " + Display.Count(worst.Peak) + " stacks of " + worst.Spell + " on you, against " +
               Display.Decimal(usual) + " on the rest of the group";
    }

    private static string Advice(Kind shape) => shape switch
    {
        Kind.Sudden => "A whole health bar went in two seconds. Nobody reacts to that - the " +
                       "question is what was allowed to land, not who failed to heal it.",
        Kind.Burst => "One hit took more than half of you. That is a defensive that was not " +
                      "pressed, or a hit that should not have been taken at all.",
        Kind.PastSaving => "More was coming in than this group has ever healed through, so this " +
                           "is not the healers. Ask why that much reached you.",
        Kind.Ground => "You were below full for a long time before this, which is as much a " +
                       "conversation for the healers as for you.",
        _ => "The raid fought on afterwards, so this was not the wipe taking you with it. What " +
             "landed is worth a look.",
    };
}
