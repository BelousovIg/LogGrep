using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LogGrep.Services;

/// <summary>What came of a build, in the terms somebody would ask about it.</summary>
public sealed record BuildReport(string Expansion, int Encounters, int Abilities, int WithRole, string Path)
{
    public string Summary => Abilities == 0
        ? $"{Expansion}: read {Encounters} encounters and found no abilities. The saved journal says what arrived."
        : $"{Expansion}: {Abilities} abilities from {Encounters} encounters, {WithRole} of them with a role.";
}

/// <summary>
/// Turns the saved journal into the rules file. It reads from disk and never from the network, so
/// the parse can be changed and the rules rebuilt as often as it takes.
///
/// The journal keeps two things apart. Abilities are sections carrying a spell id, titled by name.
/// Roles are prose: sections called Tanks, Healers or Damage Dealers, whose text names abilities in
/// square brackets. So the role is recovered by reading those sentences, which is also where the
/// advice comes from - and note that an ability turns up under several roles, because the journal
/// is saying who should care rather than who it lands on. Which of the two it is, the log decides.
/// </summary>
public sealed class RuleBuilder
{
    private static readonly Regex RoleTitle = new(@"^(tanks?|healers?|damage dealers?|dps)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Mention = new(@"\[([^\]]+)\]", RegexOptions.Compiled);

    private readonly IFileSystem _fileSystem;

    public RuleBuilder(IFileSystem fileSystem) => _fileSystem = fileSystem;

    /// <summary>Takes the encounters as they arrived, whether from the network or from disk.</summary>
    public BuildReport Build(string expansion, IEnumerable<JsonElement> bodies, string rulesPath)
    {
        var encounters = new List<Encounter>();

        foreach (var body in bodies)
        {
            var encounter = Read(body);
            if (encounter.Abilities.Count > 0) encounters.Add(encounter);
        }

        Save(rulesPath, Render(expansion, encounters));

        var all = encounters.SelectMany(e => e.Abilities.Values).ToList();
        return new BuildReport(expansion, encounters.Count, all.Count,
            all.Count(a => a.Roles.Count > 0), rulesPath);
    }

    private static Encounter Read(JsonElement body)
    {
        var encounter = new Encounter(Journal.Text(body, "name"));
        if (!body.TryGetProperty("sections", out var sections)) return encounter;

        Collect(sections, encounter);
        Attribute(sections, encounter);
        return encounter;
    }

    /// <summary>Anything carrying a spell id is an ability, however deep it sits.</summary>
    private static void Collect(JsonElement sections, Encounter encounter)
    {
        if (sections.ValueKind != JsonValueKind.Array) return;

        foreach (var section in sections.EnumerateArray())
        {
            if (section.TryGetProperty("spell", out var spell)
                && spell.TryGetProperty("id", out var id)
                && id.TryGetInt32(out int spellId))
            {
                string name = Journal.Text(section, "title");
                if (name.Length == 0) name = Journal.Text(spell, "name");
                encounter.Abilities.TryAdd(spellId, new Ability(spellId, name));
            }

            if (section.TryGetProperty("sections", out var inner)) Collect(inner, encounter);
        }
    }

    /// <summary>
    /// Reads the role sections and hands each mentioned ability that role, along with the sentence
    /// it was mentioned in - which is the closest thing to advice anybody gets for free.
    /// </summary>
    private static void Attribute(JsonElement sections, Encounter encounter)
    {
        if (sections.ValueKind != JsonValueKind.Array) return;

        foreach (var section in sections.EnumerateArray())
        {
            string title = Journal.Text(section, "title").Trim();
            if (RoleTitle.IsMatch(title)) Mentions(Journal.Text(section, "body_text"), Role(title), encounter);

            if (section.TryGetProperty("sections", out var inner)) Attribute(inner, encounter);
        }
    }

    private static void Mentions(string body, string role, Encounter encounter)
    {
        foreach (string sentence in body.Split("$bullet;", StringSplitOptions.RemoveEmptyEntries))
        {
            string clean = Tidy(sentence);
            if (clean.Length == 0) continue;

            foreach (Match mention in Mention.Matches(sentence))
            {
                var ability = encounter.Abilities.Values
                    .FirstOrDefault(a => string.Equals(a.Name, mention.Groups[1].Value,
                        StringComparison.OrdinalIgnoreCase));

                if (ability == null) continue;

                ability.Roles.Add(role);
                if (!ability.Notes.Contains(clean, StringComparer.Ordinal)) ability.Notes.Add(clean);
            }
        }
    }

    private static string Role(string title)
    {
        string lower = title.ToLowerInvariant();
        if (lower.StartsWith("tank", StringComparison.Ordinal)) return "tank";
        return lower.StartsWith("healer", StringComparison.Ordinal) ? "healer" : "damage";
    }

    /// <summary>Strips the brackets the journal marks ability names with, and squeezes the spacing.</summary>
    private static string Tidy(string sentence)
        => Regex.Replace(Mention.Replace(sentence, "$1"), @"\s+", " ").Trim();

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
    private static string Render(string expansion, List<Encounter> encounters)
    {
        var text = new StringBuilder();
        text.AppendLine("# Mechanics from Blizzard's encounter journal: " + expansion);
        text.AppendLine("# id, name, and the roles the journal says should care - not who it lands on.");
        text.AppendLine("# Which of the two it is, the log decides; where the two disagree, the log wins.");

        foreach (var encounter in encounters.OrderBy(e => e.Name, StringComparer.Ordinal))
        {
            text.AppendLine();
            text.AppendLine("# " + encounter.Name);

            foreach (var ability in encounter.Abilities.Values.OrderBy(a => a.SpellId))
            {
                string roles = ability.Roles.Count > 0 ? string.Join(",", ability.Roles) : "-";
                text.AppendLine($"{ability.SpellId}  {ability.Name}  {roles}");

                foreach (string note in ability.Notes)
                {
                    foreach (string line in Wrap(note)) text.AppendLine("    " + line);
                }
            }
        }

        return text.ToString();
    }

    private static IEnumerable<string> Wrap(string text, int width = 96)
    {
        string flat = text;

        while (flat.Length > width)
        {
            int cut = flat.LastIndexOf(' ', width);
            if (cut <= 0) cut = width;

            yield return flat[..cut];
            flat = flat[(cut + 1)..].TrimStart();
        }

        if (flat.Length > 0) yield return flat;
    }

    private sealed class Encounter
    {
        public Encounter(string name) => Name = name;

        public string Name { get; }

        public Dictionary<int, Ability> Abilities { get; } = new();
    }

    private sealed class Ability
    {
        public Ability(int spellId, string name)
        {
            SpellId = spellId;
            Name = name;
        }

        public int SpellId { get; }

        public string Name { get; }

        /// <summary>Sorted so that "damage,tank" reads the same way every time it is written.</summary>
        public SortedSet<string> Roles { get; } = new(StringComparer.Ordinal);

        public List<string> Notes { get; } = new();
    }
}
