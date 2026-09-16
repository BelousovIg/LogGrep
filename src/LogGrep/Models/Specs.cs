namespace LogGrep.Models;

/// <summary>What a specialization does in a group. Mechanics are assigned by role, not by class.</summary>
public enum Role
{
    Damage,
    Tank,
    Healer,
}

/// <summary>
/// Maps the specialization ID reported by COMBATANT_INFO to a class, a spec name and a role.
/// The log never spells any of these out, so this table is the only way to get them.
/// IDs follow https://warcraft.wiki.gg/wiki/SpecializationID; the "Initial" entries are what a
/// character that has not picked a specialization yet reports.
/// </summary>
public static class Specs
{
    private static readonly Dictionary<int, (string Class, string Spec, Role Role)> Table = new()
    {
        [250] = ("Death Knight", "Blood", Role.Tank),
        [251] = ("Death Knight", "Frost", Role.Damage),
        [252] = ("Death Knight", "Unholy", Role.Damage),
        [1455] = ("Death Knight", "Initial", Role.Damage),
        [577] = ("Demon Hunter", "Havoc", Role.Damage),
        [581] = ("Demon Hunter", "Vengeance", Role.Tank),
        [1480] = ("Demon Hunter", "Devourer", Role.Damage),
        [1456] = ("Demon Hunter", "Initial", Role.Damage),
        [102] = ("Druid", "Balance", Role.Damage),
        [103] = ("Druid", "Feral", Role.Damage),
        [104] = ("Druid", "Guardian", Role.Tank),
        [105] = ("Druid", "Restoration", Role.Healer),
        [1447] = ("Druid", "Initial", Role.Damage),
        [1467] = ("Evoker", "Devastation", Role.Damage),
        [1468] = ("Evoker", "Preservation", Role.Healer),
        [1473] = ("Evoker", "Augmentation", Role.Damage),
        [1465] = ("Evoker", "Initial", Role.Damage),
        [253] = ("Hunter", "Beast Mastery", Role.Damage),
        [254] = ("Hunter", "Marksmanship", Role.Damage),
        [255] = ("Hunter", "Survival", Role.Damage),
        [1448] = ("Hunter", "Initial", Role.Damage),
        [62] = ("Mage", "Arcane", Role.Damage),
        [63] = ("Mage", "Fire", Role.Damage),
        [64] = ("Mage", "Frost", Role.Damage),
        [1449] = ("Mage", "Initial", Role.Damage),
        [268] = ("Monk", "Brewmaster", Role.Tank),
        [269] = ("Monk", "Windwalker", Role.Damage),
        [270] = ("Monk", "Mistweaver", Role.Healer),
        [1450] = ("Monk", "Initial", Role.Damage),
        [65] = ("Paladin", "Holy", Role.Healer),
        [66] = ("Paladin", "Protection", Role.Tank),
        [70] = ("Paladin", "Retribution", Role.Damage),
        [1451] = ("Paladin", "Initial", Role.Damage),
        [256] = ("Priest", "Discipline", Role.Healer),
        [257] = ("Priest", "Holy", Role.Healer),
        [258] = ("Priest", "Shadow", Role.Damage),
        [1452] = ("Priest", "Initial", Role.Damage),
        [259] = ("Rogue", "Assassination", Role.Damage),
        [260] = ("Rogue", "Outlaw", Role.Damage),
        [261] = ("Rogue", "Subtlety", Role.Damage),
        [1453] = ("Rogue", "Initial", Role.Damage),
        [262] = ("Shaman", "Elemental", Role.Damage),
        [263] = ("Shaman", "Enhancement", Role.Damage),
        [264] = ("Shaman", "Restoration", Role.Healer),
        [1444] = ("Shaman", "Initial", Role.Damage),
        [265] = ("Warlock", "Affliction", Role.Damage),
        [266] = ("Warlock", "Demonology", Role.Damage),
        [267] = ("Warlock", "Destruction", Role.Damage),
        [1454] = ("Warlock", "Initial", Role.Damage),
        [71] = ("Warrior", "Arms", Role.Damage),
        [72] = ("Warrior", "Fury", Role.Damage),
        [73] = ("Warrior", "Protection", Role.Tank),
        [1446] = ("Warrior", "Initial", Role.Damage),
    };

    /// <summary>
    /// Blizzard's class colours, the same ones the game and the log sites paint class names with.
    /// Keyed by class rather than by spec: every spec of a class shares one colour.
    /// </summary>
    private static readonly Dictionary<string, string> Colors = new(StringComparer.Ordinal)
    {
        ["Death Knight"] = "#C41E3A",
        ["Demon Hunter"] = "#A330C9",
        ["Druid"] = "#FF7C0A",
        ["Evoker"] = "#33937F",
        ["Hunter"] = "#AAD372",
        ["Mage"] = "#3FC7EB",
        ["Monk"] = "#00FF98",
        ["Paladin"] = "#F48CBA",
        ["Priest"] = "#FFFFFF",
        ["Rogue"] = "#FFF468",
        ["Shaman"] = "#0070DD",
        ["Warlock"] = "#8788EE",
        ["Warrior"] = "#C69B6D",
    };

    /// <summary>Empty when the log never said which spec the player was.</summary>
    public static string ColorOf(int specId)
        => Table.TryGetValue(specId, out var entry) && Colors.TryGetValue(entry.Class, out string? color)
            ? color
            : string.Empty;

    public static string ClassOf(int specId) => Table.TryGetValue(specId, out var entry) ? entry.Class : "—";

    /// <summary>An unknown ID is shown rather than hidden, so a spec added by a patch is obvious.</summary>
    public static string SpecOf(int specId) => Table.TryGetValue(specId, out var entry)
        ? entry.Spec
        : specId > 0 ? "Spec " + specId : "—";

    /// <summary>An unknown spec counts as damage, the role all but a dozen specs have.</summary>
    public static Role RoleOf(int specId) => Table.TryGetValue(specId, out var entry) ? entry.Role : Role.Damage;

    public static string NameOf(Role role) => role switch
    {
        Role.Tank => "tank",
        Role.Healer => "healer",
        _ => "damage",
    };

    /// <summary>
    /// The role as a person rather than as a label: "a tank", not "tank". "damage" is the odd one
    /// out - it works as an adjective in "damage mechanic" and not as a noun, hence the two forms.
    /// </summary>
    public static string PersonOf(Role role) => role switch
    {
        Role.Tank => "a tank",
        Role.Healer => "a healer",
        _ => "a damage dealer",
    };
}
