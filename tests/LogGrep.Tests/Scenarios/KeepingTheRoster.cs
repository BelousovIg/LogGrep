using LogGrep.Tests.Framework;
using LogGrep.Tests.Logs;
using static LogGrep.Tests.Logs.CombatLogBuilder;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// The registry of people, and the one thing about them the app is told rather than reads.
///
/// Everything here except the mark is read back out of the logs every time, so a row cannot show
/// last month's answer as though it were tonight's. The mark itself is filed under the identifier
/// the log gives a character, not under their name - because a name is exactly what a rename or a
/// realm transfer changes, and a list keyed on one would quietly lose somebody the week they moved.
/// </summary>
public sealed class KeepingTheRoster : Scenario
{
    private const Boss Soulcoiler = Boss.TheSoulcoiler;

    private static void APull(PullBuilder p) => p
        .Lasting(2.Minutes())
        .At(0.Seconds()).Deals("Rockjaw", to: Soulcoiler, amount: 100_000)
        .At(2.Seconds()).BossSwingsAt("Rockjaw", 200_000)
        .Wipe();

    [Fact]
    public void Everybody_the_logs_have_seen_is_listed_once()
    {
        var log = new CombatLogBuilder()
            .Raid(
                Tank("Rockjaw", Spec.ProtectionWarrior),
                Healer("Sunwell", Spec.HolyPriest),
                Damage("Nightblade", Spec.AssassinationRogue))
            .Pulls(3, Soulcoiler, Difficulty.Mythic, APull);

        Given.IOpenedLog(log);

        // The list is sorted by attempts, and a tie falls back to the name.
        Then.PeopleAreListed("Nightblade", "Rockjaw", "Sunwell")
            .PersonWasInPulls("Rockjaw", "3");
    }

    [Fact]
    public void Somebody_who_respecced_is_one_person_who_did_two_jobs()
    {
        // Not two rows, and not one row that has to pick. What they were is a property of each
        // attempt, and the registry says which jobs those attempts amounted to.
        var log = new CombatLogBuilder()
            .Raid(
                Tank("Rockjaw", Spec.ProtectionWarrior),
                Healer("Sunwell", Spec.HolyPriest),
                Damage("Mokhgar", Spec.AssassinationRogue))
            .Pulls(3, Soulcoiler, Difficulty.Mythic, APull)
            .Raid(
                Tank("Rockjaw", Spec.ProtectionWarrior),
                Healer("Sunwell", Spec.HolyPriest),
                Tank("Mokhgar", Spec.BloodDeathKnight))
            .Pull(Soulcoiler, Difficulty.Mythic, APull);

        Given.IOpenedLog(log);

        Then.PersonWasInPulls("Mokhgar", "4")
            .PersonPlayed("Mokhgar", "damage, tank");
    }

    [Fact]
    public void A_character_who_moved_realm_is_still_one_character()
    {
        // Same identifier, different name and realm. The log carries an identifier a transfer does
        // not touch, and that is the whole reason the registry is keyed on it.
        var before = new CombatLogBuilder()
            .Called("first.txt")
            .Raid(
                Tank("Rockjaw", Spec.ProtectionWarrior),
                Healer("Sunwell", Spec.HolyPriest),
                Damage("Nightblade", Spec.AssassinationRogue))
            .On(new DateTime(2026, 9, 10, 20, 0, 0))
            .Pulls(2, Soulcoiler, Difficulty.Mythic, APull);

        var after = new CombatLogBuilder()
            .Called("second.txt")
            .Raid(
                Tank("Rockjaw", Spec.ProtectionWarrior),
                Healer("Sunwell", Spec.HolyPriest),
                Damage("Nightblade", Spec.AssassinationRogue) with
                {
                    Name = "Shadowblade", Realm = "TarrenMill", Identity = "Nightblade",
                })
            .On(new DateTime(2026, 9, 17, 20, 0, 0))
            .Pulls(2, Soulcoiler, Difficulty.Mythic, APull);

        Given.IOpenedLogs(before, after);

        // One row, four attempts, and called by the name they answer to now.
        Then.PeopleAreListed("Rockjaw", "Shadowblade", "Sunwell")
            .PersonWasInPulls("Shadowblade", "4")
            .PersonIsOn("Shadowblade", "Tarren Mill");
    }

    [Fact]
    public void An_untouched_registry_means_everybody_counts()
    {
        // Marking nobody is not the same answer as saying nobody is ours. Until the question has
        // been asked, an analysis is about the whole group, and the screen says so rather than
        // coming up empty and leaving somebody to guess why.
        var log = new CombatLogBuilder()
            .Raid(
                Tank("Rockjaw", Spec.ProtectionWarrior),
                Healer("Sunwell", Spec.HolyPriest),
                Damage("Nightblade", Spec.AssassinationRogue))
            .Pulls(2, Soulcoiler, Difficulty.Mythic, APull);

        Given.IOpenedLog(log);

        Then.OursAre("Rockjaw", "Sunwell", "Nightblade")
            .TheRegistrySays("3 characters, none marked - all of them count as ours");
    }

    [Fact]
    public void Marking_somebody_narrows_it_to_those_who_are_marked()
    {
        var log = new CombatLogBuilder()
            .Raid(
                Tank("Rockjaw", Spec.ProtectionWarrior),
                Healer("Sunwell", Spec.HolyPriest),
                Damage("Nightblade", Spec.AssassinationRogue))
            .Pulls(2, Soulcoiler, Difficulty.Mythic, APull);

        Given.IOpenedLog(log);

        When.IMarkAsOurs("Rockjaw").IMarkAsOurs("Sunwell");

        Then.OursAre("Rockjaw", "Sunwell")
            .TheRegistrySays("2 of 3 marked as ours");
    }

    [Fact]
    public void The_marks_outlive_the_session()
    {
        var log = new CombatLogBuilder()
            .Raid(
                Tank("Rockjaw", Spec.ProtectionWarrior),
                Healer("Sunwell", Spec.HolyPriest),
                Damage("Nightblade", Spec.AssassinationRogue))
            .Pulls(2, Soulcoiler, Difficulty.Mythic, APull);

        Given.IOpenedLog(log);

        When.IMarkAsOurs("Rockjaw").IReopenTheApp();

        Then.PersonIsOurs("Rockjaw", true)
            .PersonIsOurs("Sunwell", false);
    }
}
