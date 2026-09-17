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

    /// <summary>How long below full counts as having been ground down rather than caught out.</summary>
    private static readonly TimeSpan Ground = TimeSpan.FromSeconds(15);

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
        bool bursted = death.BiggestShare >= Burst;
        bool ground = !bursted && death.Span >= Ground;

        return new Finding(
            Category,
            Headline(death, bursted, ground),
            Evidence(pull, death, others),
            Advice(bursted, ground),
            Cost.Death(death.At, death.Causes.Count > 0 ? death.Causes[0].Label : null),
            attempts.NumberOf(pull),
            pull,
            player.Name,
            player.SpecId,
            death.At);
    }

    private static string Headline(DeathRecord death, bool bursted, bool ground)
    {
        if (bursted)
        {
            string from = death.BiggestFrom.Length > 0 ? death.BiggestFrom : "one hit";
            return from + " took " + Display.Percent(death.BiggestShare) + " in one hit";
        }

        return ground
            ? "ground down over " + Display.Clock(death.Span)
            : "died while the raid fought on";
    }

    /// <summary>
    /// Why this reads as one person's death rather than the attempt ending. Both halves of the case
    /// are in it, because either one alone would be arguable.
    /// </summary>
    private static string Evidence(PullRecord pull, DeathRecord death, int others)
    {
        string alongside = others == 0
            ? "nobody else died within five seconds"
            : others + (others == 1 ? " other died" : " others died") + " within five seconds";

        return alongside + ", and the attempt ran " + Display.Clock(pull.Duration - death.At) + " longer";
    }

    private static string Advice(bool bursted, bool ground)
    {
        if (bursted) return "One hit took more than half of you. That is a defensive that was not " +
                            "pressed, or a hit that should not have been taken at all.";

        return ground
            ? "You were below full for a long time before this, which is as much a conversation for " +
              "the healers as for you."
            : "The raid fought on afterwards, so this was not the wipe taking you with it. What " +
              "landed is worth a look.";
    }
}
