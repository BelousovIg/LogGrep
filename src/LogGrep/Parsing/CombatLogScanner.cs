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

    /// <summary>
    /// How far back the hits are kept at all. Longer than the causes window, because how long
    /// somebody had been in trouble is the question that separates a burst from a grind, and that
    /// span can run well past ten seconds.
    /// </summary>
    private const double HistorySeconds = 45;

    /// <summary>How close to full counts as whole, when walking back to find where trouble began.</summary>
    private const double WholeShare = 0.95;

    /// <summary>
    /// The window the healing ceiling is measured over. What the group has actually landed on one
    /// player inside five seconds, at its best all evening, is what they have demonstrated they can
    /// do - no class, no spell list, no assumption about cooldowns.
    /// </summary>
    private const double CeilingSeconds = 5;

    /// <summary>How short a stretch counts as losing a health pool all at once rather than over time.</summary>
    private const double SuddenSeconds = 2;

    /// <summary>
    /// What one cast costs in time before the next one can follow. Haste shortens it, so taking the
    /// unhasted figure makes idle time an under-count - which is the direction to be wrong in when
    /// the output is "you stood there doing nothing".
    /// </summary>
    private const double GlobalCooldown = 1.5;

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
        _labels.Clear();

        var info = _fileSystem.FileInfo.New(path);
        _result = new ScanResult
        {
            Source = new LogSource
            {
                Path = path,
                Name = info.Name,
                Size = info.Length,
                Created = info.CreationTime,
                Named = LogSource.TimeInName(info.Name),
            },
        };

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
                Report(new ScanProgress(_result.Source.Size > 0 ? (bufferOrigin + filled) * 100.0 / _result.Source.Size : 0, null));
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

        _result.Source.Pulls = _result.Pulls.Count;
        _result.Source.Encounters = _result.Pulls.Select(p => p.GroupKey).Distinct(StringComparer.Ordinal).Count();
        _result.Source.Started = _result.Pulls.Count == 0 ? null : _result.Pulls.Min(p => p.StartTime);

        Report(new ScanProgress(100, null));
        return _result;
    }

    private void Report(ScanProgress progress) => _progress?.Report(progress);

    private void ProcessLine(ReadOnlySpan<byte> line, long start, long end)
    {
        _lastLineEnd = end;

        int eventStart = FindEventStart(line);
        if (eventStart < 0) return;

        // The first timestamp in the file is when this log was being written, and it is the one
        // thing about the file that copying it cannot change - so it, not the filesystem, is what
        // places this log among the others.
        if (_result.Source.Recorded == default)
        {
            _result.Source.Recorded = LogTimestamp.Parse(Timestamp(line, eventStart), default);
        }

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
                if (_result.Source.Header.IsEmpty) _result.Source.Header = new ByteRange(start, (int)(end - start));
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

            case EventKind.CastSuccess:
                if (_open != null) OnCastSuccess(line, eventStart);
                break;

            case EventKind.Interrupt:
                if (_open != null) OnInterrupt(line, eventStart);
                break;

            case EventKind.AuraRemoved:
                if (_open != null) OnAuraRemoved(line, eventStart);
                break;

            case EventKind.AuraDose:
                if (_open != null) OnAuraDose(line, eventStart);
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
            // Every keystone run is its own row, and stays its own row when a second file is read
            // beside this one - so the key has to name the file, not count within it.
            GroupKey = "M|" + _result.Source.Path + "|" + start,
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

        // Whatever anybody was still holding when the fight ended was held until then.
        foreach (var player in segment.Players.Values) player.Settle(segment.StartSeconds + duration.TotalSeconds);

        var roster = segment.Players
            .Select(entry => new PlayerStats
            {
                Name = entry.Value.Name,
                SpecId = segment.Specs.TryGetValue(entry.Key, out int spec) ? spec : 0,
                Build = segment.Builds.TryGetValue(entry.Key, out ulong build) ? build : 0,
                Damage = entry.Value.Damage,
                Healing = entry.Value.Healing,
                DamageTaken = entry.Value.DamageTaken,
                Deaths = entry.Value.Deaths.ToArray(),
                Casts = entry.Value.Casts,
                DeadSeconds = entry.Value.Dead,
                MaxHealth = entry.Value.MaxHealth,
                MeleeTaken = entry.Value.MeleeTaken,
                Struck = entry.Value.Struck,
                WasHit = entry.Value.WasHit,
                Spells = entry.Value.Spells
                    .Select(s => new SpellUse(s.Key, s.Value.Label, s.Value.Uses, s.Value.Shortest,
                        entry.Value.Output.TryGetValue(s.Key, out long did) ? did : 0))
                    .ToArray(),
                Stacks = entry.Value.Stacks
                    .Select(s => new StackPeak(s.Key, s.Value.Label, s.Value.Peak, s.Value.At))
                    .ToArray(),
                Buffs = entry.Value.Buffs
                    .Where(b => b.Value.Seconds > 0)
                    .Select(b => new BuffUptime(b.Key, b.Value.Label, TimeSpan.FromSeconds(b.Value.Seconds)))
                    .ToArray(),
            })
            .OrderByDescending(p => p.Damage)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var pull = new PullRecord
        {
            Source = _result.Source,
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
            Blows = segment.Blows.Values.ToArray(),
            HealCeiling = segment.Players.Values.Count == 0 ? 0 : segment.Players.Values.Max(p => p.BestHealing),
            Casts = segment.Casts.ToArray(),
            Damage = segment.Damage,
            Healing = segment.Healing,
            StartOffset = segment.StartOffset,
            EndOffset = endOffset,
            ZoneChange = segment.Zone,
            MapChange = segment.Map,
        };

        _result.Pulls.Add(pull);
        Report(new ScanProgress(_result.Source.Size > 0 ? endOffset * 100.0 / _result.Source.Size : 0, pull));
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

            // The talent array itself: node, entry and rank per pick. The numbers name nothing a
            // person would recognise, and there is no spell in them - but two builds that differ
            // hash differently, which is all it takes to ask whether a change made any difference.
            _open.Builds[Hash(guid)] = Hash(field);
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

        var elapsed = Elapsed(at);
        var (span, damage, biggest, from) = victim.Event(at, elapsed);

        victim.Deaths.Add(new DeathRecord(elapsed, victim.Causes(at))
        {
            Span = span,
            Damage = damage,
            Biggest = biggest,
            BiggestFrom = from,
            MaxHealth = victim.MaxHealth,
            Sudden = victim.Sudden(at),
            Healing = victim.HealingOver(at, span.TotalSeconds),
        });

        victim.Hits.Clear();

        // A corpse casts nothing, and counting that as idleness would make dying look like standing
        // about. The next cast after this starts a fresh gap rather than closing the one across it.
        victim.Died();
    }

    /// <summary>
    /// Records a hostile debuff landing on a group member. Buffs, and anything a group member cast,
    /// are skipped on purpose: a boss debuff picks its target, which is exactly what says who took
    /// a mechanic, while the hundreds of heals and procs flying around say nothing about that and
    /// would bury it. On a raid pull the filter turns twelve thousand aura events into a few hundred.
    /// </summary>

    /// <summary>
    /// An enemy cast that went off. Only the enemy's casts matter here - what the group casts is
    /// its own business, and nobody interrupts it.
    /// </summary>
    private void OnCastSuccess(ReadOnlySpan<byte> line, int eventStart)
    {
        _fields.Split(line);
        if (_fields.Count < 11) return;

        int spellId = _fields.Int(line, 9);
        if (spellId <= 0) return;

        int sourceFlags = _fields.Hex(line, 3);
        int affiliation = sourceFlags & AffiliationMask;

        // What the group casts is its own business - nobody interrupts it - but it is the whole
        // of what rotation reads: when they cast, how often, and what they went without.
        if (affiliation != 0 && affiliation != AffiliationOutsider && (sourceFlags & ControlPlayer) != 0)
        {
            PlayerAt(line, 1)?.Cast(LogTimestamp.SecondsOfDay(line, eventStart), spellId, Label(line, 10));
            return;
        }

        _open!.Casts.Add(new CastRecord(spellId, Label(line, 10), Stopped: false, string.Empty,
            Elapsed(LogTimestamp.SecondsOfDay(line, eventStart))));
    }

    /// <summary>
    /// A cast somebody cut short. The spell that was stopped is the extra spell at the end of the
    /// line, not the kick itself - the kick is the same three or four spells all night.
    /// </summary>
    private void OnInterrupt(ReadOnlySpan<byte> line, int eventStart)
    {
        _fields.Split(line);
        if (_fields.Count < 14) return;

        int spellId = _fields.Int(line, 12);
        if (spellId <= 0) return;

        var by = PlayerAt(line, 1);
        if (by == null) return;

        _open!.Casts.Add(new CastRecord(spellId, Label(line, 13), Stopped: true, by.Name,
            Elapsed(LogTimestamp.SecondsOfDay(line, eventStart))));
    }
    private void OnAuraApplied(ReadOnlySpan<byte> line, int eventStart)
    {
        _fields.Split(line);
        if (_fields.Count < 13) return;

        // The aura type sits right after the spell school.
        if (!_fields.Field(line, 12).SequenceEqual("DEBUFF"u8))
        {
            OnSelfBuff(line, eventStart, up: true);
            return;
        }

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

    /// <summary>
    /// A debuff stacking higher on somebody. The count is the last field on the line, so how many
    /// of a thing a player was carrying is read rather than inferred - and because a stack that
    /// expires and re-lands starts at one again, the peak is a real high-water mark rather than a
    /// running total.
    /// </summary>
    private void OnAuraDose(ReadOnlySpan<byte> line, int eventStart)
    {
        _fields.Split(line);
        if (_fields.Count < 14) return;
        if (!_fields.Field(line, 12).SequenceEqual("DEBUFF"u8)) return;

        int affiliation = _fields.Hex(line, 3) & AffiliationMask;
        if (affiliation != 0 && affiliation != AffiliationOutsider) return; // one of ours cast it

        var victim = PlayerAt(line, 5);
        if (victim == null) return;

        int stacks = _fields.Int(line, 13);
        if (stacks <= 0) return;

        victim.Stacked(_fields.Int(line, 9), Label(line, 10), stacks,
            Elapsed(LogTimestamp.SecondsOfDay(line, eventStart)));
    }

    private void OnAuraRemoved(ReadOnlySpan<byte> line, int eventStart)
    {
        _fields.Split(line);
        if (_fields.Count < 13) return;
        if (_fields.Field(line, 12).SequenceEqual("DEBUFF"u8)) return;

        OnSelfBuff(line, eventStart, up: false);
    }

    /// <summary>
    /// A buff a player put on themselves. Only their own: a raid buff somebody else keeps up says
    /// nothing about this player's rotation, and the whole point of reading uptime is that it is
    /// theirs to hold.
    /// </summary>
    private void OnSelfBuff(ReadOnlySpan<byte> line, int eventStart, bool up)
    {
        var source = _fields.Field(line, 1);
        if (!source.SequenceEqual(_fields.Field(line, 5))) return;

        var player = PlayerAt(line, 1);
        if (player == null) return;

        double at = LogTimestamp.SecondsOfDay(line, eventStart);
        if (at < 0) return;

        if (up) player.BuffUp(at, _fields.Int(line, 9), Label(line, 10));
        else player.BuffDown(at, _fields.Int(line, 9));
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
        bool fromTheGroup = affiliation != 0 && affiliation != AffiliationOutsider && (sourceFlags & ControlPlayer) != 0;
        if (fromTheGroup)
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

            // What this particular spell actually did. Without it there is no telling a cooldown
            // from a filler: the app was calling a spell a cooldown for being used rarely, which is
            // exactly what a spell nobody has a reason to press looks like.
            if (actor != null && prefixParams >= 3) actor.Did(_fields.Int(line, 9), amount);

            // Their own health pool, read off their own advanced block. Somebody who was never hit
            // all fight would otherwise have no pool at all, and a healer who stood out of every
            // mechanic is exactly the person that happens to.
            if (actor != null && advancedAt >= 0) actor.Pool(_fields.Long(line, advancedAt + 3));

            // Who opened on the boss. A pull belongs to the tank: whoever lands the first blow takes
            // the threat with it, and on the first seconds of a fight that is the whole story.
            if (actor != null && kind == EventKind.Damage)
            {
                actor.Strike(Elapsed(LogTimestamp.SecondsOfDay(line, eventStart)));
            }
        }

        // Healing our players received. Who cast it is already counted above; what matters here is
        // who it landed on, because the ceiling is what one player can be held up with.
        if (kind == EventKind.Heal)
        {
            double when = LogTimestamp.SecondsOfDay(line, eventStart);
            if (when >= 0) PlayerAt(line, 5)?.Heal(when, amount);
            return;
        }

        // Damage our players took, whoever dealt it - this is what the death breakdown is built from.
        if (kind != EventKind.Damage) return;

        var victim = PlayerAt(line, 5);
        if (victim == null) return;

        victim.DamageTaken += amount;

        double at = LogTimestamp.SecondsOfDay(line, eventStart);

        // And who the enemy hit first, which is the same question read from the other end.
        if (!fromTheGroup)
        {
            victim.Took(Elapsed(at));

            // A swing carries no spell id, which is exactly what makes it worth counting on its
            // own: it is the enemy hitting whoever it is looking at, and where it lands is the only
            // reading the log gives of who is holding its attention.
            if (prefixParams < 3) victim.Swung(amount);
        }

        if (at >= 0)
        {
            // The advanced block runs infoGUID, ownerGUID, currentHP, maxHP - so the two fields
            // after the owner are the target's health at the moment of the hit.
            long health = advancedAt >= 0 ? _fields.Long(line, advancedAt + 2) : 0;
            long maxHealth = advancedAt >= 0 ? _fields.Long(line, advancedAt + 3) : 0;

            victim.Hit(at, prefixParams >= 3 ? Label(line, 10) : "Melee", amount, health, maxHealth);
        }

        // Enemy spell damage, rolled up per spell and person. A swing has no spell id and cannot
        // be avoided by standing elsewhere, so it is left out of this.
        if (fromTheGroup || prefixParams < 3) return;

        int spellId = _fields.Int(line, 9);
        if (spellId <= 0) return;

        var key = (spellId, victim.Name);
        if (_open!.Blows.TryGetValue(key, out var blow))
        {
            _open.Blows[key] = blow with { Amount = blow.Amount + amount, Times = blow.Times + 1 };
        }
        else
        {
            _open.Blows[key] = new Blow(spellId, Label(line, 10), victim.Name, amount, 1, Elapsed(at));
        }
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
        CastSuccess,
        Interrupt,
        AuraRemoved,
        AuraDose,
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
                if (name.SequenceEqual("SPELL_INTERRUPT"u8)) return EventKind.Interrupt;
                break;
            case 18:
                if (name.SequenceEqual("COMBAT_LOG_VERSION"u8)) return EventKind.CombatLogVersion;
                if (name.SequenceEqual("CHALLENGE_MODE_END"u8)) return EventKind.ChallengeEnd;
                if (name.SequenceEqual("SPELL_AURA_APPLIED"u8)) return EventKind.AuraApplied;
                if (name.SequenceEqual("SPELL_CAST_SUCCESS"u8)) return EventKind.CastSuccess;
                if (name.SequenceEqual("SPELL_AURA_REMOVED"u8)) return EventKind.AuraRemoved;
                break;
            case 19:
                if (name.SequenceEqual("SPELL_PERIODIC_HEAL"u8)) { prefixParams = 3; return EventKind.Heal; }
                break;
            case 20:
                if (name.SequenceEqual("CHALLENGE_MODE_START"u8)) return EventKind.ChallengeStart;
                break;
            case 23:
                if (name.SequenceEqual("SPELL_AURA_APPLIED_DOSE"u8)) return EventKind.AuraDose;
                break;
            case 21:
                if (name.SequenceEqual("SPELL_PERIODIC_DAMAGE"u8)) { prefixParams = 3; return EventKind.Damage; }
                if (name.SequenceEqual("SPELL_BUILDING_DAMAGE"u8)) { prefixParams = 3; return EventKind.Damage; }
                break;
        }

        return EventKind.Other;
    }

    /// <summary>One hit a player took, kept only long enough to explain a death.</summary>
    /// <summary>
    /// One hit a player took. The health is what the advanced block reported on that event, which
    /// makes "that took 52% of them" a fact in the log rather than an estimate - and lets a death
    /// be walked back to the last moment the player was whole.
    /// </summary>
    private readonly record struct Hit(double At, string Label, long Amount, long Health, long MaxHealth);

    /// <summary>One spell a player used, rolled up: how often, and the shortest gap ever seen.</summary>
    private readonly record struct Casting(string Label, int Uses, double LastAt, double Shortest);

    /// <summary>One buff a player holds on themselves: when it went up, and how long it has held.</summary>
    private readonly record struct Holding(string Label, double Since, double Seconds);

    private sealed class PlayerState
    {
        public required string Name { get; init; }
        public long Damage { get; set; }
        public long Healing { get; set; }
        public long DamageTaken { get; set; }
        public List<DeathRecord> Deaths { get; } = new();

        /// <summary>
        /// When this player first struck an enemy, and when an enemy first struck them. Both are
        /// set once and never moved. Who was first is not a question about one person, it is a
        /// comparison between the tanks and everybody else, so the scanner keeps the times and
        /// leaves the comparing to whoever knows the roles.
        /// </summary>
        public TimeSpan? Struck { get; private set; }

        public TimeSpan? WasHit { get; private set; }

        public void Strike(TimeSpan at) => Struck ??= at;

        public void Took(TimeSpan at) => WasHit ??= at;

        /// <summary>
        /// The largest health pool the log ever reported for this player. It is the unit everything
        /// a mistake cost is counted in, so it is worth taking the largest rather than the latest:
        /// a pool shrinks mid-fight when a buff drops, and a hit measured against the shrunken one
        /// would read as heavier than it was.
        /// </summary>
        public long MaxHealth { get; private set; }

        public void Pool(long maxHealth)
        {
            if (maxHealth > MaxHealth) MaxHealth = maxHealth;
        }

        /// <summary>Enemy melee that landed on them, which is the log's only account of threat.</summary>
        public long MeleeTaken { get; private set; }

        public void Swung(long amount) => MeleeTaken += amount;

        /// <summary>Rolling window of recent hits; anything older than the window is dropped on the spot.</summary>
        public Queue<Hit> Hits { get; } = new();

        public void Hit(double at, string label, long amount, long health, long maxHealth)
        {
            Hits.Enqueue(new Hit(at, label, amount, health, maxHealth));
            Pool(maxHealth);

            // Kept longer than the causes window, because the event that killed somebody can be
            // longer than the last ten seconds of it - a player ground down over half a minute is
            // a different story from one bursted in two, and reading only the end tells the first
            // as though it were the second.
            double cutoff = at - HistorySeconds;
            while (Hits.Count > 0 && Hits.Peek().At < cutoff) Hits.Dequeue();
        }

        /// <summary>
        /// How long the player had been in trouble, and how much landed in that time. Walks back to
        /// the last hit that left them near full: that span is the event. A short span with huge
        /// damage is one conversation, a long span spent low is another, and the last three seconds
        /// of a death are the symptom of either.
        /// </summary>
        public (TimeSpan Span, long Damage, long Biggest, string From) Event(double at, TimeSpan elapsed)
        {
            long damage = 0;
            long biggest = 0;
            string from_ = string.Empty;
            double from = at;

            foreach (var hit in Hits.Reverse())
            {
                if (at >= 0 && hit.At > at) continue;

                damage += hit.Amount;
                if (hit.Amount > biggest)
                {
                    biggest = hit.Amount;
                    from_ = hit.Label;
                }

                from = hit.At;

                // The hit that left them whole is where the trouble started, not before it.
                if (hit.MaxHealth > 0 && hit.Health >= hit.MaxHealth * WholeShare) break;
            }

            var span = at >= 0 ? TimeSpan.FromSeconds(Math.Max(0, at - from)) : TimeSpan.Zero;
            return (span > elapsed ? elapsed : span, damage, biggest, from_);
        }

        /// <summary>Everything this player cast, rolled up per spell rather than kept one by one.</summary>
        public Dictionary<int, Casting> Spells { get; } = new();

        public int Casts { get; private set; }

        /// <summary>Seconds spent casting nothing at all, over and above the gaps a cast itself costs.</summary>
        public double Dead { get; private set; }

        private double _lastCast = -1;

        /// <summary>Stops the gap being measured across a death.</summary>
        public void Died() => _lastCast = -1;

        /// <summary>What each of this player's spells actually did, in damage or effective healing.</summary>
        public Dictionary<int, long> Output { get; } = new();

        public void Did(int spellId, long amount)
        {
            if (spellId <= 0 || amount <= 0) return;

            Output.TryGetValue(spellId, out long had);
            Output[spellId] = had + amount;
        }

        /// <summary>The highest any hostile debuff ever stacked on this player, and when.</summary>
        public Dictionary<int, (string Label, int Peak, TimeSpan At)> Stacks { get; } = new();

        public void Stacked(int spellId, string label, int stacks, TimeSpan at)
        {
            if (Stacks.TryGetValue(spellId, out var held) && held.Peak >= stacks) return;

            Stacks[spellId] = (label, stacks, at);
        }

        /// <summary>Buffs this player put on themselves, as seconds held rather than as events.</summary>
        public Dictionary<int, Holding> Buffs { get; } = new();

        public void BuffUp(double at, int spellId, string label)
        {
            if (Buffs.TryGetValue(spellId, out var held))
            {
                // A refresh while it is already up is not a second application of it.
                if (held.Since < 0) Buffs[spellId] = held with { Since = at };
            }
            else
            {
                Buffs[spellId] = new Holding(label, at, 0);
            }
        }

        public void BuffDown(double at, int spellId)
        {
            if (!Buffs.TryGetValue(spellId, out var held) || held.Since < 0) return;

            Buffs[spellId] = held with { Since = -1, Seconds = held.Seconds + Math.Max(0, at - held.Since) };
        }

        /// <summary>Closes whatever is still up when the fight ends, which is most of it.</summary>
        public void Settle(double at)
        {
            foreach (int spellId in Buffs.Keys.ToList())
            {
                var held = Buffs[spellId];
                if (held.Since >= 0) Buffs[spellId] = held with { Since = -1, Seconds = held.Seconds + Math.Max(0, at - held.Since) };
            }
        }

        /// <summary>
        /// One cast. Two things come out of it: the gap since the last one, which is how idle time
        /// is counted, and the gap since this same spell was last used - the shortest of those, over
        /// a whole evening, is the spell's cooldown as the log demonstrates it rather than as a
        /// database claims it.
        /// </summary>
        public void Cast(double at, int spellId, string label)
        {
            if (at < 0) return;

            Casts++;

            if (_lastCast >= 0)
            {
                double gap = at - _lastCast;
                if (gap > GlobalCooldown) Dead += gap - GlobalCooldown;
            }

            _lastCast = at;

            if (Spells.TryGetValue(spellId, out var casting))
            {
                double since = at - casting.LastAt;
                Spells[spellId] = casting with
                {
                    Uses = casting.Uses + 1,
                    LastAt = at,
                    Shortest = since > 0 && (casting.Shortest <= 0 || since < casting.Shortest)
                        ? since
                        : casting.Shortest,
                };
            }
            else
            {
                Spells[spellId] = new Casting(label, 1, at, 0);
            }
        }

        /// <summary>What landed on them in the last couple of seconds - a pool lost all at once.</summary>
        public long Sudden(double at)
        {
            long amount = 0;
            foreach (var hit in Hits)
            {
                if (hit.At >= at - SuddenSeconds && (at < 0 || hit.At <= at)) amount += hit.Amount;
            }

            return amount;
        }

        /// <summary>Healing this player received, kept only as a rolling sum over the ceiling window.</summary>
        private readonly Queue<(double At, long Amount)> _heals = new();
        private long _healed;

        /// <summary>The most healing this player ever received inside one ceiling window.</summary>
        public long BestHealing { get; private set; }

        public void Heal(double at, long amount)
        {
            _heals.Enqueue((at, amount));
            _healed += amount;

            double cutoff = at - CeilingSeconds;
            while (_heals.Count > 0 && _heals.Peek().At < cutoff) _healed -= _heals.Dequeue().Amount;

            if (_healed > BestHealing) BestHealing = _healed;
        }

        /// <summary>Healing that reached them over the window that killed them.</summary>
        public long HealingOver(double at, double seconds)
        {
            long amount = 0;
            foreach (var heal in _heals)
            {
                if (heal.At >= at - seconds && (at < 0 || heal.At <= at)) amount += heal.Amount;
            }

            return amount;
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

        /// <summary>Enemy casts that went off or were cut short, in the order they happened.</summary>
        public List<CastRecord> Casts { get; } = new();

        /// <summary>One entry per enemy spell and person it landed on, keyed so it stays one entry.</summary>
        public Dictionary<(int Spell, string Player), Blow> Blows { get; } = new();

        /// <summary>Specialization per player GUID hash, learned from COMBATANT_INFO.</summary>
        public Dictionary<ulong, int> Specs { get; } = new();

        /// <summary>What each player's talent array hashed to, which is how a build change is seen.</summary>
        public Dictionary<ulong, ulong> Builds { get; } = new();
        public long Damage { get; set; }
        public long Healing { get; set; }
    }
}
