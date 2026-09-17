using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// Facts about the evening rather than about any one attempt. The first death of an attempt is
/// often the one the rest followed from, and who keeps opening the night is the thing a raid leader
/// is trying to remember afterwards.
/// </summary>
public sealed class ReadingTheNight : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue),
        Damage("Emberwild", Spec.ArcaneMage));

    /// <summary>An attempt where one person takes a mechanic that is not theirs.</summary>
    private static CombatLogBuilder Sloppy(CombatLogBuilder log, string who)
        => log.Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(20.Seconds()).BossDebuffs("Rockjaw", with: Ability.PossessionBarrage, times: 4)
            .At(1.Minutes(30)).BossDebuffs(who, with: Ability.PossessionBarrage)
            .Wipe());

    /// <summary>The same attempt with the mechanic going where it belongs.</summary>
    private static CombatLogBuilder Clean(CombatLogBuilder log)
        => log.Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(20.Seconds()).BossDebuffs("Rockjaw", with: Ability.PossessionBarrage, times: 4)
            .Wipe());

    /// <summary>An attempt where one named person goes down before anybody else.</summary>
    private static CombatLogBuilder AnAttempt(CombatLogBuilder log, string first)
        => log.Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(1.Minutes()).Kills(first)
            .At(2.Minutes()).Kills("Emberwild")
            .Wipe());

    [Fact]
    public void Somebody_who_opens_most_of_the_night_is_a_finding_of_their_own()
    {
        var log = ARaid();
        for (int i = 0; i < 4; i++) AnAttempt(log, "Nightblade");
        AnAttempt(log, "Rockjaw");
        AnAttempt(log, "Sunwell");

        Given.IOpenedLog(log);

        Then.TheNightSays("Nightblade", "first to die in 4 of 6 attempts");
    }

    [Fact]
    public void A_night_where_it_was_a_different_person_each_time_says_nothing()
    {
        var log = ARaid();
        AnAttempt(log, "Nightblade");
        AnAttempt(log, "Rockjaw");
        AnAttempt(log, "Sunwell");
        AnAttempt(log, "Nightblade");
        AnAttempt(log, "Rockjaw");
        AnAttempt(log, "Sunwell");

        Given.IOpenedLog(log);

        Then.TheNightSaysNothing();
    }

    [Fact]
    public void What_somebody_led_is_counted_and_not_judged()
    {
        // The counterweight: a report that only ever accuses stops being opened. Held to the same
        // standard as the blame - a count, never a verdict.
        var log = ARaid();
        for (int i = 0; i < 6; i++)
        {
            log.Pull(Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(2.Minutes())
                .At(1.Minutes()).Deals("Nightblade", to: Soulcoiler, amount: 9_000_000)
                .At(1.Minutes()).Deals("Emberwild", to: Soulcoiler, amount: 1_000_000)
                .Wipe());
        }

        Given.IOpenedLog(log);

        Then.TheNightSays("Nightblade", "top damage in 6 of 6 attempts");
    }

    [Fact]
    public void Somebody_who_stopped_making_their_mistakes_is_the_good_news()
    {
        // Four in the first half and none in the second reads exactly like four spread evenly,
        // until somebody counts the halves. A report that cannot tell those apart discourages the
        // person who did the work.
        var log = ARaid();
        for (int i = 0; i < 5; i++) Sloppy(log, "Nightblade");
        for (int i = 0; i < 5; i++) Clean(log);

        Given.IOpenedLog(log);

        Then.TheNightSays("Nightblade", "5 mistakes in the first 5 attempts, none in the last 5");
    }

    [Fact]
    public void Mistakes_spread_through_the_evening_are_not_an_improvement()
    {
        var log = ARaid();
        for (int i = 0; i < 5; i++)
        {
            Sloppy(log, "Nightblade");
            Clean(log);
        }

        Given.IOpenedLog(log);

        Then.TheNightSaysNothing();
    }

    [Fact]
    public void A_handful_of_attempts_is_not_an_evening()
    {
        var log = ARaid();
        for (int i = 0; i < 4; i++) AnAttempt(log, "Nightblade");

        Given.IOpenedLog(log);

        Then.TheNightSaysNothing();
    }
}
