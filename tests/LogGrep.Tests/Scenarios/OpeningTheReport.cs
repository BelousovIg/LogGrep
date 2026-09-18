using LogGrep.Analysis;
using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// Getting from the list of fights to the report, and what the report is about when you arrive.
///
/// One rule decides all of it: **the sample is the encounter entire, and the focus is whatever was
/// clicked**. They are different things, and the difference is not a nicety. Every baseline here is
/// drawn from a run of attempts, so narrowing the sample to the one attempt somebody opened would
/// leave every number on the screen a dash - the most-used gesture in the app leading to its emptiest
/// screen.
/// </summary>
public sealed class OpeningTheReport : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue));

    private static void APull(PullBuilder p) => APullAt(p, Soulcoiler);

    private static void APullAt(PullBuilder p, Boss boss) => p
        .Lasting(2.Minutes())
        .At(0.Seconds()).Deals("Rockjaw", to: boss, amount: 100_000)
        .At(2.Seconds()).BossSwingsAt("Rockjaw", 200_000)
        .At(1.Minutes()).Deals("Nightblade", to: boss, amount: 900_000)
        .Wipe();

    [Fact]
    public void Analysing_a_fight_opens_the_report_on_all_of_its_attempts()
    {
        Given.IOpenedLog(ARaid().Pulls(6, Soulcoiler, Difficulty.Mythic, APull));

        When.IAnalyseTheEncounter(Soulcoiler);

        Then.TheScreenShowing(1)
            .TheReportIsOn("The Soulcoiler")
            .TheReportMeasuresOver("measured over 6 attempts at The Soulcoiler");
    }

    [Fact]
    public void Analysing_one_attempt_narrows_what_is_shown_and_not_what_is_measured()
    {
        Given.IOpenedLog(ARaid().Pulls(6, Soulcoiler, Difficulty.Mythic, APull));

        When.IAnalyseThePull(Soulcoiler, number: 3);

        Then.TheReportIsOn("The Soulcoiler  ›  attempt 3")
            .TheReportMeasuresOver("measured over 6 attempts at The Soulcoiler");
    }

    [Fact]
    public void Analysing_one_person_narrows_it_further_and_still_measures_the_evening()
    {
        Given.IOpenedLog(ARaid().Pulls(6, Soulcoiler, Difficulty.Mythic, APull))
            .IOpenedPull(Soulcoiler, number: 1);

        When.IAnalyseThePlayer(Soulcoiler, number: 1, player: "Nightblade");

        Then.TheReportIsOn("The Soulcoiler  ›  attempt 1  ›  Nightblade")
            .TheReportMeasuresOver("measured over 6 attempts at The Soulcoiler");
    }

    [Fact]
    public void A_number_on_the_report_is_still_the_evenings_number_when_one_attempt_is_open()
    {
        // The point of keeping the sample whole, in one assertion: a ceiling wants five attempts of
        // somebody's own, so opening the third of six must not reduce it to one and hand back a dash.
        Given.IOpenedLog(ARaid().Pulls(6, Soulcoiler, Difficulty.Mythic, APull))
            .IOpenedPull(Soulcoiler, number: 3);

        When.IAnalyseThePull(Soulcoiler, number: 3).ILookAtPlayer("Nightblade");

        Then.PlayerScores(Axis.Output, "100%")
            .PlayerScoreSays(Axis.Output, "your best of 6 attempts");
    }

    [Fact]
    public void Reading_a_log_points_the_report_at_the_fight_the_night_ended_on()
    {
        // An empty report is a dead screen, and the question somebody opens the app with is about
        // what they were just doing. Not every fight at once: a selection is what the baselines are
        // drawn from, and all of them live inside one encounter.
        var log = ARaid()
            .Pulls(3, Boss.EntombedSentinels, Difficulty.Mythic, p => APullAt(p, Boss.EntombedSentinels))
            .Pulls(4, Soulcoiler, Difficulty.Mythic, APull);

        Given.IOpenedLog(log);

        Then.TheReportIsOn("The Soulcoiler")
            .TheReportMeasuresOver("measured over 4 attempts at The Soulcoiler");
    }

    [Fact]
    public void A_report_somebody_pointed_somewhere_is_not_moved_by_the_next_reading()
    {
        var log = ARaid()
            .Pulls(3, Boss.EntombedSentinels, Difficulty.Mythic, p => APullAt(p, Boss.EntombedSentinels))
            .Pulls(4, Soulcoiler, Difficulty.Mythic, APull);

        Given.IOpenedLog(log);

        // Pointed at the earlier fight, then another log arrives and everything is read again.
        When.IAnalyseTheEncounter(Boss.EntombedSentinels)
            .IOpenAnotherLog(ARaid().Called("later.txt").On(CombatLogBuilder.Evening(2026, 9, 16))
                .Pulls(2, Soulcoiler, Difficulty.Mythic, APull));

        Then.TheReportIsOn("Entombed Sentinels");
    }
}
