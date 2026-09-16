namespace LogGrep.Models;

/// <summary>Kind of content a pull belongs to.</summary>
public enum ContentKind
{
    Unknown,
    Raid,
    Dungeon,
    MythicPlus,
}

/// <summary>Maps WoW difficultyID values to a readable name and a content kind.</summary>
public static class Difficulties
{
    public static ContentKind KindOf(int difficultyId) => difficultyId switch
    {
        1 or 2 or 23 or 24 or 150 or 205 => ContentKind.Dungeon,
        8 => ContentKind.MythicPlus,
        3 or 4 or 5 or 6 or 7 or 9 or 14 or 15 or 16 or 17 or 33 or 151 or 175 or 176 or 193 or 194 => ContentKind.Raid,
        _ => ContentKind.Unknown,
    };

    public static string NameOf(int difficultyId) => difficultyId switch
    {
        1 => "Normal",
        2 => "Heroic",
        3 => "10 Player",
        4 => "25 Player",
        5 => "10 Player (Heroic)",
        6 => "25 Player (Heroic)",
        7 => "LFR",
        8 => "Mythic+",
        9 => "40 Player",
        11 => "Heroic Scenario",
        12 => "Normal Scenario",
        14 => "Normal",
        15 => "Heroic",
        16 => "Mythic",
        17 => "LFR",
        18 => "Event",
        23 => "Mythic",
        24 => "Timewalking",
        33 => "Timewalking",
        45 => "PvP",
        147 => "Normal Warfront",
        149 => "Heroic Warfront",
        150 => "Normal",
        151 => "LFR (Timewalking)",
        167 => "Torghast",
        175 => "10 Player",
        176 => "25 Player",
        193 => "10 Player (Heroic)",
        194 => "25 Player (Heroic)",
        205 => "Follower",
        _ => $"Difficulty {difficultyId}",
    };
}
