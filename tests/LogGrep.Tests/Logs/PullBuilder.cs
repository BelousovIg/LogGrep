namespace LogGrep.Tests.Logs;

/// <summary>
/// What happened during one attempt, written the way somebody would recount it: at this moment the
/// boss hit that tank, at that moment this rogue died to that spell. Times are measured from the
/// pull starting, so a scenario reads against the fight rather than against a clock.
/// </summary>
public sealed class PullBuilder
{
    private readonly CombatLogBuilder _log;
    private readonly DateTime _start;
    private TimeSpan _at;

    internal PullBuilder(CombatLogBuilder log, DateTime start)
    {
        _log = log;
        _start = start;
    }

    internal TimeSpan Length { get; private set; } = TimeSpan.FromMinutes(1);

    internal bool Won { get; private set; }

    /// <summary>How long the attempt ran. Rates are measured against this.</summary>
    public PullBuilder Lasting(TimeSpan duration)
    {
        Length = duration;
        return this;
    }

    /// <summary>Moves the clock. Everything after this happens at that moment.</summary>
    public PullBuilder At(TimeSpan time)
    {
        _at = time;
        return this;
    }

    public PullBuilder Deals(string source, Boss to, long amount, Ability with = Ability.Strike)
    {
        Damage(source, to.NameOf(), amount, with);
        return this;
    }

    /// <summary>Damage from the boss, the everyday case for anything a player takes.</summary>
    public PullBuilder BossHits(string target, long amount, Ability with = Ability.Cleave)
    {
        Damage(_log.BossName, target, amount, with);
        return this;
    }

    public PullBuilder Heals(
        string source, string target, long amount, long overheal = 0, Ability with = Ability.Mending)
    {
        _log.Line(_start + _at,
            $"SPELL_HEAL,{_log.Units(source, target)},{(int)with},\"{with.NameOf()}\",0x2," +
            $"{amount},{amount},{overheal},0,nil");
        return this;
    }

    /// <summary>A debuff the boss puts on somebody. This is what says who took a mechanic.</summary>
    public PullBuilder BossDebuffs(string target, Ability with, int times = 1)
    {
        for (int i = 0; i < times; i++)
        {
            _log.Line(_start + _at + TimeSpan.FromSeconds(i),
                $"SPELL_AURA_APPLIED,{_log.Units(_log.BossName, target)},{(int)with}," +
                $"\"{with.NameOf()}\",0x1,DEBUFF");
        }

        return this;
    }

    /// <summary>A friendly buff, which the app is expected to ignore when reading who took what.</summary>
    public PullBuilder Buffs(string source, string target, Ability with, int times = 1)
    {
        for (int i = 0; i < times; i++)
        {
            _log.Line(_start + _at + TimeSpan.FromSeconds(i),
                $"SPELL_AURA_APPLIED,{_log.Units(source, target)},{(int)with}," +
                $"\"{with.NameOf()}\",0x2,BUFF");
        }

        return this;
    }

    /// <summary>The killing blow and the death itself, which is what a death breakdown is built from.</summary>
    public PullBuilder Kills(string target, Ability with = Ability.BlastWave, long amount = 900_000)
    {
        Damage(_log.BossName, target, amount, with);
        _log.Line(_start + _at, $"UNIT_DIED,{_log.Nobody},{_log.Victim(target)},0");
        return this;
    }

    public PullBuilder Wipe()
    {
        Won = false;
        return this;
    }

    public PullBuilder Kill()
    {
        Won = true;
        return this;
    }

    private void Damage(string source, string target, long amount, Ability spell)
        => _log.Line(_start + _at,
            $"SPELL_DAMAGE,{_log.Units(source, target)},{(int)spell}," +
            $"\"{spell.NameOf()}\",0x1,{amount},0,1,0,0,0,nil,nil,nil");
}
