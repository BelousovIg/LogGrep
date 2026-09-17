using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// The list of logs the window is reading. It replaced a read-only box that held one path, from
/// when the app could only read one file - by the time it could read several, that box was writing
/// sentences like "3 logs, starting with ..." to describe a set it had no room for.
/// </summary>
public sealed class KeepingTheLogList : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ANight(DateTime evening) => new CombatLogBuilder().On(evening).Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue));

    private static CombatLogBuilder Tuesday() => ANight(Evening(2026, 9, 15))
        .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(2.Minutes()).Wipe())
        .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(3.Minutes()).Kill());

    private static CombatLogBuilder Wednesday() => ANight(Evening(2026, 9, 16))
        .Pull(Boss.EntombedSentinels, Difficulty.Heroic, p => p.Lasting(2.Minutes()).Wipe());

    [Fact]
    public void Opening_a_log_puts_it_in_the_list_with_what_it_holds()
    {
        Given.IOpenedLog(Tuesday());

        Then.TheLogListHolds("WoWCombatLog-091526_195900.txt")
            .And.TheLogRowReads("WoWCombatLog-091526_195900.txt",
                pulls: "2", encounters: "1", started: "15 Sep 20:00");
    }

    [Fact]
    public void Opening_another_log_adds_to_the_list_rather_than_replacing_it()
    {
        Given.IOpenedLog(Tuesday()).And.IOpenedLog(Wednesday());

        Then.TheLogListHolds("WoWCombatLog-091526_195900.txt", "WoWCombatLog-091626_195900.txt")
            .And.EncountersAreListed(Soulcoiler, Boss.EntombedSentinels);
    }

    [Fact]
    public void The_same_file_twice_is_one_row()
    {
        var tuesday = Tuesday();

        Given.IOpenedLog(tuesday).And.IOpenedLog(tuesday);

        Then.TheLogListHolds("WoWCombatLog-091526_195900.txt");
    }

    [Fact]
    public void Removing_a_log_re_reads_what_is_left()
    {
        // Not the same as never having added it: the order of the evening and which duplicates were
        // dropped both change with the set, so what is left has to be read again.
        Given.IOpenedLog(Tuesday()).And.IOpenedLog(Wednesday());

        When.IRemoveTheLog("WoWCombatLog-091626_195900.txt");

        Then.TheLogListHolds("WoWCombatLog-091526_195900.txt")
            .And.EncountersAreListed(Soulcoiler);
    }

    [Fact]
    public void Removing_the_last_log_leaves_nothing_to_look_at()
    {
        Given.IOpenedLog(Tuesday());

        When.IRemoveTheLog("WoWCombatLog-091526_195900.txt");

        Then.TheLogListIsEmpty()
            .And.NothingIsListed();
    }

    [Fact]
    public void The_list_comes_back_after_a_restart()
    {
        Given.IOpenedLog(Tuesday()).And.IOpenedLog(Wednesday());

        When.IReopenTheApp();

        Then.TheLogListHolds("WoWCombatLog-091526_195900.txt", "WoWCombatLog-091626_195900.txt")
            .And.EncountersAreListed(Soulcoiler, Boss.EntombedSentinels);
    }

    [Fact]
    public void A_file_that_has_gone_comes_back_as_a_row_that_says_so()
    {
        // Quietly dropping it would leave somebody wondering why last night's numbers moved.
        Given.IOpenedLog(Tuesday()).And.IOpenedLog(Wednesday());

        When.IDeleteTheFile("WoWCombatLog-091626_195900.txt").And.IReopenTheApp();

        Then.TheLogListHolds("WoWCombatLog-091526_195900.txt", "WoWCombatLog-091626_195900.txt")
            .And.TheLogRowSaysItIsGone("WoWCombatLog-091626_195900.txt")
            .And.EncountersAreListed(Soulcoiler);
    }
}
