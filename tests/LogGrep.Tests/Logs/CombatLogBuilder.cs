using System.Globalization;
using System.Text;

namespace LogGrep.Tests.Logs;

/// <summary>One member of the group, named rather than numbered.</summary>
public sealed record Fighter(string Name, Spec Spec)
{
    /// <summary>The log spells a character as Name-Realm; the app is expected to split that back apart.</summary>
    public string Raw => Name + "-Doomhammer";

    public string Guid => "Player-1-" + Math.Abs(Name.GetHashCode()).ToString("X8", CultureInfo.InvariantCulture);
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

    private readonly StringBuilder _text = new();
    private readonly List<Fighter> _roster = new();
    private DateTime _start = new(2026, 9, 15, 20, 0, 0);
    private Boss _boss = Boss.TheSoulcoiler;

    internal string BossName => _boss.NameOf();

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

        var pull = new PullBuilder(this, _start);
        body(pull);

        Line(_start + pull.Length, $"ENCOUNTER_END,{id},\"{BossName}\",{(int)difficulty},{_roster.Count}," +
                                   $"{(pull.Won ? 1 : 0)},{(long)pull.Length.TotalMilliseconds}");

        // The next attempt starts a minute after this one ended.
        _start += pull.Length + TimeSpan.FromMinutes(1);
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
        log.AppendLine("9/15/2026 19:59:00.000  COMBAT_LOG_VERSION,22,ADVANCED_LOG_ENABLED,1,BUILD_VERSION,12.0.0,PROJECT_ID,1");
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

    internal string Victim(string name)
        => $"{Actor(name)},\"{ActorName(name)}\",{Flags(name)},{NoRaidFlags}";

    /// <summary>
    /// The specialization is the field right before the talent array, which is how the app finds it.
    /// The stats in between are filler; nothing reads them.
    /// </summary>
    private string CombatantInfo(Fighter fighter)
    {
        string stats = string.Join(",", Enumerable.Repeat("0", 22));
        return $"COMBATANT_INFO,{fighter.Guid},0,{stats},{(int)fighter.Spec},[(1,1,1)],[],[],[],0,0,0,0,0";
    }
}
