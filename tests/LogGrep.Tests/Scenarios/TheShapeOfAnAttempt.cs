using LogGrep.Models;
using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// The two lines that say what an attempt was: how far down the enemy went, and how many of the
/// group were still standing.
///
/// This is the one measurement in the whole plan that was not already being taken, and it is the
/// cheapest of them - the enemy states its own health on everything it does. Between them the lines
/// tell the story a table cannot: an enemy line that stops at half while the group line falls off a
/// cliff is a raid that melted, and one where both fall together is a fight that was close.
/// </summary>
public sealed class TheShapeOfAnAttempt : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue));

    [Fact]
    public void The_enemy_leaves_a_line_of_how_far_down_it_went()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .At(10.Seconds()).BossSwingsAt("Rockjaw", 200_000)
            .At(1.Minutes()).BossSwingsAt("Rockjaw", 200_000)
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        Then.TheAttemptHasAShape(true)
            .TheGroupStoodAt(0.Seconds(), 3);
    }

    [Fact]
    public void The_group_line_falls_as_they_go_down()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .At(10.Seconds()).BossSwingsAt("Rockjaw", 200_000)
            .At(30.Seconds()).Kills("Nightblade")
            .At(1.Minutes()).Kills("Sunwell")
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        Then.TheGroupStoodAt(0.Seconds(), 3)
            .TheGroupStoodAt(45.Seconds(), 2)
            .TheGroupStoodAt(90.Seconds(), 1);
    }

    [Fact]
    public void A_kill_is_marked_where_the_enemy_went_down()
    {
        // The end of a won fight. A wipe and a kill are the same shape until the last seconds of
        // them, and this is the mark that says which of the two is on the screen.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .At(1.Minutes()).Deals("Nightblade", to: Soulcoiler, amount: 900_000)
            .Kill());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        Then.TheKillIsMarkedAt(2.Minutes());
    }

    [Fact]
    public void A_wipe_has_nothing_to_mark()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        Then.TheKillIsMarkedAt(null);
    }

    [Fact]
    public void A_line_switched_off_stops_answering_in_the_hover()
    {
        // Turning a switch off is somebody saying they are not asking about that line. A hover that
        // keeps answering anyway is the reason they turned it off.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        Then.TheShapeSaysAt(30.Seconds(), "at 0:30", "standing 3 up");

        When.ISwitchOffTheLine("standing");

        Then.TheShapeSaysAt(30.Seconds(), "at 0:30");
    }

    [Fact]
    public void Narrowing_the_rows_narrows_the_chart_with_them()
    {
        // The chart sits over the table and has to be about the same people. A damage line drawn
        // over the whole raid above a table showing one healer is the group's answer beside one
        // person's question.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .At(30.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 900_000)
            .At(40.Seconds()).Heals("Sunwell", target: "Rockjaw", amount: 200_000)
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        Then.TheShapeOffers("standing", "damage", "healing");

        When.IAnalyseThePull(Soulcoiler, number: 1).INarrowTo(Role.Healer);

        Then.TheShapeOffers("standing", "healing")
            .TheShapeSaysAt(30.Seconds(), "at 0:30", "standing 1 up");
    }

    [Fact]
    public void A_fight_whose_enemy_never_acts_simply_has_no_line()
    {
        // Council fights and anything named after a group rather than a creature. An empty line is
        // the honest answer; a flat one at full health would be a claim nothing supports.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        Then.TheAttemptHasAShape(false);
    }
}
