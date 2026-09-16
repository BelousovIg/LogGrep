using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using LogGrep.Services;
using LogGrep.Tests.Logs;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// Turning Blizzard's encounter journal into the rules file. The journal here is written by the
/// test, in the shape a real response turned out to have: abilities are sections carrying a spell
/// id, and the roles live in prose under Overview, naming abilities in square brackets.
///
/// Nothing in this suite reaches the network. Fetching needs a key, and a key belongs to whoever
/// registered it.
/// </summary>
public sealed class BuildingTheRules
{
    private const string Folder = @"C:\fake\LogGrep\journal";
    private const string Rules = @"C:\fake\LogGrep\rules.txt";

    private const string LuAshal = """
        {
          "id": 2600,
          "name": "Lu'ashal",
          "sections": [
            { "id": 1, "title": "Overview", "body_text": "Lu'ashal assails foes in a holy frenzy.",
              "sections": [
                { "id": 2, "title": "Tanks",
                  "body_text": "$bullet; [Dawnfire Breath] inflicts heavy damage in a cone at the primary target." },
                { "id": 3, "title": "Healers",
                  "body_text": "$bullet; [Dawncrazed Halo] inflicts moderate damage around afflicted players." },
                { "id": 4, "title": "Damage Dealers",
                  "body_text": "$bullet; [Dawncrazed Halo] inflicts moderate damage around afflicted players." }
              ] },
            { "id": 5, "title": "Radiant Flare", "spell": { "id": 1258427, "name": "Radiant Flare" },
              "sections": [
                { "id": 6, "title": "Radiant Ember", "spell": { "id": 1258426, "name": "Radiant Ember" } }
              ] },
            { "id": 7, "title": "Dawncrazed Halo", "spell": { "id": 1276436, "name": "Dawncrazed Halo" } },
            { "id": 8, "title": "Dawnfire Breath", "spell": { "id": 1276247, "name": "Dawnfire Breath" } }
          ]
        }
        """;

    private static AJournal AJournalWith(string encounter) => new AJournal()
        .Page("/data/wow/journal-expansion/index",
            """{ "tiers": [ { "id": 68, "name": "Legion" }, { "id": 505, "name": "Midnight" } ] }""")
        .Page("/data/wow/journal-expansion/505",
            """{ "raids": [ { "id": 1300, "name": "The Sunken Vault" } ], "dungeons": [] }""")
        .Page("/data/wow/journal-instance/1300",
            """{ "encounters": [ { "id": 2600, "name": "Lu'ashal" } ] }""")
        .Page("/data/wow/journal-encounter/2600", encounter);

    private static async Task<(MockFileSystem Disk, string Expansion, List<JsonElement> Encounters)> AFetch(
        string encounter = LuAshal, bool keepRaw = true)
    {
        var disk = new MockFileSystem();
        var (expansion, encounters) = await new JournalCache(disk, Folder, keepRaw)
            .FetchAsync(AJournalWith(encounter), null, CancellationToken.None);

        return (disk, expansion, encounters);
    }

    private static async Task<string> RulesFrom(string encounter = LuAshal)
    {
        var (disk, expansion, encounters) = await AFetch(encounter);
        new RuleBuilder(disk).Build(expansion, encounters, Rules);
        return disk.File.ReadAllText(Rules);
    }

    [Fact]
    public async Task The_newest_expansion_is_the_one_that_gets_fetched()
    {
        var (disk, expansion, encounters) = await AFetch();

        Assert.Equal("Midnight", expansion);
        Assert.Single(encounters);
        Assert.True(disk.File.Exists(@"C:\fake\LogGrep\journal\2600.json"));
    }

    [Fact]
    public async Task A_build_that_keeps_nothing_leaves_nothing_behind()
    {
        // What a release build does: the rules come out, the raw journal does not stay.
        var (disk, expansion, encounters) = await AFetch(keepRaw: false);

        var report = new RuleBuilder(disk).Build(expansion, encounters, Rules);

        Assert.Equal(4, report.Abilities);
        Assert.False(disk.Directory.Exists(Folder), "A release build should not have kept the journal.");
    }

    [Fact]
    public async Task What_was_kept_is_enough_to_rebuild_from_without_asking_again()
    {
        var (disk, _, _) = await AFetch();

        // Nothing is handed to the builder but what is on disk.
        var cache = new JournalCache(disk, Folder, keepRaw: true);
        var report = new RuleBuilder(disk).Build(cache.Expansion, cache.Saved(), Rules);

        Assert.Equal("Midnight", report.Expansion);
        Assert.Equal(4, report.Abilities);
    }

    [Fact]
    public async Task An_ability_nested_under_another_is_still_found()
        => Assert.Contains("1258426  Radiant Ember", await RulesFrom(), StringComparison.Ordinal);

    [Fact]
    public async Task An_ability_the_tank_section_names_is_a_tank_mechanic()
        => Assert.Contains("1276247  Dawnfire Breath  tank", await RulesFrom(), StringComparison.Ordinal);

    [Fact]
    public async Task An_ability_two_sections_name_carries_both_roles()
    {
        // The journal says who should care, not who it lands on - so both is a real answer.
        Assert.Contains("1276436  Dawncrazed Halo  damage,healer", await RulesFrom(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_sentence_it_was_named_in_comes_across_without_the_brackets()
    {
        string rules = await RulesFrom();

        Assert.Contains("Dawnfire Breath inflicts heavy damage in a cone at the primary target.", rules,
            StringComparison.Ordinal);
        Assert.DoesNotContain("$bullet;", rules, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_ability_nobody_mentions_is_kept_with_no_role()
    {
        var (disk, expansion, encounters) = await AFetch();

        var report = new RuleBuilder(disk).Build(expansion, encounters, Rules);

        Assert.Equal(4, report.Abilities);
        Assert.Equal(2, report.WithRole);
        Assert.Contains("1258427  Radiant Flare  -", disk.File.ReadAllText(Rules), StringComparison.Ordinal);
    }
}
