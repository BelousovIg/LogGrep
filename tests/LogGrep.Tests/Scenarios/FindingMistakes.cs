using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// Whether the app can tell who took a mechanic that was not theirs. Nothing here tells it what
/// any spell does - it has to work that out from how the fight went, which is the whole point.
/// </summary>
public sealed class FindingMistakes : Scenario
{
    private const string Boss = "The Soulcoiler";
    private const string TankMechanic = "Possession Barrage";

    /// <summary>Two tanks, one healer, seven damage - so tanks are a fifth of the group.</summary>
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
    public void A_mechanic_the_tanks_always_take_is_nobodys_mistake()
    {
        var log = ARaid().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("3:00")
            .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 5)
            .At("1:20").BossDebuffs("Grimhide", with: TankMechanic, times: 4)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedFindings();

        Then.NothingWasFound();
    }

    [Fact]
    public void A_healer_who_took_the_tank_mechanic_is_reported()
    {
        var log = ARaid().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("3:00")
            .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 8)
            .At("1:30").BossDebuffs("Sunwell", with: TankMechanic)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedFindings();

        Then.MechanicBelongsTo(TankMechanic, "tank")
            .And.TookMechanicOutOfTurn(TankMechanic, "Sunwell");
    }

    [Fact]
    public void The_rule_says_what_it_was_drawn_from()
    {
        var log = ARaid().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("3:00")
            .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 8)
            .At("1:30").BossDebuffs("Sunwell", with: TankMechanic)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedFindings();

        Then.MechanicEvidenceReads(TankMechanic, "8 of 9 hit a tank, over 1 attempt");
    }

    [Fact]
    public void A_debuff_only_the_damage_take_is_not_called_their_mechanic()
    {
        // Seven of the ten are damage, so them taking a thing proves nothing on its own. Without
        // that test this would read as a damage mechanic and every other role would be a mistake.
        var log = ARaid().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("3:00")
            .At("0:20").BossDebuffs("Nightblade", with: "Creeping Rot", times: 4)
            .At("1:00").BossDebuffs("Emberwild", with: "Creeping Rot", times: 3)
            .At("2:00").BossDebuffs("Moonfire", with: "Creeping Rot", times: 3)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedFindings();

        Then.MechanicWasNotFlagged("Creeping Rot")
            .And.NothingWasFound();
    }

    [Fact]
    public void A_handful_of_applications_is_not_a_pattern()
    {
        var log = ARaid().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("3:00")
            .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 4)
            .At("1:30").BossDebuffs("Sunwell", with: TankMechanic)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedFindings();

        Then.NothingWasFound();
    }

    [Fact]
    public void A_friendly_buff_is_never_read_as_a_mechanic()
    {
        var log = ARaid().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("3:00")
            .At("0:20").Buffs("Sunwell", target: "Rockjaw", with: "Power Word: Fortitude", times: 12)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedFindings();

        Then.NothingWasFound();
    }

    [Fact]
    public void A_finding_points_at_the_attempt_and_the_moment()
    {
        var log = ARaid()
            .Pull(Boss, Difficulty.Mythic, p => p
                .Lasting("3:00")
                .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 8)
                .Wipe())
            .Pull(Boss, Difficulty.Mythic, p => p
                .Lasting("3:00")
                .At("0:20").BossDebuffs("Grimhide", with: TankMechanic, times: 4)
                .At("1:30").BossDebuffs("Sunwell", with: TankMechanic)
                .Kill());

        Given.IOpenedLog(log).And.IOpenedFindings();

        Then.FindingPointsAt(TankMechanic, "Sunwell", pull: "pull 2", at: "1:30");
    }

    [Fact]
    public void The_heaviest_rule_comes_first_and_an_ignored_one_goes_last()
    {
        Given.IOpenedLog(TwoMechanics()).And.IOpenedFindings();

        Then.RulesAreOrdered(TankMechanic, "Hollowing Strikes");

        When.IIgnoreRule(TankMechanic);

        Then.RulesAreOrdered("Hollowing Strikes", TankMechanic)
            .And.RuleIsIgnored(TankMechanic, true);
    }

    [Fact]
    public void The_summary_counts_the_mistakes_and_the_mechanics()
    {
        Given.IOpenedLog(TwoMechanics()).And.IOpenedFindings();

        Then.FindingsRead("across 2 mechanics");
    }

    /// <summary>One mechanic two people took wrongly, another only one did.</summary>
    private static CombatLogBuilder TwoMechanics() => ARaid().Pull(Boss, Difficulty.Mythic, p => p
        .Lasting("5:00")
        .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 12)
        .At("1:30").BossDebuffs("Sunwell", with: TankMechanic)
        .At("2:00").BossDebuffs("Nightblade", with: TankMechanic)
        .At("2:30").BossDebuffs("Grimhide", with: "Hollowing Strikes", times: 8)
        .At("4:00").BossDebuffs("Emberwild", with: "Hollowing Strikes")
        .Wipe());
}
