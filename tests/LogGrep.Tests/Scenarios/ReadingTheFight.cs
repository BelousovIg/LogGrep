using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>What the app works out about a fight: what each player did, and what it cost them.</summary>
public sealed class ReadingTheFight : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue));

    [Fact]
    public void Damage_over_the_length_of_the_fight_is_the_players_dps()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(1.Minutes(40))
            .At(30.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 1_000_000)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Soulcoiler, 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerDpsIs("10K");
    }

    [Fact]
    public void Overhealing_does_not_count_towards_hps()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(1.Minutes(40))
            .At(30.Seconds()).Heals("Sunwell", target: "Rockjaw", amount: 2_000_000, overheal: 1_500_000)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Soulcoiler, 1);

        When.ILookAtPlayer("Sunwell");

        Then.PlayerHpsIs("5K");
    }

    [Fact]
    public void What_a_player_takes_is_measured_per_second_as_well()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(1.Minutes(40))
            .At(30.Seconds()).BossHits("Rockjaw", amount: 3_000_000)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Soulcoiler, 1);

        When.ILookAtPlayer("Rockjaw");

        Then.PlayerDtpsIs("30K");
    }

    [Fact]
    public void A_player_who_lived_through_the_pull_shows_no_death_time()
    {
        Given.IOpenedLog(ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(1.Minutes(40)).Wipe()))
             .And.IOpenedPull(Soulcoiler, 1);

        When.ILookAtPlayer("Rockjaw");

        Then.PlayerDidNotDie();
    }

    [Fact]
    public void A_death_is_timed_from_the_start_of_the_pull()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(1.Minutes(23)).Kills("Nightblade")
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Soulcoiler, 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerDiedAt(1.Minutes(23));
    }

    [Fact]
    public void Dying_twice_lists_both_times_in_order()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(45.Seconds()).Kills("Nightblade")
            .At(2.Minutes(10)).Kills("Nightblade")
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Soulcoiler, 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerDiedAt(45.Seconds(), 2.Minutes(10));
    }

    [Fact]
    public void The_death_names_what_had_been_landing_just_before_it()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(55.Seconds()).BossHits("Nightblade", amount: 300_000, with: Ability.CreepingRot)
            .At(1.Minutes()).Kills("Nightblade", with: Ability.BlastWave, amount: 900_000)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Soulcoiler, 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerWasKilledBy(Ability.BlastWave)
            .And.PlayerWasKilledBy(Ability.CreepingRot);
    }

    [Fact]
    public void Damage_from_long_before_the_death_is_not_blamed_for_it()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(30.Seconds()).BossHits("Nightblade", amount: 300_000, with: Ability.OldPoke)
            .At(1.Minutes()).Kills("Nightblade", with: Ability.BlastWave, amount: 900_000)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Soulcoiler, 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerWasKilledBy(Ability.BlastWave)
            .And.PlayerWasNotKilledBy(Ability.OldPoke);
    }
}
