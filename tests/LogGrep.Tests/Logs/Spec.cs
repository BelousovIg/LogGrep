namespace LogGrep.Tests.Logs;

/// <summary>
/// Specialization ids under their names. An enum rather than a set of constants because a spec is
/// not a number a test should be free to invent: the app only knows the ones the game has, and a
/// roster written with the wrong int would compile and then quietly mean nothing.
/// </summary>
public enum Spec
{
    ProtectionWarrior = 73,
    ProtectionPaladin = 66,
    BloodDeathKnight = 250,
    VengeanceDemonHunter = 581,

    HolyPriest = 257,
    RestorationShaman = 264,
    MistweaverMonk = 270,

    AssassinationRogue = 259,
    ArcaneMage = 62,
    BalanceDruid = 102,
    FrostDeathKnight = 251,
    WindwalkerMonk = 269,
    ElementalShaman = 262,
    HavocDemonHunter = 577,
}

/// <summary>Difficulty ids under their names.</summary>
public enum Difficulty
{
    /// <summary>A five player dungeon, which the app groups differently from a raid.</summary>
    HeroicDungeon = 2,

    Normal = 14,
    Heroic = 15,
    Mythic = 16,
}
