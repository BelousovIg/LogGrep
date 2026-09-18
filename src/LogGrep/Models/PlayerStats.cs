namespace LogGrep.Models;

/// <summary>One ability that hit a player shortly before they died.</summary>
public readonly record struct DamageCause(string Label, long Amount);

/// <summary>
/// One death: when it happened, measured from the start of the pull, and what had been
/// hitting the player over the seconds leading up to it.
///
/// <see cref="Span"/> is how long they had been in trouble - the walk back to the last moment they
/// were whole - and <see cref="Damage"/> is what landed in that time. The two together are what
/// separates somebody bursted from somebody ground down, and the last three seconds of a death tell
/// neither story: they are the symptom of both.
/// </summary>
public sealed record DeathRecord(TimeSpan At, IReadOnlyList<DamageCause> Causes)
{
    /// <summary>How long the player had been below full when they died.</summary>
    public TimeSpan Span { get; init; }

    /// <summary>What landed on them over that span.</summary>
    public long Damage { get; init; }

    /// <summary>Their health pool, which is what a share of a hit is measured against.</summary>
    public long MaxHealth { get; init; }

    /// <summary>
    /// The largest single hit inside the span. One hit, not an ability's total over ten seconds -
    /// a bleed ticking forty times can add up past a health pool without any tick being a burst,
    /// and it is the one big hit that says a defensive was not pressed.
    /// </summary>
    public long Biggest { get; init; }

    /// <summary>What dealt that hit.</summary>
    public string BiggestFrom { get; init; } = string.Empty;

    /// <summary>That hit as a share of the pool: "this took 52% of them".</summary>
    public double BiggestShare => MaxHealth > 0 ? Biggest / (double)MaxHealth : 0;

    /// <summary>What landed over the last couple of seconds, however many hits it took to do it.</summary>
    public long Sudden { get; init; }

    /// <summary>That as a share of the pool: a whole health bar gone at once is its own kind of death.</summary>
    public double SuddenShare => MaxHealth > 0 ? Sudden / (double)MaxHealth : 0;

    /// <summary>Healing that actually reached this player over the span.</summary>
    public long Healing { get; init; }

    /// <summary>Damage per second over the span, which is what a healing ceiling is compared against.</summary>
    public double Rate => Span.TotalSeconds > 0.5 ? Damage / Span.TotalSeconds : Damage * 2;
}


/// <summary>
/// One spell a player used during an attempt, and the shortest gap ever seen between two of its
/// casts. That gap is the spell's cooldown as the log demonstrates it - no database, no class
/// knowledge, and it stays right through a patch that changes the number.
/// </summary>
/// <param name="Output">
/// What the spell did - damage dealt or healing that landed. A spell with none is a movement or a
/// utility and cannot be under-used in any sense the app can measure; a spell with very little is
/// a filler, and pressing it twice instead of four times costs nothing worth a sentence. Matched by
/// spell id, so an ability whose cast and whose effect carry different ids reads as having none.
/// </param>
public readonly record struct SpellUse(int SpellId, string Spell, int Uses, double Cooldown, long Output = 0)
{
    /// <summary>What one use of it was worth on average.</summary>
    public long Each => Uses > 0 ? Output / Uses : 0;

    /// <summary>How many times it could have gone out over a fight of that length.</summary>
    public int Room(TimeSpan duration)
        => Cooldown <= 0 ? Uses : (int)Math.Floor(duration.TotalSeconds / Cooldown) + 1;
}

/// <summary>
/// The highest a hostile debuff ever stacked on one player during an attempt, and the moment it
/// got there. A stack that expires and re-lands starts at one again, so this is a real high-water
/// mark rather than a count of applications.
/// </summary>
public readonly record struct StackPeak(int SpellId, string Spell, int Peak, TimeSpan At);

/// <summary>
/// One buff a player kept on themselves during an attempt, and how long they held it. Only their
/// own: a raid buff somebody else maintains says nothing about how this player played.
/// </summary>
public readonly record struct BuffUptime(int SpellId, string Spell, TimeSpan Held);

/// <summary>
/// One hostile debuff landing on a group member. This is where a pull records who took which
/// mechanic: raid-wide damage says nothing, but the debuff picks its target.
/// </summary>
public readonly record struct AuraHit(int SpellId, string Spell, string Player, TimeSpan At);

/// <summary>
/// What one enemy spell did to one group member over a whole attempt, rolled up. A pull holds
/// millions of damage events and perhaps a thousand pairs of spell and person, and it is the pairs
/// that say who stood in what.
/// </summary>
public readonly record struct Blow(int SpellId, string Spell, string Player, long Amount, int Times, TimeSpan First);

/// <summary>
/// An enemy cast that either went off or was cut short. <see cref="By"/> names whoever stopped it,
/// and is empty when nobody did.
/// </summary>
public readonly record struct CastRecord(int SpellId, string Spell, bool Stopped, string By, TimeSpan At);

/// <summary>A moment somebody went down or got back up.</summary>
public readonly record struct Flip(int Second, bool Up);

/// <summary>A boss that went down, and the second of the attempt it happened in.</summary>
public readonly record struct BossKill(int Second, string Name);

/// <summary>What one group member did during a single pull.</summary>
public sealed class PlayerStats
{
    public required string Name { get; init; }

