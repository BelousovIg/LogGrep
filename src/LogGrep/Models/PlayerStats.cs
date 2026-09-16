namespace LogGrep.Models;

/// <summary>One ability that hit a player shortly before they died.</summary>
public readonly record struct DamageCause(string Label, long Amount);

/// <summary>
/// One death: when it happened, measured from the start of the pull, and what had been
/// hitting the player over the seconds leading up to it.
/// </summary>
public sealed record DeathRecord(TimeSpan At, IReadOnlyList<DamageCause> Causes);


/// <summary>
/// One hostile debuff landing on a group member. This is where a pull records who took which
/// mechanic: raid-wide damage says nothing, but the debuff picks its target.
/// </summary>
public readonly record struct AuraHit(int SpellId, string Spell, string Player, TimeSpan At);

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

    public string ClassName => Specs.ClassOf(SpecId);
    public string SpecName => Specs.SpecOf(SpecId);

    /// <summary>Blizzard's colour for this class, empty when the spec is unknown.</summary>
    public string ClassColor => Specs.ColorOf(SpecId);
}
