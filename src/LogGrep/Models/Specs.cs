namespace LogGrep.Models;

/// <summary>
/// Maps the specialization ID reported by COMBATANT_INFO to a class and a spec name.
/// The log never spells the class out, so this table is the only way to get one.
/// </summary>
public static class Specs
{
    private static readonly Dictionary<int, (string Class, string Spec)> Table = new()
    {
        [250] = ("Death Knight", "Blood"),
        [251] = ("Death Knight", "Frost"),
        [252] = ("Death Knight", "Unholy"),
        [577] = ("Demon Hunter", "Havoc"),
        [581] = ("Demon Hunter", "Vengeance"),
        [102] = ("Druid", "Balance"),
        [103] = ("Druid", "Feral"),
        [104] = ("Druid", "Guardian"),
        [105] = ("Druid", "Restoration"),
        [1467] = ("Evoker", "Devastation"),
        [1468] = ("Evoker", "Preservation"),
        [1473] = ("Evoker", "Augmentation"),
        [253] = ("Hunter", "Beast Mastery"),
        [254] = ("Hunter", "Marksmanship"),
        [255] = ("Hunter", "Survival"),
        [62] = ("Mage", "Arcane"),
        [63] = ("Mage", "Fire"),
        [64] = ("Mage", "Frost"),
        [268] = ("Monk", "Brewmaster"),
        [269] = ("Monk", "Windwalker"),
        [270] = ("Monk", "Mistweaver"),
        [65] = ("Paladin", "Holy"),
        [66] = ("Paladin", "Protection"),
        [70] = ("Paladin", "Retribution"),
        [256] = ("Priest", "Discipline"),
        [257] = ("Priest", "Holy"),
        [258] = ("Priest", "Shadow"),
        [259] = ("Rogue", "Assassination"),
        [260] = ("Rogue", "Outlaw"),
        [261] = ("Rogue", "Subtlety"),
        [262] = ("Shaman", "Elemental"),
        [263] = ("Shaman", "Enhancement"),
        [264] = ("Shaman", "Restoration"),
        [265] = ("Warlock", "Affliction"),
        [266] = ("Warlock", "Demonology"),
        [267] = ("Warlock", "Destruction"),
        [71] = ("Warrior", "Arms"),
        [72] = ("Warrior", "Fury"),
        [73] = ("Warrior", "Protection"),
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

    public static string SpecOf(int specId) => Table.TryGetValue(specId, out var entry) ? entry.Spec : "—";
}
