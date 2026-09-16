using LogGrep.Models;
using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// Whether the app can tell damage the group gets out of from damage the fight simply deals. The
/// same argument as the mechanics rule, pointed at a damage event instead of a debuff: a spell that
/// lands on one person out of ten, attempt after attempt, is one the other nine are avoiding, and
/// nothing anywhere in the app had to be told which spells those are.
/// </summary>
public sealed class FindingAvoidableDamage : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;
    private const Ability Puddle = Ability.CreepingRot;

    /// <summary>Attempts it takes before the app is willing to call anything a rule.</summary>
    private const int Habit = 12;

    /// <summary>Two tanks, one healer, seven damage.</summary>
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

    /// <summary>
    /// A run of attempts where one person stood in it each time - a different one each attempt, so
    /// the spell belongs to nobody's role and there is nothing but position to explain it.
    /// </summary>
    private static CombatLogBuilder SomebodyStoodInIt(int times = Habit, long amount = 400_000)
    {
        string[] unlucky = { "Rockjaw", "Sunwell", "Nightblade", "Grimhide", "Emberwild", "Moonfire" };
        var log = ARaid();

        for (int i = 0; i < times; i++)
        {
            string victim = unlucky[i % unlucky.Length];
            log.Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes())
                .At(30.Seconds()).BossHits(victim, amount, with: Puddle)
                .Wipe());
        }

        return log;
    }

    [Fact]
    public void Damage_one_person_in_ten_takes_is_called_avoidable()
    {
        Given.IOpenedLog(SomebodyStoodInIt());

        Then.DamageWasAvoidable(Puddle)
            .And.MechanicEvidenceReads(Puddle, "1 of 10 took it on a typical attempt, over 12 attempts")
            .And.FindingAdvises(Puddle, "Where you were standing is the whole of the fix");
    }

    [Fact]
    public void A_handful_of_attempts_is_not_enough_to_call_it_avoidable()
    {
        // Nine attempts: enough landings to average over, one attempt short of the floor. What
        // stops this is the number of attempts and nothing else.
        Given.IOpenedLog(SomebodyStoodInIt(times: 9));

        Then.NothingWasFound();
    }

    [Fact]
    public void Damage_the_whole_group_takes_is_nobodys_mistake()
    {
        // A raid-wide hit is the fight happening, not anybody standing wrong. Without this the
        // app would hand every player a finding on every attempt and mean nothing by any of them.
        var log = ARaid().Pulls(Habit, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(30.Seconds()).BossHits("Rockjaw", 200_000, with: Puddle)
            .At(30.Seconds()).BossHits("Grimhide", 200_000, with: Puddle)
            .At(30.Seconds()).BossHits("Sunwell", 200_000, with: Puddle)
            .At(30.Seconds()).BossHits("Nightblade", 200_000, with: Puddle)
            .At(30.Seconds()).BossHits("Emberwild", 200_000, with: Puddle)
            .At(30.Seconds()).BossHits("Moonfire", 200_000, with: Puddle)
            .At(30.Seconds()).BossHits("Frostbite", 200_000, with: Puddle)
            .At(30.Seconds()).BossHits("Stormfist", 200_000, with: Puddle)
            .At(30.Seconds()).BossHits("Earthen", 200_000, with: Puddle)
            .At(30.Seconds()).BossHits("Bladewind", 200_000, with: Puddle)
            .Wipe());

        Given.IOpenedLog(log);

        Then.NothingWasFound();
    }

    [Fact]
    public void Damage_only_the_tanks_take_is_not_avoidable_damage()
    {
        // Two tanks out of ten is a fifth of the group, which looks exactly like something the
        // other eight are getting out of - and it is a tank doing their job. Telling a tank off
        // for tanking is the failure this test exists to prevent.
        var log = ARaid().Pulls(Habit, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(30.Seconds()).BossHits("Rockjaw", 900_000, with: Puddle)
            .At(1.Minutes()).BossHits("Grimhide", 900_000, with: Puddle)
            .Wipe());

        Given.IOpenedLog(log);

        Then.NothingWasFound();
    }

    [Fact]
    public void A_swing_with_no_spell_behind_it_is_never_called_avoidable()
    {
        // Melee has no spell id and no way to stand somewhere else, so it never reaches this rule.
        var log = ARaid().Pulls(Habit, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(30.Seconds()).BossSwingsAt("Nightblade", 500_000)
            .Wipe());

        Given.IOpenedLog(log);

        Then.NothingWasFound();
    }

    [Fact]
    public void Standing_in_it_and_dying_to_it_costs_a_death()
    {
        string[] unlucky = { "Rockjaw", "Sunwell", "Nightblade", "Grimhide", "Emberwild", "Moonfire" };
        var log = ARaid();

        for (int i = 0; i < Habit; i++)
        {
            string victim = unlucky[i % unlucky.Length];
            log.Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes())
                .At(30.Seconds()).BossHits(victim, 400_000, with: Puddle)
                .Wipe());
        }

        // And one more, where the person who stood in it did not walk away.
        log.Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(30.Seconds()).BossHits("Earthen", 400_000, with: Puddle)
            .At(35.Seconds()).Kills("Earthen", with: Puddle)
            .Wipe());

        Given.IOpenedLog(log);

        Then.FindingsAreOrderedByCost()
            .And.MistakeKilled(Puddle, "Earthen", at: 35.Seconds())
            .And.MistakeCost(Puddle, "Nightblade", "400K taken");
    }
}
