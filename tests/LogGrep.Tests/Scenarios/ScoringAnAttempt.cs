using LogGrep.Analysis;
using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// The four numbers a row carries, and the lane beside them.
///
/// A column of sentences does not survive a fourteenth rule being added, so the findings became two
/// things instead: terms in an index, and marks on the fight's own clock. What is asserted here is
/// that both still lead back to the findings - a score that cannot be opened is a grade, and a
/// grade is what this app is built not to hand out.
///
/// Every index here is a share of something that really happened. Output is a ratio to a ceiling
/// somebody reached; survival is a share of the damage that landed; mechanics is a share of the
/// times a thing went out. None of them rests on an allowance invented for the purpose, and the
/// scenarios are written in exact numbers because of it: a health pool in this log is a million,
/// so a 300K hit is three tenths of one and the arithmetic can be checked on paper.
///
/// The nights below are long on purpose. Every rule these scores are built from needs a run of
/// attempts before it will say anything at all, so a scenario that pulled twice would score an
/// empty report and prove nothing.
/// </summary>
public sealed class ScoringAnAttempt : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue),
        Damage("Emberfall", Spec.ArcaneMage),
        Damage("Thornwood", Spec.BalanceDruid));

    /// <summary>
    /// A clean pull: the tank opened it, the tank took the swings, nothing the app judges avoidable
    /// landed on anybody.
    /// </summary>
    private static PullBuilder Clean(PullBuilder p) => p
        .Lasting(2.Minutes())
        .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
        .At(2.Seconds()).BossSwingsAt("Rockjaw", 200_000)
        .At(10.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 900_000);

    /// <summary>
    /// A night where one spell keeps catching one person at a time. It has to catch different
    /// people across the evening, because a spell that only ever lands on damage dealers is a
    /// damage-dealer mechanic and the app refuses to call that anybody's mistake.
    /// </summary>
    private static CombatLogBuilder AMessyNight()
        => ARaid()
            .Pulls(6, Soulcoiler, Difficulty.Mythic, p => Caught(p, "Emberfall"))
            .Pulls(6, Soulcoiler, Difficulty.Mythic, p => Caught(p, "Sunwell"));

    private static void Caught(PullBuilder p, string who) => Clean(p)
        .At(30.Seconds()).BossHits(who, 300_000, with: Ability.HollowingStrikes)
        .Wipe();

    /// <summary>
    /// A night with a spell that is plainly the tank's - six of every seven landings are his - and
    /// one attempt in which it caught somebody else too.
    /// </summary>
    private static CombatLogBuilder ATankMechanic()
        => ARaid().Pulls(10, Soulcoiler, Difficulty.Mythic, p => Clean(p)
            .At(20.Seconds()).BossDebuffs("Rockjaw", Ability.SoulDrain)
            .At(25.Seconds()).BossDebuffs("Rockjaw", Ability.SoulDrain)
            .At(30.Seconds()).BossDebuffs("Rockjaw", Ability.SoulDrain)
            .At(35.Seconds()).BossDebuffs("Rockjaw", Ability.SoulDrain)
            .At(40.Seconds()).BossDebuffs("Rockjaw", Ability.SoulDrain)
            .At(45.Seconds()).BossDebuffs("Rockjaw", Ability.SoulDrain)
            .At(50.Seconds()).BossDebuffs("Emberfall", Ability.SoulDrain)
            .Wipe());

    [Fact]
    public void Nothing_landing_on_somebody_is_a_whole_score_for_surviving()
    {
        // Everybody states their own health on the events they cause, which a real log does
        // constantly, so a pool is known even for somebody nothing touched all fight. Their score
        // is whole because nothing landed, not because nothing was measured - the app keeps a dash
        // for the second case, which now takes a log damaged enough not to state a pool at all.
        Given.IOpenedLog(ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => Clean(p).Wipe()))
            .IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerScores(Axis.Survival, "100%")
            .PlayerScoreSays(Axis.Survival, "nothing landed on them");
    }

    [Fact]
    public void Damage_that_was_nobodys_fault_costs_nothing()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => Clean(p)
            .At(30.Seconds()).BossHits("Nightblade", 200_000)
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerScores(Axis.Survival, "100%")
            .PlayerScoreSays(Axis.Survival, "0 of 0.2 health pools was avoidable");
    }

    [Fact]
    public void Damage_that_was_theirs_to_avoid_comes_off_what_landed_on_them()
    {
        // Three tenths of a pool hit Emberfall and all three tenths of it was avoidable, so none of
        // what landed on them was the fight's doing. The denominator is real damage rather than an
        // allowance invented for the purpose, which is why this needs no threshold to stay fair.
        Given.IOpenedLog(AMessyNight()).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Emberfall");

        Then.PlayerScores(Axis.Survival, "0%")
            .PlayerScoreSays(Axis.Survival, "0.3 of 0.3 health pools was avoidable")
            .PlayerScoreSays(Axis.Survival, "of everything that hit them");
    }

    [Fact]
    public void A_mechanic_is_read_against_the_times_it_went_out()
    {
        // Soul Drain goes out six times at the tank and once at the mage. "You stood in it once" is
        // a different sentence from "you stood in one of six", and only the second one is a score.
        Given.IOpenedLog(ATankMechanic()).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Emberfall");

        Then.PlayerScores(Axis.Mechanics, "83%")
            .PlayerScoreSays(Axis.Mechanics, "caught 1 of the 6 times it went out");
    }

    [Fact]
    public void A_score_nothing_stands_behind_is_a_dash_and_says_why()
    {
        // One attempt, and a short one. There is no ceiling to measure a rate against, and no
        // business measuring a rate over thirteen seconds either, so the cell says neither.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(13.Seconds())
            .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerScores(Axis.Output, "—")
            .PlayerScoreSays(Axis.Output, "attempt shorter than a minute, nothing to judge");
    }

    [Fact]
    public void Where_the_swings_landed_is_what_the_tank_is_read_on()
    {
        // Three quarters of the melee went where it should have. Both tanks would carry the same
        // number, because they shared the job.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .At(2.Seconds()).BossSwingsAt("Rockjaw", 300_000)
            .At(30.Seconds()).BossSwingsAt("Nightblade", 100_000)
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Rockjaw");

        Then.PlayerScores(Axis.Duty, "75%")
            .PlayerScoreSays(Axis.Duty, "the enemy swings at whoever it is looking at");
    }

    [Fact]
    public void The_row_shows_the_worst_thing_that_happened_rather_than_a_list()
    {
        // A maximum fits a column forever. A list stops fitting at the fourth rule, which is the
        // whole reason the column had to become something else.
        Given.IOpenedLog(AMessyNight()).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Emberfall");

        Then.PlayerWorstIs("0:30 Hollowing Strikes - avoidable");
    }

    [Fact]
    public void Every_finding_is_a_mark_where_it_happened()
    {
        Given.IOpenedLog(AMessyNight()).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Emberfall");

        Then.PlayerLaneHas(1)
            .PlayerLaneShows(30.Seconds(), "Hollowing Strikes");
    }

    [Fact]
    public void A_death_is_one_mark_and_the_rule_that_explains_it_supplies_the_words()
    {
        // Deaths are drawn from the roster rather than from the findings, so one nobody has a
        // lesson about is still on the lane. Where a rule does explain it, its words win and the
        // death is not drawn twice.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => Clean(p)
            .At(1.Minutes()).Kills("Nightblade")
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Nightblade");

        Then.PlayerLaneHas(1)
            .PlayerLaneShows(1.Minutes(), "Blast Wave killed you");
    }

    [Fact]
    public void One_thing_that_caught_the_group_is_drawn_as_one_thing()
    {
        // Three of five gone in the same second. That is not three mistakes, and a row of hollow
        // marks lined up down the table says so without anybody reading a word.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => Clean(p)
            .At(1.Minutes()).Kills("Nightblade")
            .At(1.Minutes()).Kills("Emberfall")
            .At(1.Minutes()).Kills("Thornwood")
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Emberfall");

        Then.PlayerLaneSharedIt(1.Minutes(), true);
    }

    [Fact]
    public void A_mistake_of_your_own_is_not_drawn_as_the_groups()
    {
        Given.IOpenedLog(AMessyNight()).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Emberfall");

        Then.PlayerLaneSharedIt(30.Seconds(), false);
    }

    [Fact]
    public void What_the_enemy_cast_runs_on_the_same_clock_above_the_group()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => Clean(p)
            .At(20.Seconds()).BossCasts(Ability.SoulDrain)
            .At(50.Seconds()).BossCasts(Ability.HollowingStrikes)
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        Then.TheEnemyLaneHas(2)
            .TheEnemyLaneShows(20.Seconds(), "Soul Drain");
    }

    [Fact]
    public void The_attempt_names_who_its_losses_went_to()
    {
        // Not an average over the roster - an average hides the one person who lost the pull behind
        // the four who did not.
        Given.IOpenedLog(AMessyNight()).IOpenedPull(Soulcoiler, number: 1);

        Then.TheAttemptCost("0.3 health pools lost")
            .TheAttemptBlames("Emberfall", "0.3 pools");
    }

    [Fact]
    public void Opening_a_pull_badly_is_priced_and_is_not_averaged_into_anything()
    {
        // The rule fires - it is in the findings, with a cost, and on the lane - and no index moves,
        // because an attempt offers exactly one opening and a percentage drawn from a sample of one
        // is an invention wearing a measurement's clothes.
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(0.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 900_000)
            .At(5.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
            .Wipe());

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Nightblade");

        Then.ThePullSays("Nightblade", "opened the pull")
            .PlayerScores(Axis.Mechanics, "100%")
            .PlayerLaneShows(0.Seconds(), "opened the pull");
    }
}
