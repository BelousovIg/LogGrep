using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// Whether a death was the player's or the raid's, and if it was theirs, what shape it had.
///
/// The first question is not what killed somebody, it is whether it was their death at all. A wipe
/// is one event, not twenty mistakes - and a tool that hands out a finding per corpse gets closed
/// and not reopened, so the doubt goes towards saying nothing.
/// </summary>
public sealed class ExplainingADeath : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Tank("Grimhide", Spec.VengeanceDemonHunter),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue),
        Damage("Emberwild", Spec.ArcaneMage),
        Damage("Moonfire", Spec.BalanceDruid),
        Damage("Frostbite", Spec.FrostDeathKnight),
        Damage("Stormfist", Spec.WindwalkerMonk),
        Damage("Earthen", Spec.ElementalShaman),
        Damage("Bladewind", Spec.HavocDemonHunter));

    [Fact]
    public void A_wipe_is_one_event_and_not_a_finding_per_corpse()
    {
        // The damage check was missed and the raid fell together. Nobody made a personal mistake
        // here, and ten findings for one wipe is the failure this whole rule exists to prevent.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(1.Minutes(55)).Kills("Rockjaw")
            .At(1.Minutes(56)).Kills("Grimhide")
            .At(1.Minutes(56)).Kills("Sunwell")
            .At(1.Minutes(57)).Kills("Nightblade")
            .At(1.Minutes(57)).Kills("Emberwild")
            .At(1.Minutes(58)).Kills("Moonfire")
            .At(1.Minutes(58)).Kills("Frostbite")
            .At(1.Minutes(59)).Kills("Stormfist")
            .Wipe());

        Given.IOpenedLog(log);

        Then.NothingWasFound();
    }

    [Fact]
    public void A_group_falling_together_is_one_event_even_when_the_fight_goes_on()
    {
        // Three of ten inside five seconds, and the attempt still had two minutes in it. The fight
        // carrying on is not enough on its own - whatever caught three people at once caught them,
        // and pinning it on each of them separately says the same thing three times.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(1.Minutes()).Kills("Nightblade")
            .At(1.Minutes(2)).Kills("Emberwild")
            .At(1.Minutes(4)).Kills("Moonfire")
            .Wipe());

        Given.IOpenedLog(log);

        Then.NothingWasFound();
    }

    [Fact]
    public void The_same_three_deaths_spread_out_are_three_separate_ones()
    {
        // Same people, same attempt, half a minute apart. Nothing caught them together, so each
        // one is their own.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(30.Seconds()).Kills("Nightblade")
            .At(1.Minutes()).Kills("Emberwild")
            .At(1.Minutes(30)).Kills("Moonfire")
            .Wipe());

        Given.IOpenedLog(log);

        Then.ADeathWasReported("Nightblade", at: 30.Seconds())
            .And.ADeathWasReported("Emberwild", at: 1.Minutes())
            .And.ADeathWasReported("Moonfire", at: 1.Minutes(30));
    }

    [Fact]
    public void A_death_the_raid_fought_on_past_belongs_to_the_player()
    {
        // One person dies a third of the way in and the fight runs another two minutes. Whatever
        // happened, it was not the attempt ending.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(1.Minutes()).Kills("Nightblade")
            .Wipe());

        Given.IOpenedLog(log);

        Then.ADeathWasReported("Nightblade", at: 1.Minutes())
            .And.DeathEvidenceReads("Nightblade", "nobody else died within five seconds, and the attempt ran 2:00 longer");
    }

    [Fact]
    public void A_death_at_the_very_end_of_an_attempt_is_the_attempt_ending()
    {
        // Alone, but ten seconds from the end. Whoever falls first in a collapse did not make a
        // mistake the others avoided; they were simply first.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(1.Minutes(50)).Kills("Nightblade")
            .Wipe());

        Given.IOpenedLog(log);

        Then.NothingWasFound();
    }

    [Fact]
    public void One_hit_taking_half_of_somebody_reads_as_a_burst()
    {
        // The health in the log makes this a fact rather than an estimate: the pool is a million,
        // the hit was six hundred thousand.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(58.Seconds()).BossHits("Nightblade", 600_000, with: Ability.BlastWave)
            .At(1.Minutes()).Kills("Nightblade", with: Ability.BlastWave, amount: 400_000)
            .Wipe());

        Given.IOpenedLog(log);

        Then.TheDeathReads("Nightblade", "Blast Wave took 60% in one hit")
            .And.DeathAdvises("Nightblade", "a defensive that was not pressed");
    }

    [Fact]
    public void A_long_time_spent_low_reads_as_a_grind()
    {
        // No single big hit - a stream of small ones over half a minute. That is a different
        // conversation, and it is not only the dead player's.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(30.Seconds()).BossHits("Nightblade", 100_000, with: Ability.CreepingRot)
            .At(40.Seconds()).BossHits("Nightblade", 100_000, with: Ability.CreepingRot)
            .At(50.Seconds()).BossHits("Nightblade", 100_000, with: Ability.CreepingRot)
            .At(1.Minutes()).Kills("Nightblade", with: Ability.CreepingRot, amount: 100_000)
            .Wipe());

        Given.IOpenedLog(log);

        Then.TheDeathReads("Nightblade", "ground down over 0:30")
            .And.DeathAdvises("Nightblade", "as much a conversation for the healers");
    }

    [Fact]
    public void Healing_that_put_somebody_back_up_ends_the_grind_there()
    {
        // Topped up halfway through, so the event is what happened after that - not the whole
        // stretch since the first scratch.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(20.Seconds()).BossHits("Nightblade", 400_000, with: Ability.CreepingRot)
            .At(30.Seconds()).Heals("Sunwell", target: "Nightblade", amount: 400_000)
            .At(55.Seconds()).BossHits("Nightblade", 300_000, with: Ability.CreepingRot)
            .At(1.Minutes()).Kills("Nightblade", with: Ability.CreepingRot, amount: 700_000)
            .Wipe());

        Given.IOpenedLog(log);

        // The grind is measured from the moment they were last whole, which healing reset.
        Then.TheDeathReads("Nightblade", "Creeping Rot took 70% in one hit");
    }
}
