using System.Text;

namespace LogGrep.Tests.Logs;

/// <summary>
/// Every spell a scenario can mention, with the id the log carries it under. The id is the enum's
/// own value, so a spell means the same thing in every test and nothing has to be handed out at
/// runtime - and a test can no longer name a spell in one place and misspell it in the next, which
/// is exactly the failure that looks like a bug in the app.
/// </summary>
public enum Ability
{
    /// <summary>What a player swings with when the scenario does not care.</summary>
    Strike = 3101,

    /// <summary>What the boss swings with when the scenario does not care.</summary>
    Cleave = 3102,

    Mending = 3103,

    /// <summary>The killing blow, when nothing in particular is meant to have done it.</summary>
    BlastWave = 3104,

    /// <summary>The inert self-buff everybody carries, so the roster is seen from the first second.</summary>
    WellFed = 3105,

    /// <summary>Damage from long enough before a death that it should not be blamed for it.</summary>
    OldPoke = 3106,

    /// <summary>What a plain swing is called in a death breakdown; it has no spell id of its own.</summary>
    Melee = 0,

    /// <summary>What a rogue or a monk stops a cast with; the same spell all night.</summary>
    Kick = 3107,

    /// <summary>Something with a long cooldown, used a handful of times in a fight.</summary>
    Reckoning = 3108,

    PossessionBarrage = 3201,
    HollowingStrikes = 3202,
    CreepingRot = 3203,
    PowerWordFortitude = 3204,

    /// <summary>A cast the group is expected to stop.</summary>
    GrimIncantation = 3205,
}

/// <summary>The bosses the scenarios fight, under the encounter id the log records them with.</summary>
public enum Boss
{
    TheSoulcoiler = 2701,
    EntombedSentinels = 2702,
    ForgottenDepths = 2703,
}

/// <summary>Keystone dungeons, under the challenge mode id the log records them with.</summary>
public enum Dungeon
{
    TheRookery = 503,
    DarkflameCleft = 504,
}

/// <summary>
/// What the log prints for a spell or a boss. Almost every name is the enum member with spaces put
/// back into it, so only the ones the game punctuates need saying twice.
/// </summary>
public static class Named
{
    private static readonly Dictionary<Ability, string> Spelled = new()
    {
        [Ability.PowerWordFortitude] = "Power Word: Fortitude",
    };

    public static string NameOf(this Ability ability)
        => Spelled.TryGetValue(ability, out string? name) ? name : Spaced(ability.ToString());

    public static string NameOf(this Boss boss) => Spaced(boss.ToString());

    public static string NameOf(this Dungeon dungeon) => Spaced(dungeon.ToString());

    /// <summary>"HollowingStrikes" reads back as "Hollowing Strikes".</summary>
    private static string Spaced(string name)
    {
        var text = new StringBuilder(name.Length + 4);

        foreach (char c in name)
        {
            if (char.IsUpper(c) && text.Length > 0) text.Append(' ');
            text.Append(c);
        }

        return text.ToString();
    }
}
