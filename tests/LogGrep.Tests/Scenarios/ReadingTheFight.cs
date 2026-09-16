using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>What the app works out about a fight: what each player did, and what it cost them.</summary>
public sealed class ReadingTheFight : Scenario
{
    private const string Boss = "The Soulcoiler";

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue));

    [Fact]
    public void Damage_over_the_length_of_the_fight_is_the_players_dps()
    {
        var log = ARaid().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("1:40")
            .At("0:30").Deals("Nightblade", to: Boss, amount: 1_000_000)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Boss, 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerDpsIs("10K");
    }

    [Fact]
    public void Overhealing_does_not_count_towards_hps()
    {
        var log = ARaid().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("1:40")
            .At("0:30").Heals("Sunwell", target: "Rockjaw", amount: 2_000_000, overheal: 1_500_000)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Boss, 1);

        When.ILookAtPlayer("Sunwell");

        Then.PlayerHpsIs("5K");
    }

    [Fact]
    public void What_a_player_takes_is_measured_per_second_as_well()
    {
        var log = ARaid().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("1:40")
            .At("0:30").BossHits("Rockjaw", amount: 3_000_000)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Boss, 1);

        When.ILookAtPlayer("Rockjaw");

        Then.PlayerDtpsIs("30K");
    }

    [Fact]
    public void A_player_who_lived_through_the_pull_shows_no_death_time()
    {
        Given.IOpenedLog(ARaid().Pull(Boss, Difficulty.Mythic, p => p.Lasting("1:40").Wipe()))
             .And.IOpenedPull(Boss, 1);

        When.ILookAtPlayer("Rockjaw");

        Then.PlayerDeathsRead("-:--");
    }

    [Fact]
    public void A_death_is_timed_from_the_start_of_the_pull()
    {
        var log = ARaid().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("2:00")
            .At("1:23").Kills("Nightblade")
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Boss, 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerDeathsRead("1:23");
    }

    [Fact]
    public void Dying_twice_lists_both_times_in_order()
    {
        var log = ARaid().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("3:00")
            .At("0:45").Kills("Nightblade")
            .At("2:10").Kills("Nightblade")
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Boss, 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerDeathsRead("0:45, 2:10");
    }

    [Fact]
    public void The_death_names_what_had_been_landing_just_before_it()
    {
        var log = ARaid().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("2:00")
            .At("0:55").BossHits("Nightblade", amount: 300_000, with: "Creeping Rot")
            .At("1:00").Kills("Nightblade", with: "Blast Wave", amount: 900_000)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Boss, 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerWasKilledBy("Blast Wave")
            .And.PlayerWasKilledBy("Creeping Rot");
    }

    [Fact]
    public void Damage_from_long_before_the_death_is_not_blamed_for_it()
    {
        var log = ARaid().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("2:00")
            .At("0:30").BossHits("Nightblade", amount: 300_000, with: "Old Poke")
            .At("1:00").Kills("Nightblade", with: "Blast Wave", amount: 900_000)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Boss, 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerWasKilledBy("Blast Wave")
            .And.PlayerWasNotKilledBy("Old Poke");
    }
}
