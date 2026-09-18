namespace LogGrep.Analysis;

/// <summary>
/// The things that make casting nothing the right answer.
///
/// Idle time is the seconds somebody spent not casting, and most of those seconds are theirs to
/// account for. Some are not: a stun is not a rotation problem, and neither is the half-minute a
/// fight spends telling one person to carry an orb to the other side of the room. Counting those
/// puts the highest idle in the raid on whoever was handed the mechanic, which is the opposite of
/// what the column is for.
///
/// So this is a list, by spell id, of what the clock stops for. It is deliberately a list and not a
/// rule: nothing in a combat log says "this debuff meant you could not cast", and any rule that
/// guessed it - every debuff, every crowd control school, anything with a duration - would excuse
/// half the fight on some boss and nothing at all on another. A list is wrong in ways somebody can
/// see and fix; a clever rule is wrong in ways nobody notices.
///
/// It starts empty on purpose. Each entry is a decision about one fight, made by somebody who was
/// in it, and a guessed list would be worse than none - it would quietly forgive time that was
/// nobody's to forgive.
///
/// <para>
/// To add one: find the spell id in the log (the number before the name in a SPELL_AURA_APPLIED
/// line), and put it here with the name and a sentence on why it stops the clock. The seconds it
/// was on somebody are then left out of their idle, from the application to the removal.
/// </para>
/// </summary>
public static class Excused
{
    /// <summary>
    /// Spell id to what it is, for anybody reading the list rather than the log.
    ///
    /// Example of the shape an entry takes, kept here as a comment rather than as an entry, because
    /// a made-up id would excuse a real fight:
    /// <code>
    /// [452162] = "Pinned Down - stunned for the duration, nothing to be cast",
    /// </code>
    /// </summary>
    private static readonly Dictionary<int, string> Stops = new();

    /// <summary>Whether time under that aura is left out of somebody's idle.</summary>
    public static bool StopsTheClock(int spellId) => Stops.ContainsKey(spellId);

    /// <summary>What the list holds, for the settings screen and for anybody asking why.</summary>
    public static IReadOnlyDictionary<int, string> All => Stops;
}
