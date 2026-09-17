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
        _log.Healed(target, amount - overheal);
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

    /// <summary>
    /// A buff a player holds on themselves for a stretch of the fight. Uptime is read from the
    /// application and the removal, so both have to be in the log for the span to mean anything.
    /// </summary>
    public PullBuilder Holds(string player, Ability spell, TimeSpan from, TimeSpan to)
    {
        _log.Line(_start + from,
            $"SPELL_AURA_APPLIED,{_log.Units(player, player)},{(int)spell},\"{spell.NameOf()}\",0x1,BUFF");
        _log.Line(_start + to,
            $"SPELL_AURA_REMOVED,{_log.Units(player, player)},{(int)spell},\"{spell.NameOf()}\",0x1,BUFF");
        return this;
    }

    /// <summary>
    /// A debuff stacking up on somebody, one application a second. The count rides on the end of
    /// each line, which is how the app reads how many of a thing a player was carrying - and a
    /// stack that expires and re-lands starts at one again, so a peak is a real high-water mark.
    /// </summary>
    public PullBuilder BossStacks(string target, Ability with, int to)
    {
        for (int stack = 2; stack <= to; stack++)
        {
            _log.Line(_start + _at + TimeSpan.FromSeconds(stack - 1),
                $"SPELL_AURA_APPLIED_DOSE,{_log.Units(_log.BossName, target)},{(int)with}," +
                $"\"{with.NameOf()}\",0x1,DEBUFF,{stack}");
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

    /// <summary>
    /// A player casting steadily, which is what a rotation looks like from outside. The interval is
    /// the whole point of it: the app reads how much of an attempt somebody spent casting nothing,
    /// and that is the difference between one cast every two seconds and one every six.
    /// </summary>
    public PullBuilder Casting(string player, Ability spell, TimeSpan from, TimeSpan to, TimeSpan every)
    {
        for (var at = from; at <= to; at += every)
        {
            _log.Line(_start + at,
                $"SPELL_CAST_SUCCESS,{_log.Units(player, _log.BossName)},{(int)spell}," +
                $"\"{spell.NameOf()}\",0x1");
        }

        return this;
    }

    /// <summary>One cast by a player, at the moment the clock is on.</summary>
    public PullBuilder Casts(string player, Ability spell)
    {
        _log.Line(_start + _at,
            $"SPELL_CAST_SUCCESS,{_log.Units(player, _log.BossName)},{(int)spell}," +
            $"\"{spell.NameOf()}\",0x1");
        return this;
    }

    /// <summary>A cast by the boss that nobody stopped.</summary>
    public PullBuilder BossCasts(Ability spell)
    {
        _log.Line(_start + _at,
            $"SPELL_CAST_SUCCESS,{_log.Units(_log.BossName, _log.BossName)},{(int)spell}," +
            $"\"{spell.NameOf()}\",0x1");
        return this;
    }

    /// <summary>
    /// A cast that was cut short. It begins and is stopped - a cast that is interrupted never
    /// succeeds, so there is no SPELL_CAST_SUCCESS for it, and writing one would be writing a log
    /// the game never produces. The kick itself is a spell of its own.
    /// </summary>
    public PullBuilder Interrupts(string source, Ability spell)
    {
        _log.Line(_start + _at,
            $"SPELL_CAST_START,{_log.Units(_log.BossName, _log.BossName)},{(int)spell}," +
            $"\"{spell.NameOf()}\",0x1");
        _log.Line(_start + _at,
            $"SPELL_INTERRUPT,{_log.Units(source, _log.BossName)},{(int)Ability.Kick}," +
            $"\"{Ability.Kick.NameOf()}\",0x1,{(int)spell},\"{spell.NameOf()}\",0x1");
        return this;
    }

    /// <summary>A plain melee swing, which carries no spell id and so no way to stand elsewhere.</summary>
    public PullBuilder BossSwingsAt(string target, long amount)
    {
        _log.Line(_start + _at,
            $"SWING_DAMAGE,{_log.Units(_log.BossName, target)},{_log.Advanced(target, amount)}," +
            $"{amount},0,1,0,0,0,nil,nil,nil");
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
            $"\"{spell.NameOf()}\",0x1,{_log.Advanced(target, amount)},{amount},0,1,0,0,0,nil,nil,nil");
}
