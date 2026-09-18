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

    /// <summary>The same three, in a key. A run is one row holding however many bosses it got to.</summary>
    private static CombatLogBuilder AParty() => ARaid();

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

        Then.TheKillsAreMarkedAt(2.Minutes())
            .TheKillsAreOf(Soulcoiler);
    }

    [Fact]
    public void A_wipe_has_nothing_to_mark()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        Then.TheKillsAreMarkedAt();
    }

    [Fact]
    public void A_key_marks_every_boss_that_went_down_inside_it()
    {
        // A keystone run is one row holding a whole dungeon - the game writes each boss inside it as
        // its own encounter, and the run swallows all of them. Without these marks its half hour is
        // an unbroken line with nothing on it.
        var log = AParty().Keystone(Dungeon.TheRookery, level: 12, p => p
            .Lasting(20.Minutes())
            .At(5.Minutes()).Downs(Soulcoiler, lasting: 2.Minutes())
            .At(12.Minutes()).Downs(Boss.EntombedSentinels, lasting: 3.Minutes())
            .Kill());

        Given.IOpenedLog(log).IOpenedPull(Dungeon.TheRookery, number: 1);

        Then.TheKillsAreMarkedAt(7.Minutes(), 15.Minutes())
            .TheKillsAreOf(Soulcoiler, Boss.EntombedSentinels);
    }

    [Fact]
    public void A_boss_the_key_did_not_put_down_leaves_no_mark()
    {
        // Wiping on the third boss and leaving is the ordinary end of a key. The two that did go
        // down are still marked; the one that did not is not.
        var log = AParty().Keystone(Dungeon.TheRookery, level: 12, p => p
            .Lasting(20.Minutes())
            .At(5.Minutes()).Downs(Soulcoiler, lasting: 2.Minutes())
            .At(12.Minutes()).Wiped(Boss.EntombedSentinels, lasting: 3.Minutes())
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Dungeon.TheRookery, number: 1);

        Then.TheKillsAreOf(Soulcoiler);
    }

    [Fact]
    public void A_point_on_a_line_is_the_second_up_to_it()
    {
        // Two hits inside the same second, neither of them on a whole second. What somebody reading
        // a point at 0:11 means by it is "the second up to 0:11", and both of these are in it - the
        // other way round has a value at a moment describing a second that has not happened yet.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(10.4.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 600_000)
            .At(10.6.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 600_000)
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        Then.TheLineReadsAt("damage", 11.Seconds(), "damage 1.2M/s")
            .TheLineReadsAt("damage", 10.Seconds(), "damage —/s");
    }

    [Fact]
    public void A_rate_is_drawn_smoothed_and_read_exact()
    {
        // One burst in one second is a spike that says something about that swing and nothing about
        // the fight, so the line is drawn as a rolling mean over five seconds - 1.2M spread over the
        // five reads as 240K - while the hover still answers for the second it is on.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(10.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 1_200_000)
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        Then.TheLineReadsAt("damage", 10.Seconds(), "damage 1.2M/s")
            .TheLineIsDrawnAt("damage", 10.Seconds(), "240K/s");
    }

    [Fact]
    public void A_headcount_is_not_smoothed_because_a_mean_of_a_headcount_is_not_a_headcount()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .At(30.Seconds()).Kills("Nightblade")
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        Then.TheLineIsDrawnAt("standing", 31.Seconds(), "2 up");
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
