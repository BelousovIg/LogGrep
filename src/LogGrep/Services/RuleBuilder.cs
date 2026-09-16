using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;

namespace LogGrep.Services;

/// <summary>What came of a build, in the terms somebody would ask about it.</summary>
public sealed record BuildReport(string Expansion, int Encounters, int Abilities, int WithRole, string Path)
{
    public string Summary => Abilities == 0
        ? $"{Expansion}: read {Encounters} encounters and found no abilities carrying a spell id. " +
          "The sample saved beside the rules file says what the journal actually returned."
        : $"{Expansion}: {Abilities} abilities from {Encounters} encounters, {WithRole} of them with a role.";
}

/// <summary>
/// Builds the rules file from Blizzard's encounter journal.
///
/// The shape of a section is known - id, title, body_text, and sections inside it - but whether a
/// spell id rides along, and whether abilities are grouped under a role, could not be checked
/// without a key. So the walk takes what it finds rather than insisting on a shape, the counts come
/// back in the report, and the first encounter is saved raw beside the rules: a disagreement with
/// reality is then one file away from being fixed rather than a mystery.
/// </summary>
public sealed class RuleBuilder
{
    private static readonly (string Word, string Role)[] RoleWords =
    {
        ("tank", "tank"),
        ("healer", "healer"),
        ("heal", "healer"),
        ("damage dealer", "damage"),
        ("dps", "damage"),
    };

    private readonly IFileSystem _fileSystem;
    private readonly IJournalSource _journal;

    public RuleBuilder(IFileSystem fileSystem, IJournalSource journal)
    {
        _fileSystem = fileSystem;
        _journal = journal;
    }

    /// <summary>Where the raw first encounter is kept, for when the parse and the journal disagree.</summary>
    public static string SamplePathFor(string rulesPath)
        => Path.ChangeExtension(rulesPath, null) + "-sample.json";

    public async Task<BuildReport> BuildAsync(string rulesPath, IProgress<string>? progress, CancellationToken ct)
    {
        var expansion = await NewestExpansionAsync(ct).ConfigureAwait(false);
        progress?.Report("Reading " + expansion.Name + "…");

        var instances = await InstancesOfAsync(expansion.Id, ct).ConfigureAwait(false);
        var abilities = new List<Ability>();
        int encounters = 0;
        bool sampled = false;

        foreach (var instance in instances)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(instance.Name + "…");

            foreach (var encounter in await EncountersOfAsync(instance.Id, ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();

                var body = await _journal.GetAsync("/data/wow/journal-encounter/" + encounter.Id, ct)
                    .ConfigureAwait(false);

                if (!sampled)
                {
                    Save(SamplePathFor(rulesPath), body.ToString());
                    sampled = true;
                }

                encounters++;
                if (body.TryGetProperty("sections", out var sections))
                {
                    Collect(sections, encounter.Name, role: null, abilities);
                }
            }
        }

        Save(rulesPath, Render(expansion.Name, abilities));

        return new BuildReport(
            expansion.Name, encounters, abilities.Count,
            abilities.Count(a => a.Role.Length > 0), rulesPath);
    }

    /// <summary>
    /// Walks the section tree, carrying down the nearest title that named a role. An ability is a
    /// section that turned out to have a spell id on it; anything else is prose on the way past.
    /// </summary>
    private static void Collect(JsonElement sections, string encounter, string? role, List<Ability> found)
    {
        if (sections.ValueKind != JsonValueKind.Array) return;

        foreach (var section in sections.EnumerateArray())
        {
            string title = Text(section, "title");
            string here = RoleIn(title) ?? role ?? string.Empty;

            if (SpellId(section) is { } id)
            {
                found.Add(new Ability(id, title.Length > 0 ? title : "Spell " + id, here,
                    encounter, Text(section, "body_text")));
            }

            if (section.TryGetProperty("sections", out var inner)) Collect(inner, encounter, here, found);
        }
    }

