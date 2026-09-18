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
            .At(50.Seconds()).BossHits("Nightblade", 600_000, with: Ability.BlastWave)
            .At(1.Minutes()).Kills("Nightblade", with: Ability.BlastWave, amount: 400_000)
            .Wipe());

        Given.IOpenedLog(log);

        Then.TheDeathReads("Nightblade", "Blast Wave took 60% in one hit")
            .And.DeathAdvises("Nightblade", "a defensive that was not pressed");
    }

    [Fact]
    public void A_whole_health_bar_in_two_seconds_is_nobodys_reaction_time()
    {
        // Not one hit - several, close enough together that no reaction of any kind fits between
        // them. This is read before the burst rule, because it needs no ceiling to settle.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(58.Seconds()).BossHits("Nightblade", 400_000, with: Ability.CreepingRot)
            .At(59.Seconds()).BossHits("Nightblade", 300_000, with: Ability.CreepingRot)
            .At(1.Minutes()).Kills("Nightblade", with: Ability.CreepingRot, amount: 300_000)
            .Wipe());

        Given.IOpenedLog(log);

        Then.TheDeathReads("Nightblade", "lost 100% in two seconds")
            .And.DeathAdvises("Nightblade", "Nobody reacts to that");
    }

    [Fact]
    public void Damage_past_what_the_healers_have_ever_covered_is_not_theirs_to_answer_for()
    {
        // The ceiling is not a guess about classes: it is the most healing this group has actually
        // landed on one player in five seconds, all evening. Against more than that, the question
        // moves back a step - to why so much reached somebody.
        var log = ARaid()
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(1.Minutes())
                .At(30.Seconds()).BossHits("Rockjaw", 500_000, with: Ability.Cleave)
                .At(31.Seconds()).Heals("Sunwell", target: "Rockjaw", amount: 500_000)
                .Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes())
                .At(50.Seconds()).BossHits("Nightblade", 250_000, with: Ability.CreepingRot)
                .At(52.Seconds()).BossHits("Nightblade", 250_000, with: Ability.CreepingRot)
                .At(54.Seconds()).BossHits("Nightblade", 250_000, with: Ability.CreepingRot)
                .At(56.Seconds()).Kills("Nightblade", with: Ability.CreepingRot, amount: 250_000)
                .Wipe());

        Given.IOpenedLog(log);

        Then.TheDeathReads("Nightblade", "took more than the healers have ever covered")
            .And.DeathEvidenceReads("Nightblade",
                "nobody else died within five seconds, and the attempt ran 2:04 longer; " +
                "166.7K a second incoming against the 100K the healers have landed at their best")
            .And.DeathAdvises("Nightblade", "this is not the healers");
    }

    [Fact]
    public void The_same_damage_against_a_group_that_heals_harder_is_not_past_saving()
    {
        // Identical death, and a night that showed four times the healing. Now the damage was
        // within what they have covered before, so the answer is not "unhealable".
        var log = ARaid()
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(1.Minutes())
                .At(30.Seconds()).BossHits("Rockjaw", 900_000, with: Ability.Cleave)
                .At(31.Seconds()).Heals("Sunwell", target: "Rockjaw", amount: 900_000)
                .At(32.Seconds()).BossHits("Rockjaw", 900_000, with: Ability.Cleave)
                .At(33.Seconds()).Heals("Sunwell", target: "Rockjaw", amount: 900_000)
                .Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes())
                .At(50.Seconds()).BossHits("Nightblade", 250_000, with: Ability.CreepingRot)
                .At(52.Seconds()).BossHits("Nightblade", 250_000, with: Ability.CreepingRot)
                .At(54.Seconds()).BossHits("Nightblade", 250_000, with: Ability.CreepingRot)
                .At(56.Seconds()).Kills("Nightblade", with: Ability.CreepingRot, amount: 250_000)
                .Wipe());

        Given.IOpenedLog(log);

        Then.TheDeathReads("Nightblade", "died while the raid fought on");
    }

    [Fact]
    public void A_long_time_spent_low_reads_as_a_grind()
    {
        // No single big hit - a stream of small ones over half a minute. That is a different
        // conversation, and it is not only the dead player's.
        //
        // The arithmetic, against a million-point pool: the hit at thirty seconds leaves them at
        // eighty-five per cent, which is the last moment they were whole, and everything after it
        // is the event.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(30.Seconds()).BossHits("Nightblade", 150_000, with: Ability.CreepingRot)
            .At(40.Seconds()).BossHits("Nightblade", 150_000, with: Ability.CreepingRot)
            .At(50.Seconds()).BossHits("Nightblade", 150_000, with: Ability.CreepingRot)
            .At(1.Minutes()).Kills("Nightblade", with: Ability.CreepingRot, amount: 100_000)
            .Wipe());

        Given.IOpenedLog(log);

        Then.TheDeathReads("Nightblade", "ground down over 0:30")
            .And.DeathAdvises("Nightblade", "as much a conversation for the healers");
    }

    [Fact]
    public void A_scratch_they_shrugged_off_is_not_where_the_trouble_started()
    {
        // A graze at ten seconds and a real hit at twenty, then the slide. The event began at the
        // real hit: ninety per cent of somebody is still somebody, and calling the graze the start
        // of it would stretch every death back to the first scratch of the fight.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(10.Seconds()).BossHits("Nightblade", 40_000, with: Ability.CreepingRot)
            .At(20.Seconds()).BossHits("Nightblade", 60_000, with: Ability.CreepingRot)
            .At(30.Seconds()).BossHits("Nightblade", 150_000, with: Ability.CreepingRot)
            .At(40.Seconds()).BossHits("Nightblade", 150_000, with: Ability.CreepingRot)
            .At(50.Seconds()).Kills("Nightblade", with: Ability.CreepingRot, amount: 100_000)
            .Wipe());

        Given.IOpenedLog(log);

        Then.TheDeathReads("Nightblade", "ground down over 0:30");
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
