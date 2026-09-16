using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// Whether the app can tell who took a mechanic that was not theirs. Nothing here tells it what any
/// spell does - it has to work that out from how the night went, which is the whole point.
///
/// Every scenario runs a night of attempts rather than one, because that is what the app asks for:
/// a rule is drawn from a habit, and a habit needs a run to show itself.
/// </summary>
public sealed class FindingMistakes : Scenario
{
    private const string Boss = "The Soulcoiler";
    private const string TankMechanic = "Possession Barrage";

    /// <summary>Attempts it takes before the app is willing to call anything a rule.</summary>
    private const int Habit = 10;

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

    /// <summary>A run of attempts where the mechanic went where it belongs, every time.</summary>
    private static CombatLogBuilder ANightThatWentRight(string mechanic = TankMechanic)
        => ARaid().Pulls(Habit, Boss, Difficulty.Mythic, p => p
            .Lasting("3:00")
            .At("0:20").BossDebuffs("Rockjaw", with: mechanic, times: 2)
            .Wipe());

    [Fact]
    public void A_mechanic_the_tanks_always_take_is_nobodys_mistake()
    {
        Given.IOpenedLog(ANightThatWentRight()).And.IOpenedFindings();

        Then.NothingWasFound();
    }

    [Fact]
    public void A_healer_who_took_the_tank_mechanic_is_reported()
    {
        var log = ANightThatWentRight().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("3:00")
            .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
            .At("1:30").BossDebuffs("Sunwell", with: TankMechanic)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedFindings();

        Then.MechanicBelongsTo(TankMechanic, "tank")
            .And.TookMechanicOutOfTurn(TankMechanic, "Sunwell");
    }

    [Fact]
    public void The_rule_says_what_it_was_drawn_from()
    {
        var log = ANightThatWentRight().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("3:00")
            .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
            .At("1:30").BossDebuffs("Sunwell", with: TankMechanic)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedFindings();

        Then.MechanicEvidenceReads(TankMechanic, "22 of 23 hit a tank, over 11 attempts");
    }

