using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// What the app can say about talents, which is less than people expect and more than nothing.
///
/// The log carries the whole tree, so what was run is known exactly - and the numbers in it name
/// nothing a person would recognise and carry no spell. So the app never says which build is better
/// or what to pick. It says the two performed differently here, for you, and leaves the reading of
/// that to somebody who knows what they changed.
/// </summary>
public sealed class ChangingTheBuild : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue));

    /// <summary>Attempts where the rogue did a fixed amount of damage a second.</summary>
    private static CombatLogBuilder Attempts(CombatLogBuilder log, int times, long perAttempt)
        => log.Pulls(times, Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(2.Minutes())
            .At(1.Minutes()).Deals("Nightblade", to: Soulcoiler, amount: perAttempt)
            .Wipe());

    [Fact]
    public void A_talent_change_the_numbers_followed_is_reported()
    {
        // Four attempts at one build, four at another, and a third less damage on the second.
        var log = Attempts(ARaid(), 4, 12_000_000).Respecced("Nightblade");
        Attempts(log, 4, 8_000_000);

        Given.IOpenedLog(log);

        Then.TheBuildFindingReads("Nightblade", "changed talents, and your damage went down 33%")
            .And.TheBuildEvidenceReads("Nightblade",
                "100K a second over 4 attempts on the build before, 66.7K over 4 on this one");
    }

    [Fact]
    public void A_talent_change_that_moved_nothing_is_not_reported()
    {
        // The same change, the same numbers either side of it. Nothing to say.
        var log = Attempts(ARaid(), 4, 12_000_000).Respecced("Nightblade");
        Attempts(log, 4, 12_400_000);

        Given.IOpenedLog(log);

        Then.NothingWasSaidAboutTheBuild("Nightblade");
    }

    [Fact]
    public void A_couple_of_attempts_on_a_new_build_say_nothing_about_it()
    {
        // Two pulls after a respec is a sample of two, and a bad pull on a new build is a bad pull.
        var log = Attempts(ARaid(), 4, 12_000_000).Respecced("Nightblade");
        Attempts(log, 2, 6_000_000);

        Given.IOpenedLog(log);

        Then.NothingWasSaidAboutTheBuild("Nightblade");
    }

    [Fact]
    public void A_night_on_one_build_is_never_a_build_finding()
    {
        Given.IOpenedLog(Attempts(ARaid(), 8, 12_000_000));

        Then.NothingWasSaidAboutTheBuild("Nightblade");
    }

    [Fact]
    public void A_healer_is_measured_on_their_healing_rather_than_their_damage()
    {
        // A healer whose damage moved after a respec has told nobody anything.
        var log = ARaid()
            .Pulls(4, Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(2.Minutes())
                .At(1.Minutes()).Heals("Sunwell", target: "Rockjaw", amount: 12_000_000)
                .Wipe())
            .Respecced("Sunwell")
            .Pulls(4, Soulcoiler, Difficulty.Mythic, p => p
                .Lasting(2.Minutes())
                .At(1.Minutes()).Heals("Sunwell", target: "Rockjaw", amount: 6_000_000)
                .Wipe());

        Given.IOpenedLog(log);

        Then.TheBuildFindingReads("Sunwell", "changed talents, and your healing went down 50%");
    }
}
