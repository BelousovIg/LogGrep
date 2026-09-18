using LogGrep.Models;
using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// Getting back out of somewhere.
///
/// The report is something you drill into - a fight, then an attempt, then a person - and every
/// step in had no step out. The trail across the top was already printing the way back; it simply
/// was not doing anything when anybody clicked it.
///
/// Beside it, a back and forward over everything the window does rather than over the report alone.
/// Somebody who narrowed the sample to the wipes and wants it back is doing the same thing as
/// somebody who opened an attempt and wants out of it, and a trail that handles one but not the
/// other is one people stop trusting.
/// </summary>
public sealed class GettingAround : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue));

    private static void APull(PullBuilder p) => p
        .Lasting(2.Minutes())
        .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
        .At(2.Seconds()).BossSwingsAt("Rockjaw", 200_000)
        .At(1.Minutes()).Deals("Nightblade", to: Soulcoiler, amount: 900_000)
        .Wipe();

    [Fact]
    public void The_trail_names_every_step_that_was_taken_in()
    {
        Given.IOpenedLog(ARaid().Pulls(4, Soulcoiler, Difficulty.Mythic, APull));

        When.IAnalyseThePull(Soulcoiler, number: 2)
            .IAnalyseThePlayer(Soulcoiler, number: 2, player: "Nightblade");

        Then.TheTrailReads("The Soulcoiler", "attempt 2", "Nightblade");
    }

    [Fact]
    public void Letting_go_of_the_attempt_keeps_the_person()
    {
        // The report narrows in two directions at once - which part of the night, and who it is
        // about - and each has to be let go of on its own. Somebody reading one player in one pull
        // wants that player over the whole night without going back to the top and starting again.
        Given.IOpenedLog(ARaid().Pulls(4, Soulcoiler, Difficulty.Mythic, APull))
            .IOpenedPull(Soulcoiler, number: 2);

        When.IAnalyseThePlayer(Soulcoiler, number: 2, player: "Nightblade")
            .IClimbTheTrailTo(depth: 1);

        Then.TheTrailReads("The Soulcoiler", "Nightblade");
    }

    [Fact]
    public void Letting_go_of_the_person_keeps_the_attempt()
    {
        Given.IOpenedLog(ARaid().Pulls(4, Soulcoiler, Difficulty.Mythic, APull))
            .IOpenedPull(Soulcoiler, number: 2);

        When.IAnalyseThePlayer(Soulcoiler, number: 2, player: "Nightblade")
            .IClimbTheTrailTo(depth: 2);

        Then.TheTrailReads("The Soulcoiler", "attempt 2");
    }

    [Fact]
    public void The_encounter_at_the_head_of_the_trail_is_the_way_home()
    {
        Given.IOpenedLog(ARaid().Pulls(4, Soulcoiler, Difficulty.Mythic, APull))
            .IOpenedPull(Soulcoiler, number: 2);

        When.IAnalyseThePlayer(Soulcoiler, number: 2, player: "Nightblade")
            .IClimbTheTrailTo(depth: 0);

        Then.TheTrailReads("The Soulcoiler");
    }

    [Fact]
    public void A_name_points_the_report_at_that_person_and_leaves_the_range()
    {
        // Over a whole encounter it gives that person over the encounter; inside one attempt it
        // gives them in that attempt. One gesture, one rule, two destinations.
        Given.IOpenedLog(ARaid().Pulls(4, Soulcoiler, Difficulty.Mythic, APull));

        When.IAnalyseTheEncounter(Soulcoiler).IClickTheNameOf("Nightblade");

        Then.TheTrailReads("The Soulcoiler", "Nightblade");

        When.IClimbTheTrailTo(depth: 0)
            .IAnalyseThePull(Soulcoiler, number: 3)
            .IClickTheNameOf("Nightblade");

        Then.TheTrailReads("The Soulcoiler", "attempt 3", "Nightblade");
    }

    [Fact]
    public void Back_returns_to_where_the_window_was()
    {
        Given.IOpenedLog(ARaid().Pulls(4, Soulcoiler, Difficulty.Mythic, APull));

        When.IAnalyseTheEncounter(Soulcoiler).IAnalyseThePull(Soulcoiler, number: 3);

        Then.TheTrailReads("The Soulcoiler", "attempt 3")
            .TheTrailCanGoBack(true);

        When.IGoBack();

        Then.TheTrailReads("The Soulcoiler")
            .TheTrailCanGoForward(true);
    }

    [Fact]
    public void Forward_goes_back_to_where_back_came_from()
    {
        Given.IOpenedLog(ARaid().Pulls(4, Soulcoiler, Difficulty.Mythic, APull));

        When.IAnalyseTheEncounter(Soulcoiler)
            .IAnalyseThePull(Soulcoiler, number: 3)
            .IGoBack()
            .IGoForward();

        Then.TheTrailReads("The Soulcoiler", "attempt 3")
            .TheTrailCanGoForward(false);
    }

    [Fact]
    public void Narrowing_the_sample_is_a_step_the_trail_remembers_too()
    {
        // Somebody who ticked "wipes only" and wants it back is doing the same thing as somebody
        // who opened an attempt and wants out of it.
        Given.IOpenedLog(ARaid().Pulls(4, Soulcoiler, Difficulty.Mythic, APull));

        When.IAnalyseTheEncounter(Soulcoiler).INarrowTo(Role.Tank);

        Then.TheReportSaysWhoItIsAbout("3 of ours, tank only");

        When.IGoBack();

        Then.TheReportSaysWhoItIsAbout("3 of ours");
    }
}
