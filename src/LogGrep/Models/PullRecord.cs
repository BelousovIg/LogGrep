namespace LogGrep.Models;

/// <summary>A byte range inside the source log file, covering whole lines including the line break.</summary>
public readonly record struct ByteRange(long Offset, int Length)
{
    public bool IsEmpty => Length <= 0;
    public static readonly ByteRange Empty = new(0, 0);
}

/// <summary>
/// One attempt: a raid/dungeon boss fight (ENCOUNTER_START..ENCOUNTER_END) or a full
/// keystone run (CHALLENGE_MODE_START..CHALLENGE_MODE_END).
/// </summary>
public sealed class PullRecord
{
    public required string GroupKey { get; init; }
    public required string EncounterName { get; init; }
    public int EncounterId { get; init; }
    public int DifficultyId { get; init; }
    public int KeystoneLevel { get; init; }
    public ContentKind Kind { get; init; }

    public bool Success { get; set; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }

    /// <summary>Distinct players seen in COMBATANT_INFO, falling back to the group size reported by the log.</summary>
    public int Participants { get; set; }

    /// <summary>Raw "Name-Realm-Region" spellings of every group member seen during the fight, sorted.</summary>
    public IReadOnlyList<string> Players { get; set; } = Array.Empty<string>();

    public long Damage { get; set; }
    public long Healing { get; set; }

    /// <summary>Byte range of the whole fight, from the START line to the end of the END line.</summary>
    public long StartOffset { get; init; }
    public long EndOffset { get; set; }

    /// <summary>Latest ZONE_CHANGE / MAP_CHANGE seen before the fight, re-emitted on export.</summary>
    public ByteRange ZoneChange { get; init; }
    public ByteRange MapChange { get; init; }

    public double Dps => Duration.TotalSeconds > 0.5 ? Damage / Duration.TotalSeconds : 0;
    public double Hps => Duration.TotalSeconds > 0.5 ? Healing / Duration.TotalSeconds : 0;

    public string DifficultyText => Kind == ContentKind.MythicPlus && KeystoneLevel > 0
        ? $"Mythic+ +{KeystoneLevel}"
        : Difficulties.NameOf(DifficultyId);
}

/// <summary>Everything the scanner learned about one log file.</summary>
public sealed class ScanResult
{
    public required string FilePath { get; init; }
    public long FileSize { get; init; }
    /// <summary>The COMBAT_LOG_VERSION header line, re-emitted at the top of every exported file.</summary>
    public ByteRange Header { get; set; } = ByteRange.Empty;
    public List<PullRecord> Pulls { get; } = new();
}
