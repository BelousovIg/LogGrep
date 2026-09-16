using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// Whether the app can tell a cast that got away from one that was never going to be stopped.
/// Nothing here says which spells are interruptible; what the log shows is that this group stops
/// this one almost every time, and the times it did not are the times worth reading.
/// </summary>
public sealed class FindingMissedInterrupts : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;
    private const Ability Incantation = Ability.GrimIncantation;

    private const int Habit = 12;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue),
        Damage("Stormfist", Spec.WindwalkerMonk),
        Damage("Emberwild", Spec.ArcaneMage));

    /// <summary>A run of attempts where the rogue stopped it every time.</summary>
    private static CombatLogBuilder ANightOfCleanKicks(int times = Habit, string by = "Nightblade")
        => ARaid().Pulls(times, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(40.Seconds()).Interrupts(by, Incantation)
            .Wipe());

    [Fact]
    public void A_cast_the_group_always_stops_is_nobodys_mistake()
    {
        Given.IOpenedLog(ANightOfCleanKicks());

        Then.NothingWasFound();
    }

    [Fact]
    public void The_one_that_got_through_is_put_to_whoever_usually_stops_it()
    {
        var log = ANightOfCleanKicks().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(40.Seconds()).BossCasts(Incantation)
            .Wipe());

        Given.IOpenedLog(log);

        Then.InterruptWasMissed(Incantation)
            .And.TookMechanicOutOfTurn(Incantation, "Nightblade")
            .And.MechanicEvidenceReads(Incantation, "12 of 13 were stopped, 12 of them by you")
            .And.FindingPointsAt(Incantation, "Nightblade", pull: 13, at: 40.Seconds());
    }

    [Fact]
    public void A_run_too_short_to_be_a_habit_says_nothing()
    {
        // Eight kicks and a miss. A group that has stopped something eight times has not yet shown
        // that stopping it is what they do.
        var log = ANightOfCleanKicks(times: 8).Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(40.Seconds()).BossCasts(Incantation)
            .Wipe());

        Given.IOpenedLog(log);

        Then.NothingWasFound();
    }

    [Fact]
    public void A_cast_nobody_ever_stops_is_not_a_missed_interrupt()
    {
        // Unstoppable, or not worth stopping. Either way the log has no opinion about it, and an
        // app that invented one would be telling a raid to interrupt a spell it cannot.
        var log = ARaid().Pulls(Habit, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(40.Seconds()).BossCasts(Incantation)
            .Wipe());

        Given.IOpenedLog(log);

        Then.NothingWasFound();
    }

    [Fact]
    public void A_cast_the_group_only_sometimes_stops_is_left_alone()
    {
        // Half and half is not a habit; it is a spell the group kicks when somebody is free.
        var log = ARaid()
            .Pulls(6, Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes())
                .At(40.Seconds()).Interrupts("Nightblade", Incantation)
                .Wipe())
            .Pulls(6, Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes())
                .At(40.Seconds()).BossCasts(Incantation)
                .Wipe());

        Given.IOpenedLog(log);

        Then.NothingWasFound();
    }

    [Fact]
    public void Nobody_is_named_when_the_stopping_was_shared_evenly()
    {
        // Two people taking turns, and then one got through. It still gets reported - it happened -
        // but with nobody's name on it: the log carries no kick rotation, so naming one of the two
        // would be a coin toss.
        var log = ARaid()
            .Pulls(6, Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes())
                .At(40.Seconds()).Interrupts("Nightblade", Incantation)
                .Wipe())
            .Pulls(6, Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes())
                .At(40.Seconds()).Interrupts("Stormfist", Incantation)
                .Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes())
                .At(40.Seconds()).BossCasts(Incantation)
                .Wipe());

        Given.IOpenedLog(log);

        Then.InterruptWasMissed(Incantation)
            .And.NobodyWasNamedFor(Incantation)
            .And.MechanicEvidenceReads(Incantation, "12 of 13 were stopped, and no one person does most of the stopping");
    }

    [Fact]
    public void What_the_group_itself_casts_is_never_counted()
    {
        // Only the enemy's casts are anybody's to stop.
        var log = ARaid().Pulls(Habit, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(40.Seconds()).Heals("Sunwell", target: "Rockjaw", amount: 100_000)
            .Wipe());

        Given.IOpenedLog(log);

        Then.NothingWasFound();
    }
}
