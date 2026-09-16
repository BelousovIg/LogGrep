using System.IO.Abstractions.TestingHelpers;
using LogGrep.Services;
using LogGrep.Tests.Logs;

namespace LogGrep.Tests.Scenarios;

/// <summary>
/// Turning Blizzard's encounter journal into the rules file. The journal here is written by the
/// test: the real one needs a key, and nothing in this suite goes near the network.
///
/// A caveat worth keeping in view - the shape of these pages is what the journal is *believed* to
/// return, drawn from a published model and a forum thread rather than from a response anybody
/// here has seen. That is why the builder saves its first encounter raw: when the belief and the
/// journal differ, the difference is in a file rather than in a guess.
/// </summary>
public sealed class BuildingTheRules
{
    private const string Rules = @"C:\fake\LogGrep\rules.txt";

    private static AJournal AJournalWith(string encounter) => new AJournal()
        .Page("/data/wow/journal-expansion/index",
            """{ "tiers": [ { "id": 68, "name": "Legion" }, { "id": 505, "name": "Midnight" } ] }""")
        .Page("/data/wow/journal-expansion/505",
            """{ "raids": [ { "id": 1300, "name": "The Sunken Vault" } ], "dungeons": [] }""")
        .Page("/data/wow/journal-instance/1300",
            """{ "encounters": [ { "id": 2600, "name": "The Soulcoiler" } ] }""")
        .Page("/data/wow/journal-encounter/2600", encounter);

    private const string TwoAbilitiesUnderRoles = """
        {
          "id": 2600,
          "name": "The Soulcoiler",
          "sections": [
            { "id": 1, "title": "Overview", "body_text": "The fight runs in three phases." },
            { "id": 2, "title": "Tank", "sections": [
                { "id": 3, "title": "Possession Barrage", "body_text": "Marks the current tank.",
                  "spell": { "id": 1284103, "name": "Possession Barrage" } } ] },
            { "id": 4, "title": "Healer", "sections": [
                { "id": 5, "title": "Creeping Rot", "body_text": "Rot spreads between players.",
                  "spell": { "id": 1284491, "name": "Creeping Rot" } } ] }
          ]
        }
        """;

    [Fact]
    public async Task The_newest_expansion_is_the_one_that_gets_read()
    {
        var disk = new MockFileSystem();

        var report = await new RuleBuilder(disk, AJournalWith(TwoAbilitiesUnderRoles))
            .BuildAsync(Rules, null, CancellationToken.None);

        Assert.Equal("Midnight", report.Expansion);
        Assert.Equal(1, report.Encounters);
    }

    [Fact]
    public async Task An_ability_takes_the_role_of_the_section_it_sits_under()
    {
        var disk = new MockFileSystem();

        await new RuleBuilder(disk, AJournalWith(TwoAbilitiesUnderRoles))
            .BuildAsync(Rules, null, CancellationToken.None);

        string written = disk.File.ReadAllText(Rules);

        Assert.Contains("1284103  Possession Barrage  tank", written, StringComparison.Ordinal);
        Assert.Contains("1284491  Creeping Rot  healer", written, StringComparison.Ordinal);
        Assert.Contains("# The Soulcoiler", written, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Blizzards_own_description_is_carried_across()
    {
        var disk = new MockFileSystem();

        await new RuleBuilder(disk, AJournalWith(TwoAbilitiesUnderRoles))
            .BuildAsync(Rules, null, CancellationToken.None);

        Assert.Contains("    Marks the current tank.", disk.File.ReadAllText(Rules), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Prose_with_no_spell_behind_it_is_not_a_rule()
    {
        var disk = new MockFileSystem();

        var report = await new RuleBuilder(disk, AJournalWith(TwoAbilitiesUnderRoles))
            .BuildAsync(Rules, null, CancellationToken.None);

        // Three sections carry text; only the two with a spell id are abilities.
        Assert.Equal(2, report.Abilities);
        Assert.DoesNotContain("Overview", disk.File.ReadAllText(Rules), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_ability_under_no_role_is_kept_and_marked_as_such()
    {
        const string unsorted = """
            { "sections": [
                { "id": 1, "title": "Shadow Bolt", "body_text": "Hits somebody.",
                  "spell": { "id": 999, "name": "Shadow Bolt" } } ] }
            """;

        var disk = new MockFileSystem();

        var report = await new RuleBuilder(disk, AJournalWith(unsorted)).BuildAsync(Rules, null, CancellationToken.None);

        Assert.Equal(1, report.Abilities);
        Assert.Equal(0, report.WithRole);
        Assert.Contains("999  Shadow Bolt  -", disk.File.ReadAllText(Rules), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_first_encounter_is_kept_raw_so_a_wrong_guess_can_be_seen()
    {
        var disk = new MockFileSystem();

        await new RuleBuilder(disk, AJournalWith(TwoAbilitiesUnderRoles))
            .BuildAsync(Rules, null, CancellationToken.None);

        string sample = RuleBuilder.SamplePathFor(Rules);

        Assert.True(disk.File.Exists(sample), "The raw encounter should have been saved to " + sample);
        Assert.Contains("1284103", disk.File.ReadAllText(sample), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_journal_that_names_no_spells_at_all_says_so_rather_than_writing_nothing()
    {
        const string prose = """{ "sections": [ { "id": 1, "title": "Overview", "body_text": "Words." } ] }""";

        var disk = new MockFileSystem();

        var report = await new RuleBuilder(disk, AJournalWith(prose)).BuildAsync(Rules, null, CancellationToken.None);

        Assert.Equal(0, report.Abilities);
        Assert.Contains("no abilities carrying a spell id", report.Summary, StringComparison.Ordinal);
        Assert.Contains("sample", report.Summary, StringComparison.Ordinal);
    }
}
