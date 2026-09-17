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
    public void A_handful_of_attempts_is_not_an_evening()
    {
        var log = ARaid();
        for (int i = 0; i < 4; i++) AnAttempt(log, "Nightblade");

        Given.IOpenedLog(log);

        Then.TheNightSaysNothing();
    }
}
