using LogGrep.Analysis;
using LogGrep.Models;
using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using LogGrep.ViewModels;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// Saying which attempts a report is about, and who it is about.
///
/// The selection is not a filter over a finished answer - it is what the answer is computed from.
/// Ten wipes out of thirty-one attempts is a different evening from the thirty-one, and every
/// baseline in the app is drawn from a run of attempts, so narrowing the selection re-runs the
/// rules. The screen has to say which selection it used, because the same person scores differently
/// under two of them and that must never be able to look like a bug.
///
/// How the choice was made matters as much as what it came to. A rule - "every attempt at this
/// boss" - grows when the log does, which is what somebody wants on a raid night. A set picked by
/// hand is theirs, and having it grow behind them would move every number for reasons they did not
/// ask for.
/// </summary>
public sealed class ChoosingWhatToMeasure : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue));

    private static void AWipe(PullBuilder p) => Body(p).Wipe();

    private static void AKill(PullBuilder p) => Body(p).Kill();

    private static PullBuilder Body(PullBuilder p) => p
        .Lasting(2.Minutes())
        .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
        .At(2.Seconds()).BossSwingsAt("Rockjaw", 200_000)
        .At(1.Minutes()).Deals("Nightblade", to: Soulcoiler, amount: 900_000);

    [Fact]
    public void A_fight_is_selected_by_a_rule_and_says_so()
    {
        Given.IOpenedLog(ARaid().Pulls(6, Soulcoiler, Difficulty.Mythic, AWipe));

        When.IAnalyseTheEncounter(Soulcoiler);

        Then.TheReportMeasuresOver("measured over 6 attempts at The Soulcoiler");
    }

    [Fact]
    public void Narrowing_to_the_wipes_is_a_different_evening_and_the_screen_says_which()
    {
        var log = ARaid()
            .Pulls(4, Soulcoiler, Difficulty.Mythic, AWipe)
            .Pulls(2, Soulcoiler, Difficulty.Mythic, AKill);

        Given.IOpenedLog(log);

        When.IAnalyseTheEncounter(Soulcoiler).INarrowTo(Outcome.Wipes);

        Then.TheReportMeasuresOver("measured over 4 attempts at The Soulcoiler, wipes only");
    }

    [Fact]
    public void A_set_picked_by_hand_says_what_it_was_picked_out_of()
    {
        // "Three attempts" and "three of six" answer different questions, and only the second one
        // can be argued with.
        Given.IOpenedLog(ARaid().Pulls(6, Soulcoiler, Difficulty.Mythic, AWipe));

        When.IAnalyseTheChosenAttempts(Soulcoiler, 1, 2, 3);

        Then.TheReportMeasuresOver("measured over 3 of 6 attempts at The Soulcoiler, picked by hand");
    }

    [Fact]
    public void Narrowing_the_selection_re_runs_the_rules_over_what_is_left()
    {
        // Six attempts is enough to know somebody's best; three is not. The score is not filtered
        // afterwards, it is computed from the selection - so the cell goes to a dash and says why.
        Given.IOpenedLog(ARaid().Pulls(6, Soulcoiler, Difficulty.Mythic, AWipe));

        When.IAnalyseTheChosenAttempts(Soulcoiler, 1, 2, 3)
            .ILookAtPullInTheReport(1)
            .ILookAtPlayer("Nightblade");

        Then.PlayerScores(Axis.Output, "—")
            .PlayerScoreSays(Axis.Output, "not enough attempts yet to know your best");
    }

    [Fact]
    public void Asking_for_the_tanks_narrows_the_rows_and_nothing_else()
    {
        Given.IOpenedLog(ARaid().Pulls(6, Soulcoiler, Difficulty.Mythic, AWipe));

        When.IAnalyseThePull(Soulcoiler, number: 1).INarrowTo(Role.Tank);

        Then.TheReportSaysWhoItIsAbout("3 of ours, tank only")
            .TheReportShowsPlayers("Rockjaw");
    }

    [Fact]
    public void Setting_somebody_aside_takes_them_out_of_who_the_report_is_about()
    {
        Given.IOpenedLog(ARaid().Pulls(6, Soulcoiler, Difficulty.Mythic, AWipe));

        When.IUnmarkAsOurs("Nightblade").IAnalyseTheEncounter(Soulcoiler);

        Then.TheReportSaysWhoItIsAbout("2 of ours");
    }
}