    private static int? SpellId(JsonElement section)
        => section.TryGetProperty("spell", out var spell) && spell.TryGetProperty("id", out var id)
           && id.TryGetInt32(out int value)
            ? value
            : null;

    private static string? RoleIn(string title)
    {
        foreach (var (word, role) in RoleWords)
        {
            if (title.Contains(word, StringComparison.OrdinalIgnoreCase)) return role;
        }

        return null;
    }

    private static string Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private async Task<(int Id, string Name)> NewestExpansionAsync(CancellationToken ct)
    {
        var index = await _journal.GetAsync("/data/wow/journal-expansion/index", ct).ConfigureAwait(false);
        if (!index.TryGetProperty("tiers", out var tiers) || tiers.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The expansion index did not list any tiers.");
        }

        var newest = tiers.EnumerateArray()
            .Select(t => (Id: t.GetProperty("id").GetInt32(), Name: Text(t, "name")))
            .OrderByDescending(t => t.Id)
            .FirstOrDefault();

        if (newest.Id == 0) throw new InvalidOperationException("The expansion index was empty.");
        return newest;
    }

    private async Task<List<(int Id, string Name)>> InstancesOfAsync(int expansion, CancellationToken ct)
    {
        var body = await _journal.GetAsync("/data/wow/journal-expansion/" + expansion, ct).ConfigureAwait(false);
        var instances = new List<(int, string)>();

        foreach (string group in new[] { "raids", "dungeons" })
        {
            if (!body.TryGetProperty(group, out var list) || list.ValueKind != JsonValueKind.Array) continue;

            instances.AddRange(list.EnumerateArray()
                .Select(i => (i.GetProperty("id").GetInt32(), Text(i, "name"))));
        }

        return instances;
    }

    private async Task<List<(int Id, string Name)>> EncountersOfAsync(int instance, CancellationToken ct)
    {
        var body = await _journal.GetAsync("/data/wow/journal-instance/" + instance, ct).ConfigureAwait(false);
        if (!body.TryGetProperty("encounters", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return new List<(int, string)>();
        }

        return list.EnumerateArray()
            .Select(e => (e.GetProperty("id").GetInt32(), Text(e, "name")))
            .ToList();
    }

    private void Save(string path, string content)
    {
        string? folder = _fileSystem.Path.GetDirectoryName(path);
        if (folder != null) _fileSystem.Directory.CreateDirectory(folder);
        _fileSystem.File.WriteAllText(path, content);
    }

    /// <summary>
    /// Plain text on purpose. Somebody with a correction should be able to open this in Notepad,
    /// and JSON is where a stray comma turns a correction into a broken file.
    /// </summary>
    private static string Render(string expansion, List<Ability> abilities)
    {
        var text = new StringBuilder();
        text.AppendLine("# Mechanics from Blizzard's encounter journal: " + expansion);
        text.AppendLine("# id, name, role. The indented lines are Blizzard's own description.");
        text.AppendLine("# Everything here is checked against the log, and muted where the two disagree.");

        foreach (var encounter in abilities.GroupBy(a => a.Encounter))
        {
            text.AppendLine();
            text.AppendLine("# " + encounter.Key);

            foreach (var ability in encounter.DistinctBy(a => a.SpellId).OrderBy(a => a.SpellId))
            {
                text.AppendLine($"{ability.SpellId}  {ability.Name}  {(ability.Role.Length > 0 ? ability.Role : "-")}");
                foreach (string line in Wrap(ability.Description)) text.AppendLine("    " + line);
            }
        }

        return text.ToString();
    }

    private static IEnumerable<string> Wrap(string text, int width = 96)
    {
        string flat = text.Replace("\r", " ").Replace("\n", " ").Trim();

        while (flat.Length > width)
        {
            int cut = flat.LastIndexOf(' ', width);
            if (cut <= 0) cut = width;

            yield return flat[..cut];
            flat = flat[(cut + 1)..].TrimStart();
        }

        if (flat.Length > 0) yield return flat;
    }

    private readonly record struct Ability(
        int SpellId, string Name, string Role, string Encounter, string Description);
}
