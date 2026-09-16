using LogGrep.Models;
using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>What the window puts in front of somebody who just opened a log.</summary>
public sealed class ShowingTheLog : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue),
        Damage("Emberwild", Spec.ArcaneMage));

    [Fact]
    public void Every_boss_in_the_log_gets_a_row()
    {
        var log = ARaid()
            .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(1.Minutes(40)).Wipe())
            .Pull(Boss.EntombedSentinels, Difficulty.Heroic, p => p.Lasting(2.Minutes()).Kill());

        Given.IOpenedLog(log);

        Then.EncountersAreListed(Soulcoiler, Boss.EntombedSentinels);
    }

    [Fact]
    public void Attempts_at_the_same_boss_gather_under_one_row()
    {
        var log = ARaid()
            .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(1.Minutes(40)).Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(2.Minutes(30)).Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(3.Minutes()).Kill());

        Given.IOpenedLog(log);
        When.ILookAtEncounter(Soulcoiler);

        Then.EncountersAreListed(Soulcoiler)
            .And.EncounterHasPulls(3)
            .And.EncounterWasKilled(true)
            .And.EncounterShowsDifficulty("Mythic");
    }

    [Fact]
    public void An_encounter_starts_closed_and_the_toggle_opens_it()
    {
        Given.IOpenedLog(ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(1.Minutes(40)).Wipe()));

        When.ILookAtEncounter(Soulcoiler);
        Then.EncounterIsOpened(false);

        When.IToggleEncounter(Soulcoiler);
        Then.EncounterIsOpened(true);

        When.IToggleEncounter(Soulcoiler);
        Then.EncounterIsOpened(false);
    }

    [Fact]
    public void A_single_dungeon_pull_has_nothing_to_open()
    {
        Given.IOpenedLog(ARaid().Pull(
            Boss.ForgottenDepths, Difficulty.HeroicDungeon, p => p.Lasting(1.Minutes()).Kill()));

        When.ILookAtEncounter(Boss.ForgottenDepths);

        Then.EncounterCanBeOpened(false);
    }

    [Fact]
    public void A_pull_opens_into_the_group_that_fought_it()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(1.Minutes(40))
            .At(20.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 500_000)
            .Wipe());

        Given.IOpenedLog(log).And.IExpandedEncounter(Soulcoiler);

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
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(1.Minutes(40))
            .At(20.Seconds()).Deals("Sunwell", to: Soulcoiler, amount: 1)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Soulcoiler, 1);

        When.ILookAtPlayer("Sunwell");

        Then.PlayerIsShownAs("Priest", "Holy");
    }

    [Fact]
    public void A_tank_and_a_healer_are_marked_and_the_damage_are_not()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(1.Minutes(40)).Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Soulcoiler, 1);

        When.ILookAtPlayer("Rockjaw");
        Then.PlayerRoleMarkIs(Role.Tank);

        When.ILookAtPlayer("Sunwell");
        Then.PlayerRoleMarkIs(Role.Healer);

        When.ILookAtPlayer("Nightblade");
        Then.PlayerRoleMarkIs(Role.Damage);
    }

    [Fact]
    public void The_group_is_listed_tanks_first_then_healers_then_the_rest()
    {
        var log = new CombatLogBuilder()
            .Raid(
                Tank("Rockjaw", Spec.ProtectionWarrior),
                Tank("Grimhide", Spec.VengeanceDemonHunter),
                Healer("Sunwell", Spec.HolyPriest),
                Healer("Lightwell", Spec.RestorationShaman),
                Damage("Nightblade", Spec.AssassinationRogue),
                Damage("Emberwild", Spec.ArcaneMage))
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(1.Minutes(40))
                // Tanks rank by damage, so the weaker one comes second despite out-healing nobody.
                .At(10.Seconds()).Deals("Grimhide", to: Soulcoiler, amount: 3_000_000)
                .At(11.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 2_000_000)
                // Healers rank by healing, and their damage is beside the point.
                .At(20.Seconds()).Heals("Lightwell", target: "Rockjaw", amount: 2_000_000)
                .At(21.Seconds()).Heals("Sunwell", target: "Rockjaw", amount: 1_000_000)
                .At(22.Seconds()).Deals("Sunwell", to: Soulcoiler, amount: 9_000_000)
                .At(30.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 4_000_000)
                .At(31.Seconds()).Deals("Emberwild", to: Soulcoiler, amount: 1_000_000)
                .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Soulcoiler, 1);

        Then.PlayersAreOrdered("Grimhide", "Rockjaw", "Lightwell", "Sunwell", "Nightblade", "Emberwild");
    }

    [Fact]
    public void Players_sort_by_the_column_that_was_clicked()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(1.Minutes(40))
            .At(10.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 3_000_000)
            .At(20.Seconds()).Deals("Emberwild", to: Soulcoiler, amount: 2_000_000)
            .At(30.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 1_000_000)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Soulcoiler, 1);

        // Tanks lead, then healers, then the rest by damage.
        Then.PlayersAreOrdered("Rockjaw", "Sunwell", "Nightblade", "Emberwild");

        When.ISortPlayersBy(PlayerColumn.Name);
        Then.PlayersAreOrdered("Emberwild", "Nightblade", "Rockjaw", "Sunwell");

        When.ISortPlayersBy(PlayerColumn.Dps);
        Then.PlayersAreOrdered("Nightblade", "Emberwild", "Rockjaw", "Sunwell");
    }
}
