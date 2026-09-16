using LogGrep.Models;
using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// A tier is fought over several nights and several files, and the app needs ten attempts at a boss
/// before it will call anything a rule. One night's file often cannot reach that on its own; the
/// same nights read together clear it without trying. These are about what happens when more than
/// one log is open at once - what joins, what stays apart, and what gets counted twice if nobody
/// is watching.
/// </summary>
public sealed class ReadingSeveralLogs : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;
    private const Ability TankMechanic = Ability.PossessionBarrage;

    private static readonly DateTime Tuesday = Evening(2026, 9, 15);
    private static readonly DateTime Wednesday = Evening(2026, 9, 16);

    /// <summary>Two tanks, one healer, seven damage - so tanks are a fifth of the group.</summary>
    private static CombatLogBuilder ANight(DateTime evening) => new CombatLogBuilder().On(evening).Raid(
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

    /// <summary>Attempts where the mechanic went where it belongs.</summary>
    private static CombatLogBuilder Attempts(CombatLogBuilder log, int times, Difficulty difficulty = Difficulty.Mythic)
        => log.Pulls(times, Soulcoiler, difficulty, p => p
            .Lasting(3.Minutes())
            .At(20.Seconds()).BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
            .Wipe());

    /// <summary>One night, three attempts, each a different length so the order shows.</summary>
    private static CombatLogBuilder ThreeAttempts() => ANight(Tuesday)
        .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(1.Minutes()).Wipe())
        .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(2.Minutes()).Wipe())
        .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(3.Minutes()).Kill());

    /// <summary>And one where the healer took it too.</summary>
    private static CombatLogBuilder AndOneThatWentWrong(CombatLogBuilder log)
        => log.Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(20.Seconds()).BossDebuffs("Rockjaw", with: TankMechanic, times: 2)
            .At(1.Minutes(30)).BossDebuffs("Sunwell", with: TankMechanic)
            .Wipe());

    [Fact]
    public void One_night_on_its_own_stays_below_the_floor()
    {
        // Six attempts is not a habit, so the same mistake in the same file says nothing yet. This
        // is the state the next scenario is the answer to.
        Given.IOpenedLog(AndOneThatWentWrong(Attempts(ANight(Tuesday), 5)));

        When.ILookAtEncounter(Soulcoiler);

        Then.EncounterHasPulls(6)
            .And.NothingWasFound();
    }

    [Fact]
    public void A_boss_fought_over_two_nights_is_one_run_of_attempts()
    {
        // The same six, plus five more from the night after. Nothing about either file changed -
        // there are simply enough attempts now for the app to be willing to say something.
        Given.IOpenedLogs(
            AndOneThatWentWrong(Attempts(ANight(Tuesday), 5)),
            Attempts(ANight(Wednesday), 5));

        When.ILookAtEncounter(Soulcoiler);

        Then.LogsWereRead(2)
            .And.EncountersAreListed(Soulcoiler)
            .And.EncounterHasPulls(11)
            .And.MechanicBelongsTo(TankMechanic, Role.Tank)
            .And.TookMechanicOutOfTurn(TankMechanic, "Sunwell");
    }

    [Fact]
    public void Two_difficulties_at_one_boss_stay_apart()
    {
        // The key that separates heroic from mythic inside one file separates them across two, and
        // a heroic attempt has nothing to say about how a mythic one should have gone.
        Given.IOpenedLogs(
            Attempts(ANight(Tuesday), 3),
            Attempts(ANight(Wednesday), 4, Difficulty.Heroic));

        Then.EncountersAreListed(Soulcoiler, Soulcoiler)
            .And.EncounterDifficultiesAre("Mythic", "Heroic");
    }

    [Fact]
    public void Each_keystone_run_stays_a_row_of_its_own()
    {
        // Two runs of one dungeon are two separate things that happen to share a name. Before the
        // files were joined the counter that kept them apart started again in each file, which
        // would have folded one night's run into the other's.
        Given.IOpenedLogs(
            ANight(Tuesday).Keystone(Dungeon.TheRookery, level: 12, p => p.Lasting(24.Minutes()).Kill()),
            ANight(Wednesday).Keystone(Dungeon.TheRookery, level: 13, p => p.Lasting(27.Minutes()).Wipe()));

        Then.EncountersAreListed(Dungeon.TheRookery, Dungeon.TheRookery)
            .And.EncounterDifficultiesAre("Mythic+ +12", "Mythic+ +13");
    }

    [Fact]
    public void The_evening_runs_in_the_order_the_logs_were_recorded()
    {
        // Handed over in the wrong order on purpose. What orders them is what is inside them.
        Given.IOpenedLogs(
            ANight(Wednesday).Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(2.Minutes()).Kill()),
            ANight(Tuesday).Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(1.Minutes()).Wipe()));

        When.ILookAtEncounter(Soulcoiler);

        Then.LogsAreOrdered(Tuesday.Date, Wednesday.Date)
            .And.PullsLasted(1.Minutes(), 2.Minutes());
    }

    [Fact]
    public void A_log_copied_from_another_machine_is_placed_by_what_is_inside_it()
    {
        // Copying a log resets the date the filesystem has for it. Sorting on that would present
        // the evening backwards, so the log's own first timestamp decides and the person is told
        // the two disagree rather than left to wonder why Tuesday came last.
        Given.IOpenedLogs(
            ANight(Tuesday)
                .CopiedOn(Evening(2026, 12, 25))
                .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(1.Minutes()).Wipe()),
            ANight(Wednesday)
                .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(2.Minutes()).Kill()));

        When.ILookAtEncounter(Soulcoiler);

        Then.LogsAreOrdered(Tuesday.Date, Wednesday.Date)
            .And.PullsLasted(1.Minutes(), 2.Minutes())
            .And.ReadingNoted("It has been placed by what is inside it.");
    }

    [Fact]
    public void An_export_opened_beside_the_log_it_came_from_is_not_counted_twice()
    {
        // The folder these were all tested against holds exactly this: a full log and slices taken
        // out of it. A doubled attempt inflates every count, and a doubled mistake looks like a
        // habit - which is the one thing the ten-attempt floor exists to prevent.
        var night = ANight(Tuesday)
            .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(1.Minutes()).Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(2.Minutes()).Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(3.Minutes()).Kill());

        // The same first attempt, cut out on its own - same fight, same second, another file.
        var slice = ANight(Tuesday)
            .Called("The-Soulcoiler_2026-09-15_20-00-00.txt")
            .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(1.Minutes()).Wipe());

        Given.IOpenedLogs(night, slice);

        When.ILookAtEncounter(Soulcoiler);

        Then.LogsWereRead(2)
            .And.EncounterHasPulls(3)
            .And.PullsLasted(1.Minutes(), 2.Minutes(), 3.Minutes())
            .And.ReadingNoted("holds the same 1 attempt as another of these files")
            .And.PullsCameFrom("WoWCombatLog-091526_195900.txt");
    }

    [Fact]
    public void A_log_under_somebody_elses_name_is_still_placed_by_what_is_inside_it()
    {
        // The game names a file for the night it starts, so a name that disagrees with the log is a
        // renamed or misfiled one. It still reads - it is just worth saying so.
        Given.IOpenedLogs(
            ANight(Tuesday)
                .Called("WoWCombatLog-010126_120000.txt")
                .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(1.Minutes()).Wipe()),
            ANight(Wednesday)
                .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(2.Minutes()).Kill()));

        When.ILookAtEncounter(Soulcoiler);

        Then.PullsLasted(1.Minutes(), 2.Minutes())
            .And.ReadingNoted("is named for");
    }

    [Fact]
    public void Files_that_overlap_in_the_middle_share_only_what_they_share()
    {
        // Logging restarted mid-evening, so the second file opens on an attempt the first one
        // already has and then carries on past it. Neither file is a copy of the other; one attempt
        // is in both.
        var first = ANight(Tuesday)
            .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(1.Minutes()).Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(2.Minutes()).Wipe());

        var second = ANight(Tuesday + 2.Minutes())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(2.Minutes()).Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(3.Minutes()).Kill());

        Given.IOpenedLogs(first, second);

        When.ILookAtEncounter(Soulcoiler);

        Then.LogsWereRead(2)
            .And.PullsLasted(1.Minutes(), 2.Minutes(), 3.Minutes())
            .And.ReadingNoted("1 attempt was in more than one of these files and counted once.");
    }

    [Fact]
    public void The_same_log_handed_over_twice_is_read_once()
    {
        // Two paths to one file. Reading it twice would cost a second pass over a gigabyte and then
        // have to be undone at the other end.
        var night = ThreeAttempts();

        Given.IOpenedLogs(night, night);

        When.ILookAtEncounter(Soulcoiler);

        Then.LogsWereRead(1)
            .And.EncounterHasPulls(3)
            .And.NothingWasRemarkedOn();
    }

    [Fact]
    public void A_copy_of_a_log_under_another_name_is_named_as_a_copy()
    {
        // The same night saved twice. Counting the attempts once is right, but saying "three
        // attempts were shared" describes it poorly - a whole file was a duplicate.
        Given.IOpenedLogs(ThreeAttempts(), ThreeAttempts().Called("a-copy-of-tuesday.txt"));

        When.ILookAtEncounter(Soulcoiler);

        Then.LogsWereRead(2)
            .And.EncounterHasPulls(3)
            .And.PullsCameFrom("WoWCombatLog-091526_195900.txt")
            .And.ReadingNoted("holds the same 3 attempts as another of these files and nothing else.");
    }

    [Fact]
    public void Two_nights_that_share_nothing_are_read_without_remark()
    {
        Given.IOpenedLogs(
            Attempts(ANight(Tuesday), 2),
            Attempts(ANight(Wednesday), 2));

        When.ILookAtEncounter(Soulcoiler);

        Then.EncounterHasPulls(4)
            .And.NothingWasRemarkedOn();
    }
}
