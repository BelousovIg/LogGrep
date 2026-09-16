using System.IO;
using System.IO.Abstractions;
using System.Text;
using LogGrep.Models;

namespace LogGrep.Parsing;

public sealed record ScanProgress(double Percent, PullRecord? Pull);

/// <summary>
/// Single pass, streaming scanner for retail WoW combat logs. It never holds the file in
/// memory: lines are read into a rolling byte buffer, only the events we care about are
/// parsed, and every pull is stored as a byte range so exporting is a raw copy later on.
/// </summary>
public sealed class CombatLogScanner
{
    private const int InitialBufferSize = 1 << 22; // 4 MB
    private const long ProgressStep = 8L << 20;    // report roughly every 8 MB

    // Unit flags, see COMBATLOG_OBJECT_* in the WoW API.
    private const int AffiliationMask = 0x0000000F;
    private const int AffiliationOutsider = 0x00000008;
    private const int ControlPlayer = 0x00000100;

    /// <summary>How far back a death looks for the hits that caused it.</summary>
    private const double CauseWindowSeconds = 10;

    /// <summary>Abilities listed per death; the long tail of chip damage is noise.</summary>
    private const int MaxCauses = 6;

    private const double SecondsPerDay = 24 * 60 * 60;

    private readonly FieldSplitter _fields = new();

    /// <summary>Spell names repeat millions of times; each distinct one becomes a string once.</summary>
    private readonly Dictionary<ulong, string> _labels = new();

    private readonly IFileSystem _fileSystem;

    private ScanResult _result = null!;
    private OpenSegment? _open;
    private ByteRange _zoneChange = ByteRange.Empty;
    private ByteRange _mapChange = ByteRange.Empty;
    private long _lastLineEnd;
    private int _keystoneCounter;
    private IProgress<ScanProgress>? _progress;


    /// <summary>The real disk. Tests hand in a fake one instead.</summary>
    public CombatLogScanner() : this(new FileSystem())
    {
    }

    public CombatLogScanner(IFileSystem fileSystem) => _fileSystem = fileSystem;

    public ScanResult Scan(string path, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        _progress = progress;
        _open = null;
        _zoneChange = ByteRange.Empty;
        _mapChange = ByteRange.Empty;
        _keystoneCounter = 0;
        _labels.Clear();

        var info = _fileSystem.FileInfo.New(path);
        _result = new ScanResult { FilePath = path, FileSize = info.Length };

        using var stream = _fileSystem.FileStream.New(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            1 << 20, FileOptions.SequentialScan);

        byte[] buffer = new byte[InitialBufferSize];
        int filled = 0;
        long bufferOrigin = 0;
        long nextProgressAt = ProgressStep;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            int read = stream.Read(buffer, filled, buffer.Length - filled);
            if (read == 0) break;
            filled += read;

            int lineStart = 0;
            while (true)
            {
                var searchArea = buffer.AsSpan(lineStart, filled - lineStart);
                int newline = searchArea.IndexOf((byte)'\n');
                if (newline < 0) break;

                int length = newline;
                if (length > 0 && searchArea[length - 1] == (byte)'\r') length--;
                ProcessLine(searchArea[..length], bufferOrigin + lineStart, bufferOrigin + lineStart + newline + 1);
                lineStart += newline + 1;
            }

            if (lineStart > 0)
            {
                Buffer.BlockCopy(buffer, lineStart, buffer, 0, filled - lineStart);
                bufferOrigin += lineStart;
                filled -= lineStart;
            }
            else if (filled == buffer.Length)
            {
                // One pathologically long line; grow rather than stall.
                Array.Resize(ref buffer, buffer.Length * 2);
            }

            if (bufferOrigin + filled >= nextProgressAt)
            {
                nextProgressAt = bufferOrigin + filled + ProgressStep;
                Report(new ScanProgress(_result.FileSize > 0 ? (bufferOrigin + filled) * 100.0 / _result.FileSize : 0, null));
            }
        }

        if (filled > 0)
        {
            int length = filled;
            if (buffer[length - 1] == (byte)'\r') length--;
            ProcessLine(buffer.AsSpan(0, length), bufferOrigin, bufferOrigin + filled);
        }

