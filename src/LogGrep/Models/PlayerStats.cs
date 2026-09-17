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
public readonly record struct SpellUse(int SpellId, string Spell, int Uses, double Cooldown)
{
    /// <summary>How many times it could have gone out over a fight of that length.</summary>
    public int Room(TimeSpan duration)
        => Cooldown <= 0 ? Uses : (int)Math.Floor(duration.TotalSeconds / Cooldown) + 1;
}

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

/// <summary>What one group member did during a single pull.</summary>
public sealed class PlayerStats
{
    public required string Name { get; init; }

    /// <summary>Specialization ID from COMBATANT_INFO, 0 when the log did not report one.</summary>
    public int SpecId { get; set; }

    public long Damage { get; set; }
    public long Healing { get; set; }
    public long DamageTaken { get; set; }

    public IReadOnlyList<DeathRecord> Deaths { get; set; } = Array.Empty<DeathRecord>();

    /// <summary>How many times they cast anything at all during the attempt.</summary>
    public int Casts { get; set; }

    /// <summary>
    /// Seconds spent casting nothing, over and above what the casts themselves cost. Read against
    /// this player's own other attempts rather than against anybody else's - the point of the
    /// measure is that it needs to know nothing about their class.
    /// </summary>
    public double DeadSeconds { get; set; }

    /// <summary>Each spell they used, with the cooldown the log demonstrates for it.</summary>
    public IReadOnlyList<SpellUse> Spells { get; set; } = Array.Empty<SpellUse>();

    public string ClassName => Specs.ClassOf(SpecId);
    public string SpecName => Specs.SpecOf(SpecId);

    /// <summary>Blizzard's colour for this class, empty when the spec is unknown.</summary>
    public string ClassColor => Specs.ColorOf(SpecId);
}
