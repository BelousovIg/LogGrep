namespace LogGrep.Models;

/// <summary>One ability that hit a player shortly before they died.</summary>
public readonly record struct DamageCause(string Label, long Amount);

/// <summary>
/// One death: when it happened, measured from the start of the pull, and what had been
/// hitting the player over the seconds leading up to it.
/// </summary>
public sealed record DeathRecord(TimeSpan At, IReadOnlyList<DamageCause> Causes);

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
}
