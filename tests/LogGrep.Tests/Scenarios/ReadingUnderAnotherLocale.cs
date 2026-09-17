using System.Globalization;
using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// The app reads the same on any machine.
///
/// Three locale bugs were shipped before this test existed - a rate written "2,5", a month written
/// "вер.", a file name that would carry a different year under a non-Gregorian calendar - and every
/// one of them read as correct on the machine it was written on. A rule in a document does not
/// catch the fourth; running the whole suite under a hostile locale does.
///
/// The locale here names months in Thai and counts years in the Buddhist calendar, which puts 2026
/// at 2569 - so anything that formats a value itself rather than going through Display comes out
/// visibly wrong.
/// </summary>
public sealed class ReadingUnderAnotherLocale : Scenario, IDisposable
{
    private readonly CultureInfo _was = CultureInfo.CurrentCulture;

    public ReadingUnderAnotherLocale()
    {
        // Thai writes decimals its own way, names months in Thai, and counts years in the Buddhist
        // calendar - which puts 2026 at 2569. Anything that formats a value itself rather than
        // going through Display comes out visibly wrong under it.
        var hostile = new CultureInfo("th-TH");

        CultureInfo.CurrentCulture = hostile;
        CultureInfo.CurrentUICulture = hostile;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _was;
        CultureInfo.CurrentUICulture = _was;
    }

    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue));

    [Fact]
    public void Every_number_the_window_shows_reads_the_same_as_anywhere_else()
    {
        var log = ARaid().Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(1.Minutes(40))
            .At(30.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 1_250_000)
            .At(50.Seconds()).BossHits("Rockjaw", amount: 3_000_000)
            .At(1.Minutes(23)).Kills("Nightblade")
            .Wipe());

        Given.IOpenedLog(log).And.IOpenedPull(Soulcoiler, 1);

        Then.PullLasted("1:40")
            .And.TheLogRowReads("WoWCombatLog-091526_195900.txt",
                pulls: "1", encounters: "1", started: "15 Sep 20:00");

        When.ILookAtPlayer("Nightblade");
        Then.PlayerDpsIs("12.5K").And.PlayerDiedAt(1.Minutes(23));

        When.ILookAtPlayer("Rockjaw");
        Then.PlayerDtpsIs("30K");
    }
}
