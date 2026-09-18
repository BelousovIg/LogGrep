using System.Globalization;
using System.Text;

namespace LogGrep.Tests.Logs;

/// <summary>One member of the group, named rather than numbered.</summary>
public sealed record Fighter(string Name, Spec Spec)
{
    /// <summary>
    /// Who the character is, when that is not what they are called. The game gives a character an
    /// identifier that a rename or a realm transfer does not touch, so a scenario about somebody
    /// coming back under a new name sets this to the old one and keeps everything else different.
    /// </summary>
    public string Identity { get; init; } = string.Empty;

    /// <summary>The realm they are on, since a transfer changes that too.</summary>
    public string Realm { get; init; } = "Doomhammer";

    /// <summary>The log spells a character as Name-Realm; the app is expected to split that back apart.</summary>
    public string Raw => Name + "-" + Realm;

    public string Guid => "Player-1-" + Math.Abs((Identity.Length > 0 ? Identity : Name).GetHashCode())
        .ToString("X8", CultureInfo.InvariantCulture);
}

/// <summary>
/// Writes a combat log the app can read, from a description a person can read. The point is that a
/// test says what happened in the fight - who took what, who died to what - and never mentions a
/// field index or a unit flag, so a scenario stays legible and a new event kind is one method away.
///
/// Everything but a player's name is a type: the boss, the spell, the spec, the difficulty and the
/// clock. A person's name is the one thing that genuinely is free text, because the log carries it
/// as text and the app has to split a realm off the back of it.
/// </summary>
public sealed class CombatLogBuilder
{
    private const string BossGuid = "Creature-0-1-2-3-100001-0000000001";
    private const string PlayerFlags = "0x514";      // raid member, friendly, player controlled
    private const string BossFlags = "0xa48";        // outsider, hostile, NPC
    private const string NoRaidFlags = "0x80000000";

    /// <summary>Everybody's health pool. One number for the raid keeps a scenario's arithmetic readable.</summary>
    public const long HealthPool = 1_000_000;

    /// <summary>What a boss has, which is nothing like what a person has.</summary>
    public const long BossPool = 500_000_000;

    private readonly StringBuilder _text = new();
    private readonly Dictionary<string, long> _health = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _builds = new(StringComparer.Ordinal);
    private readonly List<Fighter> _roster = new();
    private DateTime _start = Evening(2026, 9, 15);
    private DateTime _origin = Evening(2026, 9, 15) - TimeSpan.FromMinutes(1);
    private DateTime? _copied;
    private string? _name;
    private Boss _boss = Boss.TheSoulcoiler;

    internal string BossName => _boss.NameOf();

    /// <summary>Which boss the current attempt is against, for events that need it as a target.</summary>
    internal Boss BossOf() => _boss;

    /// <summary>When the log itself says it was written - the first line, and every line after it.</summary>
    internal DateTime Recorded => _origin;

    /// <summary>What the filesystem will claim, which a copy from another machine resets.</summary>
    internal DateTime Created => _copied ?? _origin;

    /// <summary>The name the game would give this file, which carries its own timestamp.</summary>
    internal string FileName
        => _name ?? "WoWCombatLog-" + _origin.ToString("MMddyy_HHmmss", CultureInfo.InvariantCulture) + ".txt";

    /// <summary>
    /// Somebody changed their talents. Every attempt after this carries a different array for them,
    /// which is all the app ever sees of a build change.
    /// </summary>
    public CombatLogBuilder Respecced(string player)
    {
        _builds.TryGetValue(player, out int build);
        _builds[player] = build + 1;
        return this;
    }

