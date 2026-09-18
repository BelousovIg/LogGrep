using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// What happens the second time a log is opened, and while the game is still writing into it.
///
/// Reading a gigabyte and a half takes twelve seconds and running every rule over the result takes a
/// fifth of one, so the expensive thing is the file. A log whose bytes have not changed has nothing
/// left to say that it did not say the first time, and one that grew has only its tail to say.
///
/// The awkward case is the fight that is going on right now: a start with no end, which is
/// indistinguishable in the file from a log that simply stops. Neither is a result. It stays visible
/// in the list, where its byte range is still perfectly good to cut out, and out of the analysis,
/// where it is not an attempt at anything yet.
/// </summary>
public sealed class ReadingALogTwice : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue));

    private static void APull(PullBuilder p) => p
        .Lasting(2.Minutes())
        .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
        .At(2.Seconds()).BossSwingsAt("Rockjaw", 200_000)
        .Wipe();

    [Fact]
    public void A_log_opened_again_holds_what_it_held_before()
    {
        var log = ARaid().Pulls(3, Soulcoiler, Difficulty.Mythic, APull);

        Given.IOpenedLog(log);

        When.IReopenTheApp().ILookAtEncounter(Soulcoiler);

        Then.EncountersAreListed(Soulcoiler)
            .And.EncounterHasPulls(3);
    }

    [Fact]
    public void A_log_that_grew_keeps_its_fights_and_gains_the_new_ones()
    {
        // What a log does all evening. The file keeps its name and its first line, so it is the
        // same log with more in it - and only the more is worth reading.
        var log = ARaid().Pulls(2, Soulcoiler, Difficulty.Mythic, APull);

        Given.IOpenedLog(log);

        var later = ARaid().On(CombatLogBuilder.Evening(2026, 9, 15).AddHours(1))
            .Pulls(2, Soulcoiler, Difficulty.Mythic, APull);

        When.TheGameWritesMore(log.FileName, later).IReopenTheApp().ILookAtEncounter(Soulcoiler);

        Then.EncounterHasPulls(4);
    }

    [Fact]
    public void A_fight_that_has_not_ended_is_listed_and_not_judged()
    {
        // The game is still writing. The attempt is there to look at and to cut out, and it is not
        // an attempt at anything yet: no result, and a length that only says when the file stopped.
        var log = ARaid()
            .Pulls(2, Soulcoiler, Difficulty.Mythic, APull)
            .Unfinished(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(1.Minutes())
                .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000));

        Given.IOpenedLog(log);

        When.ILookAtEncounter(Soulcoiler);

        Then.EncounterHasPulls(3)
            .And.TheAttemptsJudgedAre(2);
    }

    [Fact]
    public void A_fight_that_ends_next_time_becomes_a_real_attempt()
    {
        // The unfinished one is never kept as a result, so when the end finally arrives it is read
        // from its own beginning and becomes an attempt like any other.
        var log = ARaid()
            .Pulls(2, Soulcoiler, Difficulty.Mythic, APull)
            .Unfinished(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(1.Minutes())
                .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000));

        Given.IOpenedLog(log);

        var rest = ARaid().On(CombatLogBuilder.Evening(2026, 9, 15).AddHours(1))
            .Pulls(1, Soulcoiler, Difficulty.Mythic, APull);

        When.TheGameWritesMore(log.FileName, rest).IReopenTheApp().ILookAtEncounter(Soulcoiler);

        Then.TheAttemptsJudgedAre(3);
    }
}
