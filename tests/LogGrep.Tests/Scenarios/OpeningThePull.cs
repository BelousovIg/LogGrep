using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// Who started the fight, and who the fight started on.
///
/// A pull belongs to the tank at both ends: they land the first blow, and they take it. Whoever
/// strikes first has the threat, and whoever the boss hits first had it - so if either of those is
/// somebody else, the attempt opened wrong.
///
/// This is the one rule in the app that needs no run of attempts and no baseline. It is not a habit
/// learned from a night, it is a fact about one moment - and only that moment, because threat
/// changes hands all fight long for perfectly good reasons.
/// </summary>
public sealed class OpeningThePull : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue));

    [Fact]
    public void A_tank_opening_and_taking_the_first_hit_is_how_it_should_go()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .At(2.Seconds()).BossHits("Rockjaw", 200_000)
            .At(5.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 900_000)
            .Wipe());

        Given.IOpenedLog(log);

        Then.ThePullWasClean();
    }

    [Fact]
    public void Somebody_who_opened_ahead_of_the_tank_is_told()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 900_000)
            .At(3.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .Wipe());

        Given.IOpenedLog(log);

        Then.ThePullSays("Nightblade", "opened the pull");
    }

    [Fact]
    public void Somebody_the_boss_hit_before_the_tank_is_told()
    {
        // The tank opened correctly, and the boss still went for somebody else - which means the
        // threat was not where it should have been when the fight began.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .At(3.Seconds()).BossHits("Sunwell", 300_000)
            .At(8.Seconds()).BossHits("Rockjaw", 200_000)
            .Wipe());

        Given.IOpenedLog(log);

        Then.ThePullSays("Sunwell", "took the first hit of the pull");
    }

    [Fact]
    public void Everybody_starting_at_once_is_just_a_pull()
    {
        // A pull is a scramble: a spell cast before the fight lands the moment it begins, while the
        // tank is still closing the distance. Being in front of somebody who has not had time to
        // act is not being early, and on the real log this is what almost every attempt looks like.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 900_000)
            .At(1.0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .At(1.4.Seconds()).BossHits("Sunwell", 300_000)
            .At(2.5.Seconds()).BossHits("Rockjaw", 200_000)
            .Wipe());

        Given.IOpenedLog(log);

        Then.ThePullWasClean();
    }

    [Fact]
    public void A_pull_nobody_tanked_belongs_to_whoever_started_it()
    {
        // The tank never touched the enemy at all. There is no gap to measure and no need for one:
        // whoever opened held the threat for the whole attempt because nobody took it off them.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 900_000)
            .At(4.Seconds()).Deals("Sunwell", to: Soulcoiler, amount: 50_000)
            .Wipe());

        Given.IOpenedLog(log);

        Then.ThePullSays("Nightblade", "opened the pull");
    }

    [Fact]
    public void Threat_changing_hands_later_in_the_fight_is_not_a_bad_pull()
    {
        // A tank swap, an add picked up, a second boss - all of it looks like this and none of it
        // is a bad pull. Only the opening seconds are read this way.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .At(2.Seconds()).BossHits("Rockjaw", 200_000)
            .At(1.Minutes()).BossHits("Nightblade", 300_000)
            .Wipe());

        Given.IOpenedLog(log);

        Then.ThePullWasClean();
    }

    [Fact]
    public void A_first_hit_that_came_a_minute_in_is_not_the_opening()
    {
        // Nothing touched the group for a minute, and then the boss went for the rogue. That is a
        // mechanic, or an add, or a phase - it is not how the pull began.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .At(1.Minutes()).BossHits("Nightblade", 300_000)
            .Wipe());

        Given.IOpenedLog(log);

        Then.ThePullWasClean();
    }

    [Fact]
    public void A_group_with_no_tank_in_it_cannot_pull_wrong()
    {
        var log = new CombatLogBuilder()
            .Raid(
                Healer("Sunwell", Spec.HolyPriest),
                Damage("Nightblade", Spec.AssassinationRogue))
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(2.Minutes())
                .At(0.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 900_000)
                .At(2.Seconds()).BossHits("Nightblade", 200_000)
                .Wipe());

        Given.IOpenedLog(log);

        Then.ThePullWasClean();
    }

    [Fact]
    public void Opening_and_then_dying_for_it_costs_a_death()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 900_000)
            .At(6.Seconds()).Kills("Nightblade")
            .Wipe());

        Given.IOpenedLog(log);

        Then.ThePullCost("Nightblade", "died at 0:06");
    }
}
