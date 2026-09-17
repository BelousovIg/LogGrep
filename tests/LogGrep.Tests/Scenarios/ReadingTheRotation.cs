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

    /// <summary>
    /// A night where one spell went out every half minute. Five uses an attempt, which is a
    /// cooldown's worth rather than a filler's.
    /// </summary>
    private static CombatLogBuilder ANightOfCooldowns(int times = 6, TimeSpan? every = null)
        => ARaid().Pulls(times, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .Casting("Nightblade", Ability.Reckoning, from: 0.Seconds(), to: 2.Minutes(),
                every: every ?? 30.Seconds())
            .Wipe());

    [Fact]
    public void A_spell_used_at_its_usual_rate_is_nobodys_mistake()
    {
        Given.IOpenedLog(ANightOfCooldowns(times: 8));

        Then.NothingWasFound();
    }

    [Fact]
    public void A_spell_that_barely_went_out_on_one_attempt_is_reported()
    {
        // Five uses every attempt, then two. Nothing here knows what Reckoning does.
        var log = ANightOfCooldowns().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .Casting("Nightblade", Ability.Reckoning, from: 0.Seconds(), to: 2.Minutes(), every: 2.Minutes())
            .Wipe());

        Given.IOpenedLog(log);

        Then.GotFewerUses("Nightblade", Ability.Reckoning)
            .And.TheCooldownFindingReads("Nightblade", "used Reckoning twice where 5 times is your usual");
    }

    [Fact]
    public void One_use_fewer_than_usual_is_not_worth_saying()
    {
        // Two an attempt, then one. That is the length of the pull or the phase it reached, and a
        // finding for it would bury the ones that mean something.
        var log = ANightOfCooldowns(every: 2.Minutes()).Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Casts("Nightblade", Ability.Reckoning)
            .Wipe());

        Given.IOpenedLog(log);

        Then.GotNoCooldownFinding("Nightblade");
    }

    [Fact]
    public void A_bit_below_the_usual_rate_is_not_a_finding()
    {
        // Seven an attempt, then four. Fewer, and by more than one - but not the half that says
        // something actually went wrong rather than the fight being what it was.
        var log = ARaid()
            .Pulls(6, Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(2.Minutes())
                .Casting("Nightblade", Ability.Reckoning, from: 0.Seconds(), to: 2.Minutes(), every: 20.Seconds())
                .Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(2.Minutes())
                .Casting("Nightblade", Ability.Reckoning, from: 0.Seconds(), to: 2.Minutes(), every: 40.Seconds())
                .Wipe());

        Given.IOpenedLog(log);

        Then.GotNoCooldownFinding("Nightblade");
    }

    [Fact]
    public void A_handful_of_attempts_is_not_a_usual_rate()
    {
        // Three attempts and a bad one. Four numbers do not make a habit to depart from.
        var log = ANightOfCooldowns(times: 3).Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .Casting("Nightblade", Ability.Reckoning, from: 0.Seconds(), to: 2.Minutes(), every: 2.Minutes())
            .Wipe());

        Given.IOpenedLog(log);

        Then.GotNoCooldownFinding("Nightblade");
    }

    [Fact]
    public void A_filler_cast_constantly_is_not_a_cooldown()
    {
        // Twenty a minute is not a cooldown, and half as many of it is a pace question - which the
        // idle rule already asks better.
        var log = ARaid()
            .Pulls(6, Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(2.Minutes())
                .Casting("Nightblade", Ability.Strike, from: 0.Seconds(), to: 2.Minutes(), every: 3.Seconds())
                .Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(2.Minutes())
                .Casting("Nightblade", Ability.Strike, from: 0.Seconds(), to: 2.Minutes(), every: 12.Seconds())
                .Wipe());

        Given.IOpenedLog(log);

        Then.GotNoCooldownFinding("Nightblade");
    }

    /// <summary>A night where one buff was held for nearly the whole of every attempt.</summary>
    private static CombatLogBuilder ANightOfUptime(int times = 6, TimeSpan? until = null)
        => ARaid().Pulls(times, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .Holds("Nightblade", Ability.Reckoning, from: 0.Seconds(), to: until ?? 1.Minutes(50))
            .Wipe());

    [Fact]
    public void A_buff_held_every_attempt_is_nobodys_mistake()
    {
        Given.IOpenedLog(ANightOfUptime(times: 8));

        Then.NothingWasFound();
    }

    [Fact]
    public void A_buff_that_fell_off_on_one_attempt_is_reported()
    {
        var log = ANightOfUptime().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .Holds("Nightblade", Ability.Reckoning, from: 0.Seconds(), to: 30.Seconds())
            .Wipe());

        Given.IOpenedLog(log);

        Then.LostUptime("Nightblade", Ability.Reckoning)
            .And.TheUptimeFindingReads("Nightblade", "Reckoning up 25% of it");
    }

    [Fact]
    public void A_buff_held_slightly_less_than_usual_is_not_a_finding()
    {
        // Ninety-two percent most attempts, eighty on this one. Nobody holds anything perfectly,
        // and a finding for that is a finding every attempt.
        var log = ANightOfUptime().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .Holds("Nightblade", Ability.Reckoning, from: 0.Seconds(), to: 1.Minutes(36))
            .Wipe());

        Given.IOpenedLog(log);

        Then.KeptTheirUptime("Nightblade");
    }

    [Fact]
    public void A_handful_of_attempts_is_not_a_usual_uptime()
    {
        // Three attempts and a bad one. Four numbers do not say what somebody normally holds.
        var log = ANightOfUptime(times: 3).Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .Holds("Nightblade", Ability.Reckoning, from: 0.Seconds(), to: 20.Seconds())
            .Wipe());

        Given.IOpenedLog(log);

        Then.KeptTheirUptime("Nightblade");
    }

    [Fact]
    public void A_buff_nobody_holds_up_anyway_is_not_judged()
    {
        // Up for a third of the fight every time. That is a proc or a trinket, and its uptime is
        // luck rather than a choice - so the attempt where it was worse says nothing.
        var log = ARaid()
            .Pulls(6, Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(2.Minutes())
                .Holds("Nightblade", Ability.Reckoning, from: 0.Seconds(), to: 40.Seconds())
                .Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(2.Minutes())
                .Holds("Nightblade", Ability.Reckoning, from: 0.Seconds(), to: 5.Seconds())
                .Wipe());

        Given.IOpenedLog(log);

        Then.KeptTheirUptime("Nightblade");
    }

    [Fact]
    public void A_buff_somebody_else_keeps_up_is_not_yours_to_answer_for()
    {
        // The healer holding something on the rogue is the healer's business. Only what a player
        // put on themselves is read as theirs.
        var log = ARaid()
            .Pulls(6, Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(2.Minutes())
                .At(0.Seconds()).Buffs("Sunwell", target: "Nightblade", with: Ability.PowerWordFortitude)
                .Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(2.Minutes()).Wipe());

        Given.IOpenedLog(log);

        Then.KeptTheirUptime("Nightblade");
    }

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
    public void Slightly_worse_than_usual_is_not_worth_saying()
    {
        // A tenth off the pace. The measure is coarse enough that calling this a mistake would be
        // reading noise aloud, so the bar is a half again rather than any difference at all.
        var log = ASteadyNight().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .Casting("Nightblade", Ability.Strike, from: 0.Seconds(), to: 2.Minutes(), every: 2.2.Seconds())
            .Casting("Emberwild", Ability.Strike, from: 0.Seconds(), to: 2.Minutes(), every: Steady)
            .Wipe());

        Given.IOpenedLog(log);

        Then.WasNotIdle("Nightblade");
    }

    [Fact]
    public void A_short_attempt_is_left_out_even_when_it_looks_terrible()
    {
        // Half a minute, and barely a cast in it. A pull that short is a reset, and judging a
        // rotation on it would put a finding on everybody who ever pulled and ran.
        var log = ASteadyNight().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(30.Seconds())
            .Casting("Nightblade", Ability.Strike, from: 0.Seconds(), to: 30.Seconds(), every: 10.Seconds())
            .Wipe());

        Given.IOpenedLog(log);

        Then.WasNotIdle("Nightblade");
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
