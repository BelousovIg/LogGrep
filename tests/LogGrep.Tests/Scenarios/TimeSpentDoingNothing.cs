using LogGrep.Models;
using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// How much of a fight somebody spent casting nothing.
///
/// It used to be a finding, measured against that player's own usual share and reported only when
/// an attempt broke from it. That answered a question nobody was asking - the useful reading is the
/// plain seconds, in a column, next to everybody else's, where a spec that waits on procs is
/// obviously a spec that waits on procs and a minute of standing about is obviously a minute.
///
/// One global cooldown is forgiven after every cast, unhasted, so the figure under-counts. That is
/// the direction to be wrong in when the output is "you stood there doing nothing".
/// </summary>
public sealed class TimeSpentDoingNothing : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue));

    [Fact]
    public void A_cast_every_six_seconds_leaves_four_and_a_half_of_each_of_them()
    {
        // Twenty-one casts over two minutes: twenty gaps of six seconds, less the cooldown a cast
        // costs, is ninety seconds with nothing in them.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .Casting("Nightblade", Ability.Strike, from: 0.Seconds(), to: 2.Minutes(), every: 6.Seconds())
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerIdleIs("90s");
    }

    [Fact]
    public void Time_spent_dead_is_not_time_spent_standing_about()
    {
        // Killed at 1:12 of a three-minute pull and of course casting nothing afterwards. Counting
        // the corpse would say they stood about for two minutes; a corpse has no rotation.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .Casting("Nightblade", Ability.Strike, from: 0.Seconds(), to: 1.Minutes(10), every: 2.Seconds())
            .At(1.Minutes(12)).Kills("Nightblade")
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Nightblade");

        // Thirty-five gaps of two seconds, and the two seconds between the last cast and the death.
        Then.PlayerIdleIs("18s");
    }

    [Fact]
    public void Somebody_who_got_back_up_is_measured_over_both_stretches()
    {
        // Down at 1:12, on their feet again at 2:00 - a battle rez, which the log never says in so
        // many words and which shows up as something hitting them again. Both stretches they were
        // up for count, and the minute in between does not.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .Casting("Nightblade", Ability.Strike, from: 0.Seconds(), to: 1.Minutes(10), every: 2.Seconds())
            .At(1.Minutes(12)).Kills("Nightblade")
            .At(2.Minutes()).BossHits("Nightblade", 1000)
            .Casting("Nightblade", Ability.Strike, from: 2.Minutes(), to: 3.Minutes(), every: 2.Seconds())
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Nightblade");

        // Eighteen seconds in the first stretch, fifteen in the second.
        Then.PlayerIdleIs("33s");
    }

    [Fact]
    public void Somebody_who_never_cast_anything_stood_about_for_all_of_it()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(10.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerIdleIs("119s");
    }
}