        // A log that was cut off mid-fight still gives us a usable (wiped) pull.
        if (_open != null) Close(_open, success: false, _lastLineEnd, TimeSpan.Zero, _open.LastTimestamp);

        Report(new ScanProgress(100, null));
        return _result;
    }

    private void Report(ScanProgress progress) => _progress?.Report(progress);

    private void ProcessLine(ReadOnlySpan<byte> line, long start, long end)
    {
        _lastLineEnd = end;

        int eventStart = FindEventStart(line);
        if (eventStart < 0) return;

        var rest = line[eventStart..];
        int comma = rest.IndexOf((byte)',');
        var name = comma < 0 ? rest : rest[..comma];

        var kind = Classify(name, out int prefixParams);

        // COMBATANT_INFO also starts with a player GUID, but its remaining fields are stats, not units.
        if (_open != null && comma >= 0 && kind != EventKind.CombatantInfo) TrackUnits(line, eventStart + comma + 1);

        if (kind == EventKind.Other) return;

        switch (kind)
        {
            case EventKind.CombatLogVersion:
                if (_result.Header.IsEmpty) _result.Header = new ByteRange(start, (int)(end - start));
                break;

            case EventKind.ZoneChange:
                _zoneChange = new ByteRange(start, (int)(end - start));
                break;

            case EventKind.MapChange:
                _mapChange = new ByteRange(start, (int)(end - start));
                break;

            case EventKind.EncounterStart:
                OnEncounterStart(line, eventStart, start);
                break;

            case EventKind.EncounterEnd:
                OnEncounterEnd(line, eventStart, end);
                break;

            case EventKind.ChallengeStart:
                OnChallengeStart(line, eventStart, start);
                break;

            case EventKind.ChallengeEnd:
                OnChallengeEnd(line, eventStart, end);
                break;

            case EventKind.CombatantInfo:
                OnCombatantInfo(line);
                break;

            case EventKind.UnitDied:
                if (_open != null) OnUnitDied(line, eventStart);
                break;

            case EventKind.AuraApplied:
                if (_open != null) OnAuraApplied(line, eventStart);
                break;

            case EventKind.Damage:
            case EventKind.Heal:
                if (_open != null) Accumulate(line, eventStart, kind, prefixParams);
                break;
        }
    }

    /// <summary>The event name follows the timestamp, separated by two spaces.</summary>
    private static int FindEventStart(ReadOnlySpan<byte> line)
    {
        int limit = Math.Min(line.Length - 1, 64);
        for (int i = 0; i < limit; i++)
        {
            if (line[i] == (byte)' ' && line[i + 1] == (byte)' ') return i + 2;
        }
        return -1;
    }

    private static string Timestamp(ReadOnlySpan<byte> line, int eventStart)
        => Encoding.UTF8.GetString(line[..Math.Max(0, eventStart - 2)]);

    private void OnEncounterStart(ReadOnlySpan<byte> line, int eventStart, long start)
    {
        // Boss pulls inside a keystone run belong to the run, they do not open a segment.
        if (_open is { IsKeystone: true }) return;
        if (_open != null) Close(_open, success: false, start, TimeSpan.Zero, _open.LastTimestamp);

        _fields.Split(line);
        int encounterId = _fields.Int(line, 1);
        string name = _fields.Text(line, 2);
        int difficulty = _fields.Int(line, 3);
        int groupSize = _fields.Int(line, 4);
        var kind = Difficulties.KindOf(difficulty);

        var startTime = LogTimestamp.Parse(Timestamp(line, eventStart), DateTime.Now);
        _open = new OpenSegment
        {
            IsKeystone = false,
            EncounterId = encounterId,
            Name = name.Length > 0 ? name : "Encounter " + encounterId,
            DifficultyId = difficulty,
            GroupSize = groupSize,
            Kind = kind,
            StartOffset = start,
            StartTime = startTime,
            StartSeconds = LogTimestamp.SecondsOfDay(line, eventStart),
            LastTimestamp = startTime,
            Zone = _zoneChange,
            Map = _mapChange,
            GroupKey = (kind == ContentKind.Raid ? "R|" : "D|") + encounterId + "|" + difficulty,
        };
    }

    private void OnEncounterEnd(ReadOnlySpan<byte> line, int eventStart, long end)
    {
        if (_open == null || _open.IsKeystone) return;

        _fields.Split(line);
        bool success = _fields.Flag(line, 5);
        long fightTimeMs = _fields.Count > 6 ? _fields.Long(line, 6) : 0;
        var endTime = LogTimestamp.Parse(Timestamp(line, eventStart), _open.StartTime);
        Close(_open, success, end, fightTimeMs > 0 ? TimeSpan.FromMilliseconds(fightTimeMs) : TimeSpan.Zero, endTime);
    }

    private void OnChallengeStart(ReadOnlySpan<byte> line, int eventStart, long start)
    {
        if (_open != null) Close(_open, success: false, start, TimeSpan.Zero, _open.LastTimestamp);

        _fields.Split(line);
        string zone = _fields.Text(line, 1);
        int keystoneLevel = _fields.Int(line, 4);

        var startTime = LogTimestamp.Parse(Timestamp(line, eventStart), DateTime.Now);
        _open = new OpenSegment
        {
            IsKeystone = true,
            EncounterId = _fields.Int(line, 3),
            Name = zone.Length > 0 ? zone : "Mythic Keystone",
            DifficultyId = 8,
            KeystoneLevel = keystoneLevel,
            GroupSize = 5,
            Kind = ContentKind.MythicPlus,
            StartOffset = start,
            StartTime = startTime,
            StartSeconds = LogTimestamp.SecondsOfDay(line, eventStart),
            LastTimestamp = startTime,
            Zone = _zoneChange,
            Map = _mapChange,
            GroupKey = "M|" + (++_keystoneCounter), // every keystone run is its own row
        };
    }

    private void OnChallengeEnd(ReadOnlySpan<byte> line, int eventStart, long end)
    {
        if (_open is not { IsKeystone: true }) return;

        _fields.Split(line);
        bool success = _fields.Flag(line, 2);
        int keystoneLevel = _fields.Int(line, 3);
        long totalTimeMs = _fields.Long(line, 4);
        if (keystoneLevel > 0) _open.KeystoneLevel = keystoneLevel;

        var endTime = LogTimestamp.Parse(Timestamp(line, eventStart), _open.StartTime);
        Close(_open, success, end, totalTimeMs > 0 ? TimeSpan.FromMilliseconds(totalTimeMs) : TimeSpan.Zero, endTime);
    }

    private void Close(OpenSegment segment, bool success, long endOffset, TimeSpan reportedDuration, DateTime endTime)
    {
        _open = null;

        var duration = reportedDuration > TimeSpan.Zero
            ? reportedDuration
            : endTime > segment.StartTime ? endTime - segment.StartTime : TimeSpan.Zero;

        var roster = segment.Players
            .Select(entry => new PlayerStats
            {
                Name = entry.Value.Name,
                SpecId = segment.Specs.TryGetValue(entry.Key, out int spec) ? spec : 0,
                Damage = entry.Value.Damage,
                Healing = entry.Value.Healing,
                DamageTaken = entry.Value.DamageTaken,
                Deaths = entry.Value.Deaths.ToArray(),
            })
            .OrderByDescending(p => p.Damage)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var pull = new PullRecord
        {
            GroupKey = segment.GroupKey,
            EncounterName = segment.Name,
            EncounterId = segment.EncounterId,
            DifficultyId = segment.DifficultyId,
            KeystoneLevel = segment.KeystoneLevel,
            Kind = segment.Kind,
            Success = success,
            StartTime = segment.StartTime,
            EndTime = endTime,
            Duration = duration,
            Participants = segment.Combatants.Count > 0 ? segment.Combatants.Count : segment.GroupSize,
            Players = roster.Select(p => p.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray(),
            Roster = roster,
            Debuffs = segment.Debuffs.ToArray(),
            Damage = segment.Damage,
            Healing = segment.Healing,
            StartOffset = segment.StartOffset,
            EndOffset = endOffset,
            ZoneChange = segment.Zone,
            MapChange = segment.Map,
        };

        _result.Pulls.Add(pull);
        Report(new ScanProgress(_result.FileSize > 0 ? endOffset * 100.0 / _result.FileSize : 0, pull));
    }

    /// <summary>
    /// The specialization is the field just before the talent array. Anchoring on the array
    /// rather than a fixed index keeps this working across log versions, which have added
    /// stats to the block more than once.
    /// </summary>
    private void OnCombatantInfo(ReadOnlySpan<byte> line)
    {
        if (_open == null) return;

        _fields.Split(line);
        var guid = _fields.Field(line, 1);
        if (guid.IsEmpty) return;

        _open.Combatants.Add(Encoding.UTF8.GetString(guid));

        int limit = Math.Min(_fields.Count, 40);
        for (int i = 3; i < limit; i++)
        {
            var field = _fields.Field(line, i);
            if (field.Length == 0 || field[0] != (byte)'[') continue;

            int spec = _fields.Int(line, i - 1);
            if (spec > 0) _open.Specs[Hash(guid)] = spec;
            return;
        }
    }

    /// <summary>
    /// Records a death and the abilities that landed on the player just before it. The window is
    /// cleared afterwards so that a later death is not blamed on the hits of the previous one.
    /// </summary>
    private void OnUnitDied(ReadOnlySpan<byte> line, int eventStart)
    {
        _fields.Split(line);
        if (_fields.Count < 6) return;

        var victim = PlayerAt(line, 5);
        if (victim == null) return;

        double at = LogTimestamp.SecondsOfDay(line, eventStart);

        victim.Deaths.Add(new DeathRecord(Elapsed(at), victim.Causes(at)));
        victim.Hits.Clear();
    }

    /// <summary>
    /// Records a hostile debuff landing on a group member. Buffs, and anything a group member cast,
    /// are skipped on purpose: a boss debuff picks its target, which is exactly what says who took
    /// a mechanic, while the hundreds of heals and procs flying around say nothing about that and
    /// would bury it. On a raid pull the filter turns twelve thousand aura events into a few hundred.
    /// </summary>
    private void OnAuraApplied(ReadOnlySpan<byte> line, int eventStart)
    {
        _fields.Split(line);
        if (_fields.Count < 13) return;

        // The aura type sits right after the spell school.
        if (!_fields.Field(line, 12).SequenceEqual("DEBUFF"u8)) return;

        int affiliation = _fields.Hex(line, 3) & AffiliationMask;
        if (affiliation != 0 && affiliation != AffiliationOutsider) return; // one of ours cast it

        var victim = PlayerAt(line, 5);
        if (victim == null) return;

        _open!.Debuffs.Add(new AuraHit(
            _fields.Int(line, 9),
            Label(line, 10),
            victim.Name,
            Elapsed(LogTimestamp.SecondsOfDay(line, eventStart))));
    }

    /// <summary>Time since the pull started, which is what deaths and debuffs are both stamped with.</summary>
    private TimeSpan Elapsed(double at)
    {
        if (at < 0 || _open!.StartSeconds < 0) return TimeSpan.Zero;

        double seconds = at - _open.StartSeconds;
        if (seconds < 0) seconds += SecondsPerDay; // the pull ran across midnight
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>Adds one damage or healing event to the open segment and to the players involved.</summary>
    private void Accumulate(ReadOnlySpan<byte> line, int eventStart, EventKind kind, int prefixParams)
    {
        _fields.Split(line);
        if (_fields.Count < 10) return;

        int index = 9 + prefixParams;
        if (index >= _fields.Count) return;

        // A block of unit-info fields may sit between the prefix and the payload. Its first
        // field is a GUID, which always starts with a letter (or is all zeroes) - amounts never do.
        var probe = _fields.Field(line, index);
        if (probe.Length == 0) return;
        bool advanced = char.IsAsciiLetter((char)probe[0])
            || (probe.Length == 16 && probe.IndexOfAnyExcept((byte)'0') < 0);
        int advancedAt = advanced ? index : -1;
        if (advanced)
        {
            // The block grew between log versions (17 fields in 11.x, 19 in 12.x), so anchor on
            // the position instead: everything before posX is an integer, and posX,posY,uiMapID,
            // facing,level always close the block.
            int position = FindFirstDecimal(line, index + 2);
            if (position < 0) return;
            index = position + 5;
        }

        int payload = _fields.Count - index;
        if (payload <= 0) return;

        long amount = _fields.Long(line, index);
        if (amount <= 0) return;

        if (kind == EventKind.Heal)
        {
            // amount, [baseAmount,] overhealing, absorbed, critical
            long overheal = payload >= 5 ? _fields.Long(line, index + 2) : _fields.Long(line, index + 1);
            amount -= overheal;
            if (amount <= 0) return;
        }

        int sourceFlags = _fields.Hex(line, 3);
        int affiliation = sourceFlags & AffiliationMask;
        if (affiliation != 0 && affiliation != AffiliationOutsider && (sourceFlags & ControlPlayer) != 0)
        {
            var actor = Actor(line, advancedAt);
            if (kind == EventKind.Heal)
            {
                _open!.Healing += amount;
                if (actor != null) actor.Healing += amount;
            }
            else
            {
                _open!.Damage += amount;
                if (actor != null) actor.Damage += amount;
            }
        }

        // Damage our players took, whoever dealt it - this is what the death breakdown is built from.
        if (kind != EventKind.Damage) return;

        var victim = PlayerAt(line, 5);
        if (victim == null) return;

        victim.DamageTaken += amount;

        double at = LogTimestamp.SecondsOfDay(line, eventStart);
        if (at >= 0) victim.Hit(at, prefixParams >= 3 ? Label(line, 10) : "Melee", amount);
    }

    /// <summary>
    /// The group member behind an event. Pets and guardians name their owner in the advanced
    /// block, which is the only place their damage can be pinned back on a player.
    /// </summary>
    private PlayerState? Actor(ReadOnlySpan<byte> line, int advancedAt)
    {
        var source = _fields.Field(line, 1);
        if (source.StartsWith("Player-"u8)) return Lookup(source);

        if (advancedAt < 0) return null;
        var owner = _fields.Field(line, advancedAt + 1);
        return owner.StartsWith("Player-"u8) ? Lookup(owner) : null;
    }

    private PlayerState? PlayerAt(ReadOnlySpan<byte> line, int index)
    {
        var guid = _fields.Field(line, index);
        return guid.StartsWith("Player-"u8) ? Lookup(guid) : null;
    }

    private PlayerState? Lookup(ReadOnlySpan<byte> guid)
        => _open!.Players.TryGetValue(Hash(guid), out var state) ? state : null;

    private string Label(ReadOnlySpan<byte> line, int index)
    {
        var span = _fields.Field(line, index);
        if (span.IsEmpty) return "Unknown";

        ulong key = Hash(span);
        if (_labels.TryGetValue(key, out string? text)) return text;

        text = Encoding.UTF8.GetString(span);
        _labels[key] = text;
        return text;
    }

    /// <summary>
    /// Builds the roster of the fight. Runs on every unit event, not just damage and healing, so
    /// that a player who spent the whole pull dead still shows up. It walks only as far as the
    /// eight base fields instead of splitting the whole line, and reads no further than the first
    /// seven commas.
    /// </summary>
    private void TrackUnits(ReadOnlySpan<byte> line, int from)
    {
        // Once every reported group member has been seen there is nothing left to learn.
        if (_open!.GroupSize > 0 && _open.Players.Count >= _open.GroupSize) return;

        var rest = line[from..];
        if (!rest.StartsWith("Player-"u8) && !rest.StartsWith("Creature-"u8) && !rest.StartsWith("Pet-"u8)
            && !rest.StartsWith("Vehicle-"u8) && !rest.StartsWith("0000000000000000"u8)) return;

        Span<int> starts = stackalloc int[7];
        Span<int> lengths = stackalloc int[7];
        int count = 0;
        int fieldStart = from;
        bool quoted = false;

        for (int i = from; i < line.Length && count < 7; i++)
        {
            byte c = line[i];
            if (c == (byte)'"') quoted = !quoted;
            else if (quoted) continue;
            else if (c == (byte)',')
            {
                starts[count] = fieldStart;
                lengths[count] = i - fieldStart;
                count++;
                fieldStart = i + 1;
            }
        }

        if (count < 7) return;

        TrackPlayer(line, starts[0], lengths[0], starts[1], lengths[1], starts[2], lengths[2]);
        TrackPlayer(line, starts[4], lengths[4], starts[5], lengths[5], starts[6], lengths[6]);
    }

    /// <summary>
    /// Notes a group member the first time their GUID shows up. The GUID is only hashed, so the
    /// hot path allocates nothing for the players we have already seen.
    /// </summary>
    private void TrackPlayer(ReadOnlySpan<byte> line, int guidAt, int guidLength,
        int nameAt, int nameLength, int flagsAt, int flagsLength)
    {
        var id = line.Slice(guidAt, guidLength);
        if (!id.StartsWith("Player-"u8)) return;

        int affiliation = FieldSplitter.ParseHex(line.Slice(flagsAt, flagsLength)) & AffiliationMask;
        if (affiliation == 0 || affiliation == AffiliationOutsider) return;

        ulong key = Hash(id);
        if (_open!.Players.ContainsKey(key)) return;

        var name = FieldSplitter.Unquote(line.Slice(nameAt, nameLength));
        if (name.IsEmpty) return;

        _open.Players[key] = new PlayerState { Name = Encoding.UTF8.GetString(name) };
    }

    private static ulong Hash(ReadOnlySpan<byte> value)
    {
        ulong hash = 14695981039346656037;
        foreach (byte b in value)
        {
            hash = (hash ^ b) * 1099511628211;
        }

        return hash;
    }

    /// <summary>Index of the first field holding a decimal number, i.e. the X coordinate.</summary>
    private int FindFirstDecimal(ReadOnlySpan<byte> line, int from)
    {
        int limit = Math.Min(_fields.Count, from + 24);
        for (int i = from; i < limit; i++)
        {
            if (_fields.Field(line, i).IndexOf((byte)'.') >= 0) return i;
        }

        return -1;
    }

    private enum EventKind
    {
        Other,
        CombatLogVersion,
        ZoneChange,
        MapChange,
        EncounterStart,
        EncounterEnd,
        ChallengeStart,
        ChallengeEnd,
        CombatantInfo,
        UnitDied,
        AuraApplied,
        Damage,
        Heal,
    }

    /// <summary>
    /// Recognises the handful of events we need. Dispatching on length first keeps the
    /// hot path (millions of ignored lines) down to one or two span comparisons.
    /// </summary>
    private static EventKind Classify(ReadOnlySpan<byte> name, out int prefixParams)
    {
        prefixParams = 0;
        switch (name.Length)
        {
            case 9:
                if (name.SequenceEqual("UNIT_DIED"u8)) return EventKind.UnitDied;
                break;
            case 10:
                if (name.SequenceEqual("SPELL_HEAL"u8)) { prefixParams = 3; return EventKind.Heal; }
                if (name.SequenceEqual("MAP_CHANGE"u8)) return EventKind.MapChange;
                break;
            case 11:
                if (name.SequenceEqual("ZONE_CHANGE"u8)) return EventKind.ZoneChange;
                break;
            case 12:
                if (name.SequenceEqual("SPELL_DAMAGE"u8)) { prefixParams = 3; return EventKind.Damage; }
                if (name.SequenceEqual("SWING_DAMAGE"u8)) return EventKind.Damage;
                if (name.SequenceEqual("RANGE_DAMAGE"u8)) { prefixParams = 3; return EventKind.Damage; }
                if (name.SequenceEqual("DAMAGE_SPLIT"u8)) { prefixParams = 3; return EventKind.Damage; }
                break;
            case 13:
                if (name.SequenceEqual("DAMAGE_SHIELD"u8)) { prefixParams = 3; return EventKind.Damage; }
                if (name.SequenceEqual("ENCOUNTER_END"u8)) return EventKind.EncounterEnd;
                break;
            case 14:
                if (name.SequenceEqual("COMBATANT_INFO"u8)) return EventKind.CombatantInfo;
                break;
            case 15:
                if (name.SequenceEqual("ENCOUNTER_START"u8)) return EventKind.EncounterStart;
                break;
            case 18:
                if (name.SequenceEqual("COMBAT_LOG_VERSION"u8)) return EventKind.CombatLogVersion;
                if (name.SequenceEqual("CHALLENGE_MODE_END"u8)) return EventKind.ChallengeEnd;
                if (name.SequenceEqual("SPELL_AURA_APPLIED"u8)) return EventKind.AuraApplied;
                break;
            case 19:
                if (name.SequenceEqual("SPELL_PERIODIC_HEAL"u8)) { prefixParams = 3; return EventKind.Heal; }
                break;
            case 20:
                if (name.SequenceEqual("CHALLENGE_MODE_START"u8)) return EventKind.ChallengeStart;
                break;
            case 21:
                if (name.SequenceEqual("SPELL_PERIODIC_DAMAGE"u8)) { prefixParams = 3; return EventKind.Damage; }
                if (name.SequenceEqual("SPELL_BUILDING_DAMAGE"u8)) { prefixParams = 3; return EventKind.Damage; }
                break;
        }

        return EventKind.Other;
    }

    /// <summary>One hit a player took, kept only long enough to explain a death.</summary>
    private readonly record struct Hit(double At, string Label, long Amount);

    private sealed class PlayerState
    {
        public required string Name { get; init; }
        public long Damage { get; set; }
        public long Healing { get; set; }
        public long DamageTaken { get; set; }
        public List<DeathRecord> Deaths { get; } = new();

        /// <summary>Rolling window of recent hits; anything older than the window is dropped on the spot.</summary>
        public Queue<Hit> Hits { get; } = new();

        public void Hit(double at, string label, long amount)
        {
            Hits.Enqueue(new Hit(at, label, amount));

            double cutoff = at - CauseWindowSeconds;
            while (Hits.Count > 0 && Hits.Peek().At < cutoff) Hits.Dequeue();
        }

        /// <summary>The heaviest abilities that landed inside the window, largest first.</summary>
        public IReadOnlyList<DamageCause> Causes(double at)
        {
            if (Hits.Count == 0) return Array.Empty<DamageCause>();

            double from = at - CauseWindowSeconds;
            var totals = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var hit in Hits)
            {
                if (at >= 0 && hit.At < from) continue;
                totals.TryGetValue(hit.Label, out long sum);
                totals[hit.Label] = sum + hit.Amount;
            }

            return totals
                .OrderByDescending(e => e.Value)
                .Take(MaxCauses)
                .Select(e => new DamageCause(e.Key, e.Value))
                .ToArray();
        }
    }

    private sealed class OpenSegment
    {
        public required string GroupKey { get; init; }
        public required string Name { get; init; }
        public bool IsKeystone { get; init; }
        public int EncounterId { get; init; }
        public int DifficultyId { get; init; }
        public int KeystoneLevel { get; set; }
        public int GroupSize { get; init; }
        public ContentKind Kind { get; init; }
        public long StartOffset { get; init; }
        public DateTime StartTime { get; init; }

        /// <summary>Seconds since midnight at the start line, the baseline every death time is measured from.</summary>
        public double StartSeconds { get; init; }
        public DateTime LastTimestamp { get; set; }
        public ByteRange Zone { get; init; }
        public ByteRange Map { get; init; }
        public HashSet<string> Combatants { get; } = new(StringComparer.Ordinal);
        public Dictionary<ulong, PlayerState> Players { get; } = new();

        /// <summary>Hostile debuffs that landed on group members, in the order they were applied.</summary>
        public List<AuraHit> Debuffs { get; } = new();

        /// <summary>Specialization per player GUID hash, learned from COMBATANT_INFO.</summary>
        public Dictionary<ulong, int> Specs { get; } = new();
        public long Damage { get; set; }
        public long Healing { get; set; }
    }
}