    /// <summary>A file under a name of somebody's own - an export, rather than what the game wrote.</summary>
    public CombatLogBuilder Called(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>Eight in the evening on that day, which is when a raid night starts.</summary>
    public static DateTime Evening(int year, int month, int day) => new(year, month, day, 20, 0, 0);

    /// <summary>Which evening this log was recorded on. Files are read in the order they were.</summary>
    public CombatLogBuilder On(DateTime evening)
    {
        _start = evening;
        _origin = evening - TimeSpan.FromMinutes(1);
        return this;
    }

    /// <summary>
    /// A file carried over from another machine, whose creation date is the day it was copied and
    /// says nothing about the night it holds.
    /// </summary>
    public CombatLogBuilder CopiedOn(DateTime when)
    {
        _copied = when;
        return this;
    }

    public CombatLogBuilder Raid(params Fighter[] fighters)
    {
        _roster.Clear();
        _roster.AddRange(fighters);
        return this;
    }

    public static Fighter Tank(string name, Spec spec = Spec.ProtectionWarrior) => new(name, spec);

    public static Fighter Healer(string name, Spec spec = Spec.HolyPriest) => new(name, spec);

    public static Fighter Damage(string name, Spec spec = Spec.AssassinationRogue) => new(name, spec);

    /// <summary>One attempt. Every attempt at the same boss groups into one encounter row.</summary>
    public CombatLogBuilder Pull(Boss boss, Difficulty difficulty, Action<PullBuilder> body)
    {
        _boss = boss;
        int id = (int)boss;

        Line(_start, $"ENCOUNTER_START,{id},\"{BossName}\",{(int)difficulty},{_roster.Count},1");
        foreach (var fighter in _roster) Line(_start, CombatantInfo(fighter));

        // Everybody is present from the first second of a real fight - buffs, procs, the lot - and
        // the app builds its roster from the units it sees in events. One inert self-buff each keeps
        // the synthetic log honest about that without touching a single number a scenario asserts.
        foreach (var fighter in _roster)
        {
            Line(_start, $"SPELL_AURA_APPLIED,{Units(fighter.Name, fighter.Name)}," +
                         $"{(int)Ability.WellFed},\"{Ability.WellFed.NameOf()}\",0x1,BUFF");
        }

        // Everybody starts an attempt whole.
        _health.Clear();

        // And everybody states their own health at the start, the way a real log does on every
        // event somebody causes. Without it the app would only learn a player's pool if they dealt
        // damage - and a scenario about somebody being killed is exactly the one where they do not.
        foreach (var fighter in _roster)
        {
            Line(_start, $"SPELL_CAST_SUCCESS,{Units(fighter.Name, fighter.Name)}," +
                         $"{(int)Ability.WellFed},\"{Ability.WellFed.NameOf()}\",0x1,{Advanced(fighter.Name)}");
        }

        var pull = new PullBuilder(this, _start);
        body(pull);

        Line(_start + pull.Length, $"ENCOUNTER_END,{id},\"{BossName}\",{(int)difficulty},{_roster.Count}," +
                                   $"{(pull.Won ? 1 : 0)},{(long)pull.Length.TotalMilliseconds}");

        // The next attempt starts a minute after this one ended.
        _start += pull.Length + TimeSpan.FromMinutes(1);
        return this;
    }

    /// <summary>
    /// One keystone run. Unlike a boss, a run is never grouped with another - two runs of the same
    /// dungeon are two separate things that happen to share a name.
    /// </summary>
    public CombatLogBuilder Keystone(Dungeon zone, int level, Action<PullBuilder> body)
    {
        Line(_start, $"CHALLENGE_MODE_START,\"{zone.NameOf()}\",2521,{(int)zone},{level},[9,10]");
        foreach (var fighter in _roster) Line(_start, CombatantInfo(fighter));

        var run = new PullBuilder(this, _start);
        body(run);

        Line(_start + run.Length, $"CHALLENGE_MODE_END,2521,{(run.Won ? 1 : 0)},{level}," +
                                  $"{(long)run.Length.TotalMilliseconds},0");

        _start += run.Length + TimeSpan.FromMinutes(1);
        return this;
    }

    /// <summary>
    /// The same attempt, several times over. A rule is only drawn from a run of attempts, so a
    /// scenario about one needs a run of them, and writing ten out by hand would bury the one line
    /// that differs.
    /// </summary>
    public CombatLogBuilder Pulls(int times, Boss boss, Difficulty difficulty, Action<PullBuilder> body)
    {
        for (int i = 0; i < times; i++) Pull(boss, difficulty, body);
        return this;
    }

    public string Build()
    {
        var log = new StringBuilder();
        log.Append(_origin.ToString("M/d/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture))
           .AppendLine("  COMBAT_LOG_VERSION,22,ADVANCED_LOG_ENABLED,1,BUILD_VERSION,12.0.0,PROJECT_ID,1");
        log.Append(_text);
        return log.ToString();
    }

    internal Fighter Find(string name)
        => _roster.FirstOrDefault(f => f.Name == name)
           ?? throw new InvalidOperationException($"'{name}' is not in the raid. Add them with Raid(...).");

    internal void Line(DateTime at, string body)
        => _text.Append(at.ToString("M/d/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append("  ")
                .AppendLine(body);

    internal string Actor(string name)
        => name == BossName ? BossGuid : Find(name).Guid;

    internal string ActorName(string name)
        => name == BossName ? name : Find(name).Raw;

    internal string Flags(string name) => name == BossName ? BossFlags : PlayerFlags;

    internal string Units(string source, string target)
        => $"{Actor(source)},\"{ActorName(source)}\",{Flags(source)},{NoRaidFlags}," +
           $"{Actor(target)},\"{ActorName(target)}\",{Flags(target)},{NoRaidFlags}";

    internal string Nobody => $"0000000000000000,nil,{NoRaidFlags},{NoRaidFlags}";

    /// <summary>
    /// The advanced parameter block the game puts on every damage event, which is where the target's
    /// health lives. The app reads that health to say how much of somebody one hit took, so a
    /// generator that leaves the block out writes a log nothing can be concluded from.
    ///
    /// Health is tracked as the fight goes: the hit is applied here, and what the block reports is
    /// what is left afterwards, which is what the game reports too.
    /// </summary>
    internal string Advanced(string source)
    {
        _health.TryGetValue(source, out long left);
        if (left <= 0) left = PoolOf(source);

        // infoGUID, ownerGUID, currentHP, maxHP, then the stats nothing reads, then the position
        // fields that close the block - the app anchors on the first decimal to find the end.
        return $"{Actor(source)},0000000000000000,{left},{PoolOf(source)},0,0,0,0,0,0,0,0," +
               "1234.56,789.01,2000,3.14,80";
    }

    /// <summary>What a hit does to whoever it lands on, which the block on that line never says.</summary>
    internal void Took(string target, long amount)
    {
        _health.TryGetValue(target, out long left);
        if (left <= 0) left = PoolOf(target);

        _health[target] = Math.Max(0, left - amount);
    }

    /// <summary>A boss is not a person-sized thing, and writing it as one hid a real bug for weeks.</summary>
    private long PoolOf(string name) => name == BossName ? BossPool : HealthPool;

    /// <summary>Healing puts it back, so a fight can be a grind rather than one long slide.</summary>
    internal void Healed(string target, long amount)
    {
        _health.TryGetValue(target, out long left);
        if (left <= 0) left = HealthPool;

        _health[target] = Math.Min(HealthPool, left + amount);
    }

    internal string Victim(string name)
        => $"{Actor(name)},\"{ActorName(name)}\",{Flags(name)},{NoRaidFlags}";

    /// <summary>
    /// The specialization is the field right before the talent array, which is how the app finds it.
    /// The stats in between are filler; nothing reads them.
    /// </summary>
    /// <summary>
    /// Everything the log says about one player at the start of an attempt. The talent array is
    /// what a build change shows up in: the numbers name nothing, but a different array is a
    /// different build, which is exactly as much as the app can read from it.
    /// </summary>
    private string CombatantInfo(Fighter fighter)
    {
        string stats = string.Join(",", Enumerable.Repeat("0", 22));
        _builds.TryGetValue(fighter.Name, out int build);

        return $"COMBATANT_INFO,{fighter.Guid},0,{stats},{(int)fighter.Spec}," +
               $"[({80000 + build},{101000 + build},1)],[],[],[],0,0,0,0,0";
    }
}
