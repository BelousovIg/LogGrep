using LogGrep.Analysis;
using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// What a person is compared against when they did not spend the evening doing one job.
///
/// Every baseline in the app is drawn from somebody's own attempts, and a night with a respec in it
/// has two different somebodies in one name. Pooled, the numbers describe nobody: they would tell a
/// tank their healing was down and a healer their damage was up, in the same breath, about the same
/// evening. So the unit of comparison is the pair of person and specialization, and everything
/// built on a baseline follows.
/// </summary>
public sealed class ComparingLikeWithLike : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static CombatLogBuilder AsRogue() => new CombatLogBuilder().Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Damage("Mokhgar", Spec.AssassinationRogue));

    private static CombatLogBuilder Retanked(CombatLogBuilder log) => log.Raid(
        Tank("Rockjaw", Spec.ProtectionWarrior),
        Healer("Sunwell", Spec.HolyPriest),
        Tank("Mokhgar", Spec.BloodDeathKnight));

    private static void APull(PullBuilder p) => p
        .Lasting(2.Minutes())
        .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
        .At(2.Seconds()).BossSwingsAt("Rockjaw", 200_000)
        .At(1.Minutes()).Deals("Mokhgar", to: Soulcoiler, amount: 900_000)
        .Wipe();

    [Fact]
    public void Two_specialisations_do_not_add_up_to_one_run_of_attempts()
    {
        // Four attempts as a rogue and four as a death knight. Counted by name that is eight, and
        // enough to know somebody's best; counted honestly it is four of one job and four of
        // another, and neither is a run yet. The cell says so rather than averaging the two.
        var log = AsRogue().Pulls(4, Soulcoiler, Difficulty.Mythic, APull);
        Retanked(log).Pulls(4, Soulcoiler, Difficulty.Mythic, APull);

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Mokhgar");

        Then.PlayerScores(Axis.Output, "—")
            .PlayerScoreSays(Axis.Output, "not enough attempts yet to know your best");
    }

    [Fact]
    public void One_job_all_evening_is_still_a_run_of_attempts()
    {
        // The same eight attempts without the respec. The point of the rule is that it splits a
        // night that was two jobs, not that it refuses to measure a night that was one.
        var log = AsRogue().Pulls(8, Soulcoiler, Difficulty.Mythic, APull);

        Given.IOpenedLog(log).IOpenedPull(Soulcoiler, number: 1);

        When.ILookAtPlayer("Mokhgar");

        Then.PlayerScores(Axis.Output, "100%")
            .PlayerScoreSays(Axis.Output, "your best of 8 attempts");
    }

    [Fact]
    public void Giving_up_a_job_is_not_the_same_as_fixing_a_habit()
    {
        // Five attempts of standing in something, then a respec and five clean ones. The mistakes
        // stopped because the job did, and congratulating somebody for that is congratulating the
        // wrong person for the wrong thing.
        var log = new CombatLogBuilder().Raid(
            Tank("Rockjaw", Spec.ProtectionWarrior),
            Healer("Sunwell", Spec.HolyPriest),
            Damage("Nightblade", Spec.AssassinationRogue),
            Damage("Emberwild", Spec.ArcaneMage));

        for (int i = 0; i < 5; i++) Sloppy(log, "Nightblade");

        log.Raid(
            Tank("Rockjaw", Spec.ProtectionWarrior),
            Healer("Sunwell", Spec.HolyPriest),
            Tank("Nightblade", Spec.BloodDeathKnight),
            Damage("Emberwild", Spec.ArcaneMage));

        for (int i = 0; i < 5; i++) Clean(log);

        Given.IOpenedLog(log);

        Then.TheNightDoesNotSay("Nightblade", "in the first");
    }

    private static CombatLogBuilder Sloppy(CombatLogBuilder log, string who)
        => log.Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(20.Seconds()).BossDebuffs("Rockjaw", with: Ability.PossessionBarrage, times: 7)
            .At(1.Minutes(30)).BossDebuffs(who, with: Ability.PossessionBarrage)
            .Wipe());

    private static CombatLogBuilder Clean(CombatLogBuilder log)
        => log.Pull(Soulcoiler, Difficulty.Mythic, p => p
            .Lasting(3.Minutes())
            .At(20.Seconds()).BossDebuffs("Rockjaw", with: Ability.PossessionBarrage, times: 7)
            .Wipe());
}
