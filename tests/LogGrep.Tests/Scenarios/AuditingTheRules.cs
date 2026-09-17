using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// The written rules and the log checking each other.
///
/// A derived rule needs ten attempts; a written one works from the first pull and carries the one
/// thing derivation never will - what the mechanic is for. They fail in opposite directions, which
/// is why both are worth having, and why the interesting case is the one where they disagree.
///
/// Nobody else can run this check, for the plain reason that nobody else has a second, independent
/// source of truth to hold the first against.
/// </summary>
public sealed class AuditingTheRules : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;
    private const Ability Barrage = Ability.PossessionBarrage;

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

    /// <summary>A night where the mechanic went to a tank every time, and once to the healer.</summary>
    private static CombatLogBuilder ATankMechanic()
        => ARaid()
            .Pulls(10, Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes())
                .At(20.Seconds()).BossDebuffs("Rockjaw", with: Barrage, times: 2)
                .Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes())
                .At(20.Seconds()).BossDebuffs("Rockjaw", with: Barrage, times: 2)
                .At(1.Minutes(30)).BossDebuffs("Sunwell", with: Barrage)
                .Wipe());

    [Fact]
    public void The_journals_own_sentence_replaces_the_written_one_when_they_agree()
    {
        // The file says tank and the log says tank, so the file's advice is used - which is the
        // point of having it: it knows what the mechanic is for, and no amount of counting does.
        Given.ARulesFileSaying(Barrage, "tank", "Face the boss away from the raid while this is up.")
             .And.IOpenedLog(ATankMechanic());

        Then.MechanicBelongsTo(Barrage, LogGrep.Models.Role.Tank)
            .And.FindingAdvises(Barrage, "Face the boss away from the raid while this is up.");
    }

    [Fact]
    public void A_file_that_disagrees_with_the_log_is_said_out_loud_and_muted()
    {
        // The file calls it a healer mechanic; eleven attempts say it goes to a tank. Reporting
        // against the file would flag every tank in every pull, and only the log would notice.
        Given.ARulesFileSaying(Barrage, "healer", "Stand apart when this lands on you.")
             .And.IOpenedLog(ATankMechanic());

        Then.TheRulesSay("Possession Barrage - the file says healer, the log says tank")
            .And.MechanicWasNotFlagged(Barrage);
    }

    [Fact]
    public void No_rules_file_changes_nothing()
    {
        Given.IOpenedLog(ATankMechanic());

        Then.MechanicBelongsTo(Barrage, LogGrep.Models.Role.Tank)
            .And.FindingAdvises(Barrage, "This one follows the tank.")
            .And.TheRulesSayNothing();
    }

    [Fact]
    public void A_file_naming_an_ability_nobody_took_says_nothing()
    {
        Given.ARulesFileSaying(Ability.CreepingRot, "tank", "Dispel it.")
             .And.IOpenedLog(ATankMechanic());

        Then.TheRulesSayNothing()
            .And.MechanicBelongsTo(Barrage, LogGrep.Models.Role.Tank);
    }
}
