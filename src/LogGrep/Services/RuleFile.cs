using System.Globalization;
using System.IO.Abstractions;
using LogGrep.Models;

namespace LogGrep.Services;

/// <summary>
/// One ability as the journal describes it: which roles are told to care, and Blizzard's own
/// sentence about what to do. The roles are not a claim about who it lands on - an ability the
/// whole raid takes while the tank must react is flagged for tanks all the same.
/// </summary>
public sealed record WrittenRule(int SpellId, string Spell, IReadOnlyList<Role> Roles, string Advice);

/// <summary>
/// Reads the rules file.
///
/// Plain text on purpose, so a correction is a thing somebody does in Notepad. A line is an id, a
/// name and the roles, with the journal's own advice indented under it; anything unreadable is
/// skipped rather than fatal, because a file with one bad line in it is still worth the rest.
/// </summary>
public sealed class RuleFile
{
    private readonly IFileSystem _fileSystem;

    public RuleFile(IFileSystem fileSystem) => _fileSystem = fileSystem;

    public IReadOnlyList<WrittenRule> Read(string path)
    {
        if (!_fileSystem.File.Exists(path)) return Array.Empty<WrittenRule>();

        try
        {
            return Parse(_fileSystem.File.ReadAllLines(path));
        }
        catch (Exception)
        {
            // A rules file nobody can read costs the written half of the app, not the app.
            return Array.Empty<WrittenRule>();
        }
    }

    private static List<WrittenRule> Parse(IEnumerable<string> lines)
    {
        var rules = new List<WrittenRule>();
        int spellId = 0;
        string spell = string.Empty;
        var roles = new List<Role>();
        var advice = new List<string>();

        foreach (string line in lines)
        {
            if (line.StartsWith("#", StringComparison.Ordinal)) continue;

            // Indented lines are the advice belonging to the ability above them.
            if (line.StartsWith("    ", StringComparison.Ordinal))
            {
                if (spellId > 0) advice.Add(line.Trim());
                continue;
            }

            Close(rules, spellId, spell, roles, advice);
            spellId = 0;

            var parts = line.Split("  ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 3) continue;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out spellId)) continue;

            spell = parts[1];
            roles = Roles(parts[2]);
            advice = new List<string>();
        }

        Close(rules, spellId, spell, roles, advice);
        return rules;
    }

    private static void Close(List<WrittenRule> rules, int spellId, string spell,
        List<Role> roles, List<string> advice)
    {
        if (spellId > 0) rules.Add(new WrittenRule(spellId, spell, roles, string.Join(" ", advice)));
    }

    private static List<Role> Roles(string field)
    {
        var roles = new List<Role>();

        foreach (string name in field.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (name == "tank") roles.Add(Role.Tank);
            else if (name == "healer") roles.Add(Role.Healer);
            else if (name == "damage") roles.Add(Role.Damage);
        }

        return roles;
    }
}
