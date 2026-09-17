using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// Whether a fight has a number of stacks it stops forgiving, and whether the app can find it
/// without being told.
///
/// The honest answer is usually silence. On the log this was built against, people died holding
/// three of something and lived holding twenty of it - so stacks are not what kills there, and a
/// rule that spoke anyway would be inventing a threshold.
/// </summary>
public sealed class CountingStacks : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;
    private const Ability Rot = Ability.CreepingRot;

    private static CombatLogBuilder ARaid() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Nightblade", Spec.AssassinationRogue),
        Damage("Emberwild", Spec.ArcaneMage),
        Damage("Moonfire", Spec.BalanceDruid),
        Damage("Frostbite", Spec.FrostDeathKnight));

    /// <summary>Somebody stacks it to a count and then either dies of it or does not.</summary>
    private static CombatLogBuilder AnAttempt(CombatLogBuilder log, string who, int stacks, bool dies)
        => log.Pull(Soulcoiler, Difficulty.Mythic, p =>
        {
            p.Lasting(3.Minutes())
             .At(30.Seconds()).BossDebuffs(who, with: Rot)
             .At(30.Seconds()).BossStacks(who, with: Rot, to: stacks);

            // Five seconds after the last application, so the death follows the peak rather than
            // happening at some unrelated point later on.
            if (dies) p.At(30.Seconds() + (stacks - 1).Seconds() + 5.Seconds()).Kills(who, with: Rot);
            p.Wipe();
        });

    [Fact]
    public void A_count_everybody_dies_past_and_nobody_dies_under_is_the_fights_tolerance()
    {
        // Three died at eight and above, three lived at five and below. The line is at eight, and
        // nothing had to be told what Creeping Rot is.
        var log = ARaid();
        AnAttempt(log, "Nightblade", 8, dies: true);
        AnAttempt(log, "Emberwild", 9, dies: true);
        AnAttempt(log, "Moonfire", 10, dies: true);
        AnAttempt(log, "Frostbite", 5, dies: false);
        AnAttempt(log, "Nightblade", 4, dies: false);
        AnAttempt(log, "Emberwild", 3, dies: false);

        Given.IOpenedLog(log);

        Then.TheStackFindingReads("Nightblade", "carried 8 stacks of Creeping Rot")
            .And.TheStackEvidenceReads("Nightblade",
                "everybody who reached 8 died within ten seconds; nobody who stopped at 5 did");
    }

    [Fact]
    public void Stacks_that_do_not_separate_the_dead_from_the_living_say_nothing()
    {
        // Somebody died at three and somebody lived at twenty. Whatever kills on this fight, it is
        // not the stack count, and the app has nothing to say about it.
        var log = ARaid();
        AnAttempt(log, "Nightblade", 3, dies: true);
        AnAttempt(log, "Emberwild", 12, dies: true);
        AnAttempt(log, "Moonfire", 6, dies: true);
        AnAttempt(log, "Frostbite", 20, dies: false);
        AnAttempt(log, "Nightblade", 8, dies: false);
        AnAttempt(log, "Emberwild", 4, dies: false);

        Given.IOpenedLog(log);

        Then.NothingWasSaidAboutStacks();
    }

    [Fact]
    public void Two_deaths_and_two_survivals_are_not_a_line()
    {
        // A clean split, and four data points behind it. That is a coincidence with a shape.
        var log = ARaid();
        AnAttempt(log, "Nightblade", 9, dies: true);
        AnAttempt(log, "Emberwild", 10, dies: true);
        AnAttempt(log, "Frostbite", 4, dies: false);
        AnAttempt(log, "Moonfire", 3, dies: false);

        Given.IOpenedLog(log);

        Then.NothingWasSaidAboutStacks();
    }

    [Fact]
    public void A_death_long_after_the_peak_does_not_count_as_dying_of_it()
    {
        // The stacks topped out and the player lived another minute. Whatever killed them, the
        // debuff had stopped being the story.
        var log = ARaid();
        log.Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(30.Seconds()).BossDebuffs("Nightblade", with: Rot)
            .At(30.Seconds()).BossStacks("Nightblade", with: Rot, to: 9)
            .At(2.Minutes(30)).Kills("Nightblade")
            .Wipe());
        AnAttempt(log, "Emberwild", 8, dies: true);
        AnAttempt(log, "Moonfire", 10, dies: true);
        AnAttempt(log, "Frostbite", 11, dies: true);
        AnAttempt(log, "Nightblade", 4, dies: false);
        AnAttempt(log, "Emberwild", 3, dies: false);

        Given.IOpenedLog(log);

        // Without the late death this would separate cleanly at eight. Counting it as a survival -
        // which is what it was, a minute and a half later - puts a nine among the living and there
        // is no line left to draw.
        Then.NothingWasSaidAboutStacks();
    }
}
