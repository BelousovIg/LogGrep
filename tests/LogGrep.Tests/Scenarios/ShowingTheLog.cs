using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>What the window puts in front of somebody who just opened a log.</summary>
public sealed class ShowingTheLog : Scenario
{
    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue),
        Damage("Emberwild", Spec.ArcaneMage));

    [Fact]
    public void Every_boss_in_the_log_gets_a_row()
    {
        var log = ARaid()
            .Pull("The Soulcoiler", Difficulty.Mythic, p => p.Lasting("1:40").Wipe())
            .Pull("Entombed Sentinels", Difficulty.Heroic, p => p.Lasting("2:00").Kill());

        Given.IOpenedLog(log);

        Then.EncountersAreListed("The Soulcoiler", "Entombed Sentinels");
    }

    [Fact]
    public void Attempts_at_the_same_boss_gather_under_one_row()
    {
        var log = ARaid()
            .Pull("The Soulcoiler", Difficulty.Mythic, p => p.Lasting("1:40").Wipe())
            .Pull("The Soulcoiler", Difficulty.Mythic, p => p.Lasting("2:30").Wipe())
            .Pull("The Soulcoiler", Difficulty.Mythic, p => p.Lasting("3:00").Kill());

        Given.IOpenedLog(log);
        When.ILookAtEncounter("The Soulcoiler");

        Then.EncountersAreListed("The Soulcoiler")
            .And.EncounterHasPulls(3)
            .And.EncounterWasKilled(true)
            .And.EncounterShowsDifficulty("Mythic");
    }

    [Fact]
    public void An_encounter_starts_closed_and_the_toggle_opens_it()
    {
        Given.IOpenedLog(ARaid().Pull("The Soulcoiler", Difficulty.Mythic, p => p.Lasting("1:40").Wipe()));

        When.ILookAtEncounter("The Soulcoiler");
        Then.EncounterIsOpened(false);

        When.IToggleEncounter("The Soulcoiler");
        Then.EncounterIsOpened(true);

        When.IToggleEncounter("The Soulcoiler");
        Then.EncounterIsOpened(false);
    }

    [Fact]
    public void A_single_dungeon_pull_has_nothing_to_open()
    {
        Given.IOpenedLog(ARaid().Pull("Forgotten Depths", Difficulty.HeroicDungeon, p => p.Lasting("1:00").Kill()));

        When.ILookAtEncounter("Forgotten Depths");

        Then.EncounterCanBeOpened(false);
    }

    [Fact]
    public void A_pull_opens_into_the_group_that_fought_it()
    {
        var log = ARaid().Pull("The Soulcoiler", Difficulty.Mythic, p => p
            .Lasting("1:40")
            .At("0:20").Deals("Nightblade", to: "The Soulcoiler", amount: 500_000)
            .Wipe());

        Given.IOpenedLog(log).And.IExpandedEncounter("The Soulcoiler");

        When.ILookAtPull(1);
        Then.PullIsOpened(false);

        When.ITogglePull(1);
        Then.PullIsOpened(true)
            .And.PullShowsPlayers(4)
            .And.PullResultIs("wiped")
            .And.PullLasted("1:40");
    }

    [Fact]
    public void A_player_row_carries_the_class_and_the_specialization()
    {
        var log = ARaid().Pull("The Soulcoiler", Difficulty.Mythic, p => p
            .Lasting("1:40")
            .At("0:20").Deals("Sunwell", to: "The Soulcoiler", amount: 1)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull("The Soulcoiler", 1);

        When.ILookAtPlayer("Sunwell");

        Then.PlayerIsShownAs("Priest", "Holy");
    }


    [Fact]
    public void A_tank_and_a_healer_are_marked_and_the_damage_are_not()
    {
        var log = ARaid().Pull("The Soulcoiler", Difficulty.Mythic, p => p.Lasting("1:40").Wipe());

        Given.IOpenedLog(log).And.IOpenedPull("The Soulcoiler", 1);

        When.ILookAtPlayer("Rockjaw");
        Then.PlayerRoleMarkIs("tank");

        When.ILookAtPlayer("Sunwell");
        Then.PlayerRoleMarkIs("healer");

        When.ILookAtPlayer("Nightblade");
        Then.PlayerRoleMarkIs("none");
    }
    [Fact]
    public void Players_sort_by_the_column_that_was_clicked()
    {
        var log = ARaid().Pull("The Soulcoiler", Difficulty.Mythic, p => p
            .Lasting("1:40")
            .At("0:10").Deals("Nightblade", to: "The Soulcoiler", amount: 3_000_000)
            .At("0:20").Deals("Emberwild", to: "The Soulcoiler", amount: 2_000_000)
            .At("0:30").Deals("Rockjaw", to: "The Soulcoiler", amount: 1_000_000)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull("The Soulcoiler", 1);

        Then.PlayersAreOrdered("Nightblade", "Emberwild", "Rockjaw", "Sunwell");

        When.ISortPlayersBy("Name");
        Then.PlayersAreOrdered("Emberwild", "Nightblade", "Rockjaw", "Sunwell");

        When.ISortPlayersBy("Dps");
        Then.PlayersAreOrdered("Nightblade", "Emberwild", "Rockjaw", "Sunwell");
    }
}