    /// <summary>
    /// The log's own identifier for this character. A name is what a person reads and what a realm
    /// transfer or a rename changes; this does not, so it is what says a row from tonight and a row
    /// from last month are the same character rather than two people.
    ///
    /// It identifies a character, not a human. Somebody bringing an alt is a second one of these,
    /// and grouping those under one person is not something the log can do for us.
    /// </summary>
    public string Guid { get; init; } = string.Empty;

    /// <summary>Specialization ID from COMBATANT_INFO, 0 when the log did not report one.</summary>
    public int SpecId { get; set; }

    public long Damage { get; set; }
    public long Healing { get; set; }
    public long DamageTaken { get; set; }

    /// <summary>
    /// The largest health pool the log reported for them during the attempt, and the unit everything
    /// a mistake cost is counted in. Raw damage does not compare across a raid - the same hit is
    /// half a rogue and a fifth of a tank - and it does not compare across a patch either, whereas
    /// a share of somebody's own pool means the same thing in any tier. Zero when the log never
    /// reported one, which is the signal to show nothing rather than to divide by it.
    ///
    /// It is read off whichever unit the advanced block says it is about, which in a real log is
    /// whoever caused the event. Reading it as the victim's instead put the boss's seven hundred
    /// million on every damage dealer in the raid and turned every score built on it into quiet
    /// nonsense.
    /// </summary>
    public long MaxHealth { get; set; }

    /// <summary>
    /// Enemy melee that landed on them. A swing has no spell id and cannot be avoided by standing
    /// somewhere else - it goes wherever the enemy is looking - so where the swings landed is the
    /// log's only account of who was holding the fight's attention.
    /// </summary>
    public long MeleeTaken { get; set; }

    /// <summary>
    /// When they went down and when they got back up, in order. A death is not the end of somebody's
    /// fight - there are battle rezzes, soulstones and places where people simply stand back up.
    /// </summary>
    public IReadOnlyList<Flip> Flips { get; set; } = Array.Empty<Flip>();

    /// <summary>
    /// What they dealt, healed and took, second by second.
    ///
    /// A rate over a whole attempt answers a question nobody asked once somebody is looking at one
    /// minute of it. These are what let the same three columns be read over any stretch of the
    /// fight - and they are the reason a cached log costs a few megabytes rather than a few hundred
    /// kilobytes, which is the trade.
    /// </summary>
    public IReadOnlyList<long> DamageLine { get; set; } = Array.Empty<long>();

    public IReadOnlyList<long> HealingLine { get; set; } = Array.Empty<long>();

    public IReadOnlyList<long> TakenLine { get; set; } = Array.Empty<long>();

    /// <summary>What they dealt over a stretch of the fight, which is what a windowed rate is of.</summary>
    public long Over(IReadOnlyList<long> line, int from, int to)
    {
        if (line.Count == 0) return 0;

        long total = 0;
        for (int i = Math.Max(0, from); i <= Math.Min(to, line.Count - 1); i++) total += line[i];

        return total;
    }

    /// <summary>
    /// How many seconds of the fight the enemy spent swinging at this person while it was swinging
    /// at one or two people at all - the seconds when it was looking at somebody rather than at the
    /// room.
    /// </summary>
    public int HeldSeconds { get; set; }

    public IReadOnlyList<DeathRecord> Deaths { get; set; } = Array.Empty<DeathRecord>();

    /// <summary>How many times they cast anything at all during the attempt.</summary>
    public int Casts { get; set; }

    /// <summary>
    /// Seconds spent casting nothing, over the stretches of the fight they were on their feet, and
    /// over and above what the casts themselves cost.
    ///
    /// Time spent dead is not idleness - a corpse has no rotation to fall apart - so somebody who
    /// died three times is measured over the three stretches they were up for.
    /// </summary>
    public double IdleSeconds { get; set; }

    /// <summary>How much of the attempt they were on their feet for, which the idle is out of.</summary>
    public double AliveSeconds { get; set; }

    /// <summary>
    /// When they first landed damage on an enemy, and when an enemy first landed damage on them,
    /// measured from the start of the attempt. Null means it never happened during the fight.
    ///
    /// Neither is worth anything on its own: what a pull opening wrongly looks like is one of these
    /// arriving for somebody well before it arrives for a tank.
    /// </summary>
    public TimeSpan? Struck { get; set; }

    public TimeSpan? WasHit { get; set; }

    /// <summary>Each spell they used, with the cooldown the log demonstrates for it.</summary>
    public IReadOnlyList<SpellUse> Spells { get; set; } = Array.Empty<SpellUse>();

    /// <summary>The highest any hostile debuff stacked on them, and when it got there.</summary>
    public IReadOnlyList<StackPeak> Stacks { get; set; } = Array.Empty<StackPeak>();

    /// <summary>Buffs they put on themselves, and how long each was held.</summary>
    public IReadOnlyList<BuffUptime> Buffs { get; set; } = Array.Empty<BuffUptime>();

    /// <summary>
    /// The talent array, hashed. The log carries every node they picked, so what was run is known
    /// exactly - but the numbers name nothing a person would recognise and carry no spell. Two
    /// builds that differ hash differently, which is enough to ask whether a change made any
    /// difference, and is all that can be said without reference data.
    /// </summary>
    public ulong Build { get; set; }

    public string ClassName => Specs.ClassOf(SpecId);
    public string SpecName => Specs.SpecOf(SpecId);

    /// <summary>Blizzard's colour for this class, empty when the spec is unknown.</summary>
    public string ClassColor => Specs.ColorOf(SpecId);
}