    [Fact]
    public void A_debuff_only_the_damage_take_is_not_called_their_mechanic()
    {
        // Seven of the ten are damage, so them taking a thing proves nothing on its own. Without
        // that test this would read as a damage mechanic and every other role would be a mistake.
        var log = ARaid().Pulls(Habit, Boss, Difficulty.Mythic, p => p
            .Lasting("3:00")
            .At("0:20").BossDebuffs("Nightblade", with: "Creeping Rot", times: 2)
            .At("1:00").BossDebuffs("Emberwild", with: "Creeping Rot", times: 2)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedFindings();

        Then.MechanicWasNotFlagged("Creeping Rot")
            .And.NothingWasFound();
    }

    [Fact]
    public void A_handful_of_applications_is_not_a_pattern()
    {
        // A full night of attempts, but the mechanic barely happened.
        var log = ARaid()
            .Pulls(5, Boss, Difficulty.Mythic, p => p
                .Lasting("3:00").At("0:20").BossDebuffs("Rockjaw", with: TankMechanic).Wipe())
            .Pulls(4, Boss, Difficulty.Mythic, p => p.Lasting("3:00").Wipe())
            .Pull(Boss, Difficulty.Mythic, p => p
                .Lasting("3:00").At("1:30").BossDebuffs("Sunwell", with: TankMechanic).Wipe());

        Given.IOpenedLog(log).And.IOpenedFindings();

        Then.NothingWasFound();
    }

    [Fact]
    public void A_couple_of_attempts_are_not_a_pattern_either()
    {
        // Plenty of applications, and a clear enough split - but over three attempts, which is not
        // a habit. A night where the same mistake repeats makes itself the majority of its sample.
        var log = ARaid().Pulls(3, Boss, Difficulty.Mythic, p => p
            .Lasting("3:00")
            .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 8)
            .At("1:30").BossDebuffs("Sunwell", with: TankMechanic)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedFindings();

        Then.NothingWasFound();
    }

    [Fact]
    public void A_friendly_buff_is_never_read_as_a_mechanic()
    {
        var log = ARaid().Pulls(Habit, Boss, Difficulty.Mythic, p => p
            .Lasting("3:00")
            .At("0:20").Buffs("Sunwell", target: "Rockjaw", with: "Power Word: Fortitude", times: 12)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedFindings();

        Then.NothingWasFound();
    }

    [Fact]
    public void A_finding_points_at_the_attempt_and_the_moment()
    {
        var log = ANightThatWentRight().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("3:00")
            .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
            .At("1:30").BossDebuffs("Sunwell", with: TankMechanic)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedFindings();

        Then.FindingPointsAt(TankMechanic, "Sunwell", pull: "pull 11", at: "1:30");
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

    [Fact]
    public void The_encounter_counts_the_attempts_that_went_wrong()
    {
        var log = ANightThatWentRight()
            .Pull(Boss, Difficulty.Mythic, p => p
                .Lasting("3:00")
                .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
                .At("1:00").BossDebuffs("Sunwell", with: TankMechanic)
                .Wipe())
            .Pull(Boss, Difficulty.Mythic, p => p
                .Lasting("3:00")
                .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
                .At("1:00").BossDebuffs("Nightblade", with: TankMechanic)
                .Kill());

        Given.IOpenedLog(log);
        When.ILookAtEncounter(Boss);

        Then.EncounterMistakesRead("2/12");
    }

    [Fact]
    public void The_attempt_counts_every_mistake_made_in_it()
    {
        // Two people, each taking two mechanics that were not theirs, on the same attempt.
        var log = ARaid()
            .Pulls(Habit, Boss, Difficulty.Mythic, p => p
                .Lasting("5:00")
                .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
                .At("2:00").BossDebuffs("Grimhide", with: "Hollowing Strikes", times: 2)
                .Wipe())
            .Pull(Boss, Difficulty.Mythic, p => p
                .Lasting("5:00")
                .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
                .At("1:00").BossDebuffs("Sunwell", with: TankMechanic)
                .At("1:10").BossDebuffs("Nightblade", with: TankMechanic)
                .At("2:00").BossDebuffs("Grimhide", with: "Hollowing Strikes", times: 2)
                .At("3:00").BossDebuffs("Sunwell", with: "Hollowing Strikes")
                .At("3:10").BossDebuffs("Nightblade", with: "Hollowing Strikes")
                .Wipe());

        Given.IOpenedLog(log).And.IExpandedEncounter(Boss);
        When.ILookAtPull(11);

        Then.PullMistakesRead("4");
    }

    [Fact]
    public void The_player_lists_each_mistake_with_the_moment_it_happened()
    {
        var log = ARaid()
            .Pulls(Habit, Boss, Difficulty.Mythic, p => p
                .Lasting("5:00")
                .At("0:10").BossDebuffs("Rockjaw", with: "Hollowing Strikes", times: 2)
                .At("1:00").BossDebuffs("Grimhide", with: TankMechanic, times: 2)
                .Wipe())
            .Pull(Boss, Difficulty.Mythic, p => p
                .Lasting("5:00")
                .At("0:10").BossDebuffs("Rockjaw", with: "Hollowing Strikes", times: 2)
                .At("0:31").BossDebuffs("Sunwell", with: "Hollowing Strikes")
                .At("1:00").BossDebuffs("Grimhide", with: TankMechanic, times: 2)
                .At("2:51").BossDebuffs("Sunwell", with: TankMechanic)
                .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Boss, 11);
        When.ILookAtPlayer("Sunwell");

        Then.PlayerMistakesRead("0:31 Hollowing Strikes - tank mechanic; 2:51 Possession Barrage - tank mechanic")
            .And.PlayerMistakesTooltipReads(
                "0:31  Hollowing Strikes\n      went to a healer; 22 of 23 hit a tank, over 11 attempts",
                "2:51  Possession Barrage\n      went to a healer; 22 of 23 hit a tank, over 11 attempts");
    }

    [Fact]
    public void Somebody_who_took_nothing_of_anyone_elses_shows_a_dash()
    {
        var log = ANightThatWentRight().Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("3:00")
            .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
            .At("1:00").BossDebuffs("Sunwell", with: TankMechanic)
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Boss, 11);
        When.ILookAtPlayer("Nightblade");

        Then.PlayerMistakesRead("—");
    }

    /// <summary>A night where one mechanic was taken wrongly by two people and another by one.</summary>
    private static CombatLogBuilder TwoMechanics() => ARaid()
        .Pulls(Habit, Boss, Difficulty.Mythic, p => p
            .Lasting("5:00")
            .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
            .At("2:30").BossDebuffs("Grimhide", with: "Hollowing Strikes", times: 2)
            .Wipe())
        .Pull(Boss, Difficulty.Mythic, p => p
            .Lasting("5:00")
            .At("0:20").BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
            .At("1:30").BossDebuffs("Sunwell", with: TankMechanic)
            .At("2:00").BossDebuffs("Nightblade", with: TankMechanic)
            .At("2:30").BossDebuffs("Grimhide", with: "Hollowing Strikes", times: 2)
            .At("4:00").BossDebuffs("Emberwild", with: "Hollowing Strikes")
            .Wipe());
}
