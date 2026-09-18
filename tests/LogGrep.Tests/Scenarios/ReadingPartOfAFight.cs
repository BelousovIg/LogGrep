using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// Reading one stretch of an attempt rather than all of it.
///
/// A rate over a whole pull is an average over everything that happened in it, which is exactly
/// what somebody asking about the first burn phase does not want. Dragging across the chart picks
/// the stretch, and every rate in the table underneath is read over that stretch instead - the
/// chart and the numbers under it are always about the same piece of the fight.
///
/// The way back has to be as cheap as the way in, or people stop using the way in: a double-click
/// on the chart, and a link beside it that says what is being read.
/// </summary>
public sealed class ReadingPartOfAFight : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue));

    /// <summary>
    /// Two minutes, and everything Nightblade did landed in the first ten seconds of them. Over the
    /// whole attempt that is 10K; over the first minute it is 20K, and both are true of different
    /// questions.
    /// </summary>
    private static CombatLogBuilder AFrontLoadedPull() => ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
        .Lasting(2.Minutes())
        .At(10.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 1_200_000)
        .At(30.Seconds()).BossSwingsAt("Rockjaw", 200_000)
        .Wipe());

    [Fact]
    public void The_whole_attempt_is_where_the_numbers_start()
    {
        Given.IOpenedLog(AFrontLoadedPull()).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Nightblade");

        Then.TheAttemptIsReadOver("reading the whole attempt")
            .PlayerDpsIs("10K");
    }

    [Fact]
    public void A_stretch_of_the_fight_is_read_over_that_stretch_alone()
    {
        Given.IOpenedLog(AFrontLoadedPull()).IOpenedPull(Soulcoiler, number: 1);

        When.IReadTheFightFrom(0.Seconds(), 59.Seconds()).ILookAtPlayer("Nightblade");

        Then.TheAttemptIsReadOver("reading 0:00 to 0:59")
            .PlayerDpsIs("20K");
    }

    [Fact]
    public void A_stretch_with_nothing_in_it_reads_as_nothing_rather_than_as_the_average()
    {
        // The second minute, where Nightblade did not swing once. An average over the whole pull
        // would credit them with 10K of a minute they spent doing nothing.
        Given.IOpenedLog(AFrontLoadedPull()).IOpenedPull(Soulcoiler, number: 1);

        When.IReadTheFightFrom(1.Minutes(), 2.Minutes()).ILookAtPlayer("Nightblade");

        Then.PlayerDpsIs("—");
    }

    [Fact]
    public void Putting_it_back_puts_the_numbers_back()
    {
        Given.IOpenedLog(AFrontLoadedPull()).IOpenedPull(Soulcoiler, number: 1);

        When.IReadTheFightFrom(0.Seconds(), 59.Seconds())
            .IReadTheWholeAttempt()
            .ILookAtPlayer("Nightblade");

        Then.TheAttemptIsReadOver("reading the whole attempt")
            .PlayerDpsIs("10K");
    }
}
