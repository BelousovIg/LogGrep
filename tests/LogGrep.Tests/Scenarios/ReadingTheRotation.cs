using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// Whether the app can say somebody stood there doing nothing, without knowing one thing about
/// their class.
///
/// It never says what should have been cast. It says this attempt had far more standing about in it
/// than your other attempts on the same boss did - a sentence that means the same to a rogue and a
/// priest, and one the log can actually support.
/// </summary>
public sealed class ReadingTheRotation : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    /// <summary>A steady rotation: something out every two seconds.</summary>
    private static readonly TimeSpan Steady = 2.Seconds();

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue),
        Damage("Emberwild", Spec.ArcaneMage));

    /// <summary>A night of attempts where everybody kept casting at the same pace throughout.</summary>
    private static CombatLogBuilder ASteadyNight(int times = 5)
        => ARaid().Pulls(times, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .Casting("Nightblade", Ability.Strike, from: 0.Seconds(), to: 2.Minutes(), every: Steady)
            .Casting("Emberwild", Ability.Strike, from: 0.Seconds(), to: 2.Minutes(), every: Steady)
            .Wipe());

    [Fact]
    public void A_night_at_one_pace_is_nobodys_mistake()
    {
        Given.IOpenedLog(ASteadyNight(times: 8));

        Then.NothingWasFound();
    }

    [Fact]
    public void An_attempt_with_far_more_standing_about_than_usual_is_reported()
    {
        // The same fight, the same person, at a third of their own pace.
        var log = ASteadyNight().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .Casting("Nightblade", Ability.Strike, from: 0.Seconds(), to: 2.Minutes(), every: 6.Seconds())
            .Casting("Emberwild", Ability.Strike, from: 0.Seconds(), to: 2.Minutes(), every: Steady)
            .Wipe());

        Given.IOpenedLog(log);

        Then.WasIdle("Nightblade")
            .And.IdleEvidenceMentions("Nightblade", "you average on this fight")
            .And.NobodyElseWasIdle("Nightblade");
    }

    [Fact]
    public void A_player_who_is_always_slow_is_not_told_off_for_being_themselves()
    {
        // Every attempt at six seconds a cast. That is this player's normal - whatever spec waits
        // that long between casts, it is not the app's business to call it a mistake.
        var log = ARaid().Pulls(8, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .Casting("Nightblade", Ability.Strike, from: 0.Seconds(), to: 2.Minutes(), every: 6.Seconds())
            .Casting("Emberwild", Ability.Strike, from: 0.Seconds(), to: 2.Minutes(), every: Steady)
            .Wipe());

        Given.IOpenedLog(log);

        Then.NothingWasFound();
    }

    [Fact]
    public void Time_spent_dead_is_not_time_spent_standing_about()
    {
        // Killed a third of the way in, and of course cast nothing afterwards. Counting that as
        // idleness would hand everybody who dies a second finding for the privilege.
        var log = ASteadyNight().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .Casting("Nightblade", Ability.Strike, from: 0.Seconds(), to: 1.Minutes(10), every: Steady)
            .Casting("Emberwild", Ability.Strike, from: 0.Seconds(), to: 3.Minutes(), every: Steady)
            .At(1.Minutes(12)).Kills("Nightblade")
            .Wipe());

        Given.IOpenedLog(log);

        Then.WasNotIdle("Nightblade");
    }

    [Fact]
    public void A_short_attempt_says_nothing_about_anybodys_rotation()
    {
        // Half a minute of pull is not a rotation, it is a reset.
        var log = ASteadyNight().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(30.Seconds())
            .Wipe());

        Given.IOpenedLog(log);

        Then.WasNotIdle("Nightblade");
    }

    [Fact]
    public void One_night_is_not_enough_to_know_what_somebody_usually_does()
    {
        // Four attempts, one of them bad. Four is not a normal to depart from.
        var log = ASteadyNight(times: 3).Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .Casting("Nightblade", Ability.Strike, from: 0.Seconds(), to: 2.Minutes(), every: 10.Seconds())
            .Casting("Emberwild", Ability.Strike, from: 0.Seconds(), to: 2.Minutes(), every: Steady)
            .Wipe());

        Given.IOpenedLog(log);

        Then.NothingWasFound();
    }
}
