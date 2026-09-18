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

    /// <summary>
    /// Whether the log said how this attempt ended.
    ///
    /// A fight with a start and no end is not a wipe, and reporting it as one - which is what this
    /// did, as a wipe of zero length - quietly added an attempt to every count a baseline rests on.
    /// It happens for two reasons that look identical in the file: the log stops mid-fight, or the
    /// game is still writing and the fight is going on right now. Neither is a result, so an
    /// unfinished attempt stays visible in the log browser, where its byte range is still perfectly
    /// good to cut out, and stays out of the analysis, where it is not an attempt at anything yet.
    /// </summary>
    public bool Finished { get; set; } = true;
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }

    /// <summary>Distinct players seen in COMBATANT_INFO, falling back to the group size reported by the log.</summary>
    public int Participants { get; set; }

    /// <summary>Raw "Name-Realm-Region" spellings of every group member seen during the fight, sorted.</summary>
    public IReadOnlyList<string> Players { get; set; } = Array.Empty<string>();

    /// <summary>Per player damage, healing, damage taken and deaths, ordered by damage done.</summary>
    public IReadOnlyList<PlayerStats> Roster { get; set; } = Array.Empty<PlayerStats>();

    /// <summary>Hostile debuffs that landed on group members, in the order they were applied.</summary>
    public IReadOnlyList<AuraHit> Debuffs { get; set; } = Array.Empty<AuraHit>();

    /// <summary>Enemy damage that landed on group members, one entry per spell and person.</summary>
    public IReadOnlyList<Blow> Blows { get; set; } = Array.Empty<Blow>();

    /// <summary>
    /// How much of the boss had been taken off, second by second, from nothing to all of it.
    ///
    /// This is the story of an attempt in one line: whether the group pushed the thing and lost it
    /// late, or never moved it at all. Several creatures at once are one pool - a council is a fight
    /// against all of them - and the share is what is gone out of the lot of them together.
    ///
    /// NaN in the seconds no boss fight was happening, which the chart draws as a gap: half a
    /// keystone run is trash, and a line held flat across it is a claim about a fight nobody was
    /// having. Empty when the log never stated any enemy's health at all.
    /// </summary>
    public IReadOnlyList<double> BossProgress { get; set; } = Array.Empty<double>();

    /// <summary>
    /// How many of the group were still standing, second by second. Together with the line above it
    /// this is the whole shape of an attempt: whether the raid melted at once or was worn down.
    /// </summary>
    public IReadOnlyList<int> Standing { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Every boss that went down inside this attempt, and the second it did.
    ///
    /// A boss pull has one of these or none, and it is the end of the fight. A keystone run has one
    /// per boss, which is the only place inside a half-hour record where anything is marked at all -
    /// the run is one row by design, and without these its chart is thirty minutes of unbroken line.
    ///
    /// One entry per encounter rather than per creature: a council is several corpses and one
    /// ending, and the log says nothing about which of them was the last to fall.
    /// </summary>
    public IReadOnlyList<BossKill> Kills { get; set; } = Array.Empty<BossKill>();

    /// <summary>What the group dealt and healed, second by second.</summary>
    public IReadOnlyList<long> DamageLine { get; set; } = Array.Empty<long>();

    public IReadOnlyList<long> HealingLine { get; set; } = Array.Empty<long>();

    /// <summary>
    /// Seconds in which the enemy was swinging at one or two people rather than at the room. This is
    /// the only stretch of a fight that says anything about threat: in a phase where a boss hits
    /// everybody, who it hit is a fact about the phase.
    /// </summary>
    public int ThreatSeconds { get; set; }

    /// <summary>Enemy casts, and whether each one went off or was stopped.</summary>
    public IReadOnlyList<CastRecord> Casts { get; set; } = Array.Empty<CastRecord>();

    /// <summary>
    /// The most healing that reached any one player inside five seconds during this attempt. Not a
    /// theoretical maximum - what this group actually landed, which is the only ceiling that can be
    /// argued with. Against a pool drained faster than that, the question is not the healers'.
    /// </summary>
    public long HealCeiling { get; set; }

    public long Damage { get; set; }
    public long Healing { get; set; }

    /// <summary>
    /// The file this attempt was read out of, and that every offset below points into. Settable
    /// because an attempt that came back from a cache has to be pointed at the file as it is now -
    /// same bytes, but a fresh reading of its size and its place among the other logs.
    /// </summary>
    public required LogSource Source { get; set; }

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
    public required LogSource Source { get; init; }

    public List<PullRecord> Pulls { get; } = new();

    /// <summary>
    /// How far a later read could pick up from: the end of the last fight this log finished. Not
    /// the last line read, because a fight still in progress is not a result to keep.
    /// </summary>
    public long ReadTo { get; set; }

    /// <summary>The zone and map lines in force at that point, which a later fight re-emits.</summary>
    public ByteRange Zone { get; set; }

    public ByteRange Map { get; set; }
}
