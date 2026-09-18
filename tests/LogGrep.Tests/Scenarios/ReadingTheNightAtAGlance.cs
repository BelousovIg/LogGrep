using LogGrep.ViewModels;
using LogGrep.Models;
using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// The night as a row of tags, above any table of it.
///
/// Nobody remembers which pull was the close one. A row of "64%" with one "8%" in it says so before
/// a single number is read, and each tag opens the attempt it is about - which makes it the cheapest
/// way into the report as well as the quickest look at the evening.
///
/// Each one carries four things: what was left of the boss and the phase it was left in on the small
/// side, which attempt it was with its length and when it ran on the large one.
/// </summary>
public sealed class ReadingTheNightAtAGlance : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static readonly DateTime Tuesday = new(2026, 9, 15, 20, 30, 0);

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().On(Tuesday).Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue));

    [Fact]
    public void Every_attempt_of_the_sample_is_a_tag()
    {
        // A quarter off on the first, three quarters on the second, and the third put it down.
        var log = ARaid()
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(2.Minutes())
                .At(10.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 125_000_000)
                .At(11.Seconds()).BossSwingsAt("Rockjaw", 1000)
                .Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(3.Minutes())
                .At(10.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 375_000_000)
                .At(11.Seconds()).BossSwingsAt("Rockjaw", 1000)
                .Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(1.Minutes(30))
                .At(10.Seconds()).Deals("Nightblade", to: Soulcoiler, amount: 500_000_000)
                .At(11.Seconds()).BossSwingsAt("Rockjaw", 1000)
                .Kill());

        Given.IOpenedLog(log);

        When.IAnalyseTheEncounter(Soulcoiler);

        Then.TheAttemptTagsRead(
            "75% P1 1 (2:00) 20:30",
            "25% P1 2 (3:00) 20:33",
            "0% P1 3 (1:30) 20:37");
    }

    [Fact]
    public void Narrowing_the_sample_narrows_the_row_of_tags()
    {
        // The tags are numbered by their place in the sample, because the grid's columns are - and a
        // tag that named a different attempt from the column under it would be worse than no tag.
        var log = ARaid()
            .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(2.Minutes()).Wipe())
            .Pull(Soulcoiler, Difficulty.Mythic, p => p.Lasting(1.Minutes(30)).Kill());

        Given.IOpenedLog(log);

        When.IAnalyseTheEncounter(Soulcoiler).INarrowTo(Outcome.Kills);

        Then.TheAttemptTagsRead("0% P1 1 (1:30) 20:33");
    }
}
