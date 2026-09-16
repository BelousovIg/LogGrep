using LogGrep.Models;
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
    private const Boss Soulcoiler = Boss.TheSoulcoiler;
    private const Ability TankMechanic = Ability.PossessionBarrage;

    /// <summary>The written fix for a tank mechanic - the one field of a finding nothing can derive.</summary>
    private const string TankAdvice =
        "This one follows the tank. On anybody else it means a swap went wrong, " +
        "or you were the nearest thing to a tank when it picked.";

    /// <summary>What the app says it is going by, once the night below has run.</summary>
    private const string OverElevenAttempts = "22 of 23 hit a tank, over 11 attempts";

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
    private static CombatLogBuilder ANightThatWentRight(Ability mechanic = TankMechanic)
        => ARaid().Pulls(Habit, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(20.Seconds()).BossDebuffs("Rockjaw", with: mechanic, times: 2)
            .Wipe());

    /// <summary>One more attempt, where the healer took it as well.</summary>
    private static CombatLogBuilder AndThenTheHealerTookIt(TimeSpan at)
        => ANightThatWentRight().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(20.Seconds()).BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
            .At(at).BossDebuffs("Sunwell", with: TankMechanic)
            .Wipe());

    [Fact]
    public void A_mechanic_the_tanks_always_take_is_nobodys_mistake()
    {
        Given.IOpenedLog(ANightThatWentRight());

        Then.NothingWasFound();
    }

    [Fact]
    public void A_healer_who_took_the_tank_mechanic_is_reported()
    {
        Given.IOpenedLog(AndThenTheHealerTookIt(at: 1.Minutes(30)));

        Then.MechanicBelongsTo(TankMechanic, Role.Tank)
            .And.TookMechanicOutOfTurn(TankMechanic, "Sunwell");
    }

    [Fact]
    public void The_rule_says_what_it_was_drawn_from()
    {
        Given.IOpenedLog(AndThenTheHealerTookIt(at: 1.Minutes(30)));

        Then.MechanicEvidenceReads(TankMechanic, OverElevenAttempts);
    }

    [Fact]
    public void A_debuff_only_the_damage_take_is_not_called_their_mechanic()
    {
        // Seven of the ten are damage, so them taking a thing proves nothing on its own. Without
        // that test this would read as a damage mechanic and every other role would be a mistake.
        var log = ARaid().Pulls(Habit, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(20.Seconds()).BossDebuffs("Nightblade", with: Ability.CreepingRot, times: 2)
            .At(1.Minutes()).BossDebuffs("Emberwild", with: Ability.CreepingRot, times: 2)
            .Wipe());

        Given.IOpenedLog(log);

        Then.MechanicWasNotFlagged(Ability.CreepingRot)
            .And.NothingWasFound();
    }

    [Fact]
    public void A_handful_of_applications_is_not_a_pattern()
    {
        // A full night of attempts, but the mechanic barely happened.
        var log = ARaid()
            .Pulls(5, Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes()).At(20.Seconds()).BossDebuffs("Rockjaw", with: TankMechanic).Wipe())
            .Pulls(4, Soulcoiler, Difficulty.Mythic, p => p.Lasting(3.Minutes()).Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes()).At(1.Minutes(30)).BossDebuffs("Sunwell", with: TankMechanic).Wipe());

        Given.IOpenedLog(log);

        Then.NothingWasFound();
    }

    [Fact]
    public void A_couple_of_attempts_are_not_a_pattern_either()
    {
        // Plenty of applications, and a clear enough split - but over three attempts, which is not
        // a habit. A night where the same mistake repeats makes itself the majority of its sample.
        var log = ARaid().Pulls(3, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(20.Seconds()).BossDebuffs("Rockjaw", with: TankMechanic, times: 8)
            .At(1.Minutes(30)).BossDebuffs("Sunwell", with: TankMechanic)
            .Wipe());

        Given.IOpenedLog(log);

        Then.NothingWasFound();
    }

    [Fact]
    public void A_friendly_buff_is_never_read_as_a_mechanic()
    {
        var log = ARaid().Pulls(Habit, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(20.Seconds())
            .Buffs("Sunwell", target: "Rockjaw", with: Ability.PowerWordFortitude, times: 12)
            .Wipe());

        Given.IOpenedLog(log);

        Then.NothingWasFound();
    }

    [Fact]
    public void A_finding_points_at_the_attempt_and_the_moment()
    {
        Given.IOpenedLog(AndThenTheHealerTookIt(at: 1.Minutes(30)));

        Then.FindingPointsAt(TankMechanic, "Sunwell", pull: 11, at: 1.Minutes(30));
    }

    [Fact]
    public void A_mistake_that_killed_somebody_outweighs_one_they_walked_away_from()
    {
        // Same mechanic, same attempt, two people: what separates the two findings is what each
        // one cost, and that is the only thing the order is allowed to rest on.
        var log = ANightThatWentRight().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(5.Minutes())
            .At(20.Seconds()).BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
            .At(1.Minutes(30)).BossDebuffs("Sunwell", with: TankMechanic)
            .At(1.Minutes(35)).Kills("Sunwell", with: TankMechanic)
            .At(2.Minutes()).BossDebuffs("Nightblade", with: TankMechanic)
            .Wipe());

        Given.IOpenedLog(log);

        Then.FindingsAreOrderedByCost()
            .And.MistakeKilled(TankMechanic, "Sunwell", at: 1.Minutes(35))
            .And.MistakeWasSurvived(TankMechanic, "Nightblade");
    }

    [Fact]
    public void A_death_long_after_the_mechanic_is_not_laid_at_its_door()
    {
        // A minute later is a different story, and a finding that claims otherwise is worse than
        // no finding at all - it sends somebody to look at the wrong moment.
        var log = ANightThatWentRight().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(5.Minutes())
            .At(20.Seconds()).BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
            .At(1.Minutes(30)).BossDebuffs("Sunwell", with: TankMechanic)
            .At(2.Minutes(30)).Kills("Sunwell")
            .Wipe());

        Given.IOpenedLog(log);

        Then.MistakeWasSurvived(TankMechanic, "Sunwell");
    }

    [Fact]
    public void A_finding_says_what_to_do_about_it()
    {
        Given.IOpenedLog(AndThenTheHealerTookIt(at: 1.Minutes(30)));

        Then.FindingAdvises(TankMechanic, TankAdvice);
    }

    [Fact]
    public void The_summary_counts_the_mistakes_and_the_people_who_made_them()
    {
        // Three mistakes over two mechanics, but the summary counts people rather than mechanics:
        // detectors other than this one have no mechanic to count, and a person always has a name.
        Given.IOpenedLog(TwoMechanics());

        Then.FindingsSummaryCounts(mistakes: 3, players: 3);
    }

    [Fact]
    public void The_encounter_counts_the_attempts_that_went_wrong()
    {
        var log = ANightThatWentRight()
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes())
                .At(20.Seconds()).BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
                .At(1.Minutes()).BossDebuffs("Sunwell", with: TankMechanic)
                .Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes())
                .At(20.Seconds()).BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
                .At(1.Minutes()).BossDebuffs("Nightblade", with: TankMechanic)
                .Kill());

        Given.IOpenedLog(log);
        When.ILookAtEncounter(Soulcoiler);

        Then.EncounterMistakesRead(withMistakes: 2, ofPulls: 12);
    }

    [Fact]
    public void The_attempt_counts_every_mistake_made_in_it()
    {
        // Two people, each taking two mechanics that were not theirs, on the same attempt.
        var log = ARaid()
            .Pulls(Habit, Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(5.Minutes())
                .At(20.Seconds()).BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
                .At(2.Minutes()).BossDebuffs("Grimhide", with: Ability.HollowingStrikes, times: 2)
                .Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(5.Minutes())
                .At(20.Seconds()).BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
                .At(1.Minutes()).BossDebuffs("Sunwell", with: TankMechanic)
                .At(1.Minutes(10)).BossDebuffs("Nightblade", with: TankMechanic)
                .At(2.Minutes()).BossDebuffs("Grimhide", with: Ability.HollowingStrikes, times: 2)
                .At(3.Minutes()).BossDebuffs("Sunwell", with: Ability.HollowingStrikes)
                .At(3.Minutes(10)).BossDebuffs("Nightblade", with: Ability.HollowingStrikes)
                .Wipe());

        Given.IOpenedLog(log).And.IExpandedEncounter(Soulcoiler);
        When.ILookAtPull(11);

        Then.PullMistakesRead(4);
    }

    [Fact]
    public void The_player_lists_each_mistake_with_the_moment_it_happened()
    {
        var log = ARaid()
            .Pulls(Habit, Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(5.Minutes())
                .At(10.Seconds()).BossDebuffs("Rockjaw", with: Ability.HollowingStrikes, times: 2)
                .At(1.Minutes()).BossDebuffs("Grimhide", with: TankMechanic, times: 2)
                .Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(5.Minutes())
                .At(10.Seconds()).BossDebuffs("Rockjaw", with: Ability.HollowingStrikes, times: 2)
                .At(31.Seconds()).BossDebuffs("Sunwell", with: Ability.HollowingStrikes)
                .At(1.Minutes()).BossDebuffs("Grimhide", with: TankMechanic, times: 2)
                .At(2.Minutes(51)).BossDebuffs("Sunwell", with: TankMechanic)
                .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Soulcoiler, 11);
        When.ILookAtPlayer("Sunwell");

        var strikes = TookOutOfTurn(31.Seconds(), Ability.HollowingStrikes);
        var barrage = TookOutOfTurn(2.Minutes(51), TankMechanic);

        Then.PlayerMistakesRead(strikes, barrage)
            .And.PlayerMistakesTooltipReads(strikes, barrage);
    }

    [Fact]
    public void Somebody_who_took_nothing_of_anyone_elses_shows_a_dash()
    {
        Given.IOpenedLog(AndThenTheHealerTookIt(at: 1.Minutes()))
             .And.IOpenedPull(Soulcoiler, 11);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerHasNoMistakes();
    }

    /// <summary>A tank mechanic a healer took and lived through, as the column and the tooltip read it.</summary>
    private static AMistake TookOutOfTurn(TimeSpan at, Ability spell) => new(at, spell, Role.Tank)
    {
        Evidence = "went to a healer; " + OverElevenAttempts,
        Cost = "survived it",
        Advice = TankAdvice,
    };

    /// <summary>A night where one mechanic was taken wrongly by two people and another by one.</summary>
    private static CombatLogBuilder TwoMechanics() => ARaid()
        .Pulls(Habit, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(5.Minutes())
            .At(20.Seconds()).BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
            .At(2.Minutes(30)).BossDebuffs("Grimhide", with: Ability.HollowingStrikes, times: 2)
            .Wipe())
        .Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(5.Minutes())
            .At(20.Seconds()).BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
            .At(1.Minutes(30)).BossDebuffs("Sunwell", with: TankMechanic)
            .At(2.Minutes()).BossDebuffs("Nightblade", with: TankMechanic)
            .At(2.Minutes(30)).BossDebuffs("Grimhide", with: Ability.HollowingStrikes, times: 2)
            .At(4.Minutes()).BossDebuffs("Emberwild", with: Ability.HollowingStrikes)
            .Wipe());
}
