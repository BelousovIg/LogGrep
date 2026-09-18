using LogGrep.Models;
using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// The report as a grid: rows are who, columns are when.
///
/// One control, and the views of the report are states of it rather than separate screens. What
/// changes between them is how many rows there are and what a column stands for, so somebody learns
/// to read it once.
///
/// The thing that has to be right from the first day is the empty cell. Somebody who was not in an
/// attempt and somebody who was and had a clean one are two completely different answers, and drawn
/// the same way the person who missed half the evening reads as the best player in the raid.
/// </summary>
public sealed class ReadingTheGrid : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue));

    private static void APull(PullBuilder p) => Body(p).Wipe();

    private static PullBuilder Body(PullBuilder p) => p
        .Lasting(2.Minutes())
        .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
        .At(2.Seconds()).BossSwingsAt("Rockjaw", 200_000)
        .At(1.Minutes()).Deals("Nightblade", to: Soulcoiler, amount: 900_000);

    [Fact]
    public void Every_attempt_of_the_selection_is_a_column()
    {
        Given.IOpenedLog(ARaid().Pulls(5, Soulcoiler, Difficulty.Mythic, APull));

        When.IAnalyseTheEncounter(Soulcoiler);

        Then.TheGridHasColumns(5)
            .TheGridLists("Nightblade", "Rockjaw", "Sunwell");
    }

    [Fact]
    public void A_row_carries_the_same_columns_an_attempt_does()
    {
        // Somebody reading down a night and somebody reading across one attempt are the same person,
        // and should not have to learn two tables.
        Given.IOpenedLog(ARaid().Pulls(2, Soulcoiler, Difficulty.Mythic, APull));

        When.IAnalyseTheEncounter(Soulcoiler);

        // 900K dealt once in each of two two-minute attempts: 1.8M over 240 seconds.
        Then.TheGridRowReads("Nightblade", "Rogue", "Assassination", dps: "7.5K", hps: "—", dtps: "—")
            .TheGridRowRoleMarkIs("Nightblade", Role.Damage)
            .TheGridRowRoleMarkIs("Rockjaw", Role.Tank)
            .TheGridRowRoleMarkIs("Sunwell", Role.Healer);
    }

    [Fact]
    public void Somebody_who_missed_an_attempt_is_not_shown_as_having_played_it_cleanly()
    {
        // Two attempts with the rogue, then one without them. A blank and a dot are different
        // answers, and only one of them is true about somebody who was not in the room.
        var log = ARaid().Pulls(2, Soulcoiler, Difficulty.Mythic, APull);

        log.Raid(
                Tank("Rockjaw", Spec.ProtectionWarrior),
                Healer("Sunwell", Spec.HolyPriest))
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(2.Minutes())
                .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
                .At(2.Seconds()).BossSwingsAt("Rockjaw", 200_000)
                .Wipe());

        Given.IOpenedLog(log);

        When.IAnalyseTheEncounter(Soulcoiler);

        Then.TheGridSaysTheyWereThere("Nightblade", attempt: 1, expected: true)
            .TheGridSaysTheyWereThere("Nightblade", attempt: 3, expected: false)
            .TheGridCellReads("Nightblade", attempt: 3, expected: "·");
    }

    [Fact]
    public void An_attempt_that_cost_somebody_nothing_is_blank_rather_than_dotted()
    {
        Given.IOpenedLog(ARaid().Pulls(3, Soulcoiler, Difficulty.Mythic, APull));

        When.IAnalyseTheEncounter(Soulcoiler);

        Then.TheGridSaysTheyWereThere("Nightblade", attempt: 1, expected: true)
            .TheGridCellReads("Nightblade", attempt: 1, expected: string.Empty);
    }

    [Fact]
    public void A_death_shows_up_as_what_it_cost_them()
    {
        // One health pool, which is what a death is worth in the only unit that compares a rogue
        // with a tank.
        var log = ARaid()
            .Pulls(2, Soulcoiler, Difficulty.Mythic, APull)
            .Pull(Soulcoiler, Difficulty.Mythic, p => Body(p)
                .At(1.Minutes(30)).Kills("Nightblade")
                .Wipe());

        Given.IOpenedLog(log);

        When.IAnalyseTheEncounter(Soulcoiler);

        Then.TheGridCellReads("Nightblade", attempt: 3, expected: "1");
    }

    [Fact]
    public void Narrowing_to_the_tanks_leaves_the_others_out_of_the_grid()
    {
        Given.IOpenedLog(ARaid().Pulls(3, Soulcoiler, Difficulty.Mythic, APull));

        When.IAnalyseTheEncounter(Soulcoiler).INarrowTo(Role.Tank);

        Then.TheGridLists("Rockjaw");
    }

    [Fact]
    public void Setting_somebody_aside_takes_their_row_away()
    {
        Given.IOpenedLog(ARaid().Pulls(3, Soulcoiler, Difficulty.Mythic, APull));

        When.IUnmarkAsOurs("Sunwell").IAnalyseTheEncounter(Soulcoiler);

        Then.TheGridLists("Nightblade", "Rockjaw");
    }
}
