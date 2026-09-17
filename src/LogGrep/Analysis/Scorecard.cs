using LogGrep.Models;
using LogGrep.ViewModels;

namespace LogGrep.Analysis;

/// <summary>
/// The four things every player is read on, and the fifth the raid gets because it has healers in
/// it and one number cannot be both.
/// </summary>
public enum Axis
{
    /// <summary>What they put out - damage, or healing for a healer.</summary>
    Output,

    /// <summary>The raid's healing, kept apart from its damage. No player card uses this one.</summary>
    Healing,

    /// <summary>What hit them, and how much of it was theirs to avoid.</summary>
    Survival,

    /// <summary>What caught them, out of the times it went out.</summary>
    Mechanics,

    /// <summary>The job their role was there to do.</summary>
    Duty,
}

/// <summary>
/// One number on a card, or the reason there is not one.
///
/// A dash is a first-class answer here. Every index rests on a sample, and an index built on a
/// sample too thin to carry it is the one thing this app refuses to print - so a score that cannot
/// be had says why in the same space the number would have taken.
///
/// <see cref="Against"/> is not decoration. Every percentage here is a share of something that
/// really happened, and naming what it is a share of is the difference between a measurement and
/// an opinion.
/// </summary>
public sealed record Score(Axis Axis, double? Value, string Facts, string Against)
{
    public bool Known => Value != null;

    public string Text => Value == null ? "—" : Display.Percent(Value.Value);

    public static Score Missing(Axis axis, string why) => new(axis, null, why, string.Empty);
}

/// <summary>
/// What one player, or one whole raid, did with an attempt.
///
/// <see cref="Pools"/> is the loss in the only unit that compares across a raid: the player's own
/// health pool. <see cref="Findings"/> holds all of them, including the ones no index is built from
/// - the opening of a pull is the clearest of those, because an attempt offers exactly one opening
/// and a percentage drawn from a sample of one is an invention.
///
/// A null <see cref="Role"/> means the card is the raid rather than a person in it.
/// </summary>
public sealed record Scorecard(
    string Subject,
    Role? Role,
    IReadOnlyList<Score> Axes,
    double Pools,
    IReadOnlyList<Finding> Findings)
{
    public Score this[Axis axis] => Axes.FirstOrDefault(s => s.Axis == axis) ?? Score.Missing(axis, "—");

    /// <summary>The most expensive thing that happened, which is the one sentence a row shows.</summary>
    public Finding? Worst => Findings.Count == 0
        ? null
        : Findings.OrderByDescending(f => f.Cost.Weight).First();
}

/// <summary>
/// Turns the findings into numbers.
///
/// Two kinds of number, and they are not computed the same way. Output is a ratio to a ceiling -
/// and the ceiling is always one somebody actually reached, because the log cannot know what was
/// possible, only what happened. Everything else is a share of a real quantity: of the damage that
/// hit you, how much was yours to avoid; of the times a mechanic went out, how often it caught you.
/// Neither kind needs a budget invented for it, which is the whole reason they are shaped this way.
///
/// The findings are not summarised away. Every index is decomposable back to the findings it was
/// built from, which is what lets a number on screen be opened until it is an event in the log.
/// </summary>
public sealed class Scorecards
{
    /// <summary>Attempts of your own before your best counts as a ceiling rather than an accident.</summary>
    private const int MinimumAttempts = 5;

    /// <summary>
    /// An attempt shorter than this says nothing about a rate. Thirteen seconds of a wipe is not a
    /// damage number, it is the pull going wrong before anybody had a chance to do anything.
    /// </summary>
    private static readonly TimeSpan LongEnough = TimeSpan.FromSeconds(60);

    private readonly Attempts _attempts;
    private readonly ILookup<PullRecord, Finding> _byPull;
    private readonly Yardstick _damage;
    private readonly Yardstick _healing;
    private readonly double _bestDamage;
    private readonly double _bestHealing;
    private readonly Dictionary<PullRecord, IReadOnlyList<TimeSpan>> _shared = new();

    private Scorecards(Attempts attempts, IReadOnlyList<Finding> found)
    {
        _attempts = attempts;
        _byPull = found.ToLookup(f => f.Pull);

        _damage = Yardstick.Of(Rates(attempts, healing: false), MinimumAttempts);
        _healing = Yardstick.Of(Rates(attempts, healing: true), MinimumAttempts);

        var judged = attempts.Pulls.Where(p => p.Duration >= LongEnough).ToList();
        _bestDamage = judged.Count == 0 ? 0 : judged.Max(p => p.Dps);
        _bestHealing = judged.Count == 0 ? 0 : judged.Max(p => p.Hps);
    }

    public static Scorecards Of(Attempts attempts, IReadOnlyList<Finding> found)
        => new(attempts, found);

    /// <summary>How one player played one attempt.</summary>
    public Scorecard For(PullRecord pull, PlayerStats player)
    {
        var mine = About(pull)
            .Where(f => string.Equals(f.Player, player.Name, StringComparison.Ordinal))
            .OrderBy(f => f.At)
            .ToArray();

        var role = Specs.RoleOf(player.SpecId);
        int alone = Alone(pull, player);

        var axes = new[]
        {
            Output(pull, player, role),
            Survival(player, mine, alone),
            Mechanics(pull, player, mine),
            Duty(pull, player, role),
        };

        return new Scorecard(player.Name, role, axes, Lost(player, mine, alone), mine);
    }

    /// <summary>
    /// The findings that are about this attempt. The reviews are not: they are conclusions about a
    /// whole evening that had to be filed against some attempt, and "four mistakes in the first six
    /// attempts" on the card of the thirteenth is an answer to a question nobody asked there.
    /// </summary>
    private IEnumerable<Finding> About(PullRecord pull)
        => _byPull[pull].Where(f => f.Category != "the night" && f.Category != "rules");

    /// <summary>
    /// How many of their deaths were their own. A wipe kills everybody, and charging twenty people
    /// with a death each for the fact that the attempt ended is how a report starts crying wolf -
    /// the same death belongs to the raid's card, where it is one of the twenty that ended the pull.
    /// </summary>
    private int Alone(PullRecord pull, PlayerStats player)
    {
        if (player.Deaths.Count == 0) return 0;

        var shared = _shared.TryGetValue(pull, out var moments)
            ? moments
            : _shared[pull] = Collective.In(pull, _byPull[pull].ToArray());

        return player.Deaths.Count(d => !Collective.Covers(shared, d.At));
    }

    /// <summary>How the whole group played one attempt.</summary>
    public Scorecard For(PullRecord pull)
    {
        var all = About(pull).OrderBy(f => f.At).ToArray();

        var axes = new List<Score>
        {
            Group(pull, Axis.Output, pull.Dps, _bestDamage),
            Group(pull, Axis.Healing, pull.Hps, _bestHealing),
            RaidSurvival(pull, all),
            RaidMechanics(pull, all),
            Threat(pull),
        };

        // Every death counts here, including the ones the wipe took. They were not anybody's
        // personally and they are still what the attempt cost.
        double pools = pull.Roster.Sum(p => Lost(p, Mine(all, p.Name), p.Deaths.Count));

        return new Scorecard(pull.EncounterName, null, axes, pools, all);
    }

    /// <summary>What an attempt cost this player, in their own health pools.</summary>
    private static double Lost(PlayerStats player, IReadOnlyList<Finding> mine, int deaths)
    {
        if (player.MaxHealth <= 0) return 0;

        double avoidable = mine
            .Where(f => AxisOf(f.Category) == Axis.Survival && f.Cost.Toll == Toll.Damage)
            .Sum(f => f.Cost.Amount) / (double)player.MaxHealth;

        // A death is one pool and then some - the pull carries on without you - but the pool is
        // what can be defended, so the pool is what is counted.
        return avoidable + deaths;
    }

    private Score Output(PullRecord pull, PlayerStats player, Role role)
    {
        if (pull.Duration < LongEnough)
        {
            return Score.Missing(Axis.Output, "attempt shorter than a minute, nothing to judge");
        }

        bool healing = role == Role.Healer;
        double rate = Rate(healing ? player.Healing : player.Damage, pull.Duration);
        var best = (healing ? _healing : _damage).Best(player.Name, player.SpecId);

        if (!best.Exists || best.Value <= 0)
        {
            return Score.Missing(Axis.Output, "not enough attempts yet to know your best");
        }

        return new Score(Axis.Output, Math.Clamp(rate / best.Value, 0, 1),
            Display.Rate(rate) + " against " + Display.Rate(best.Value),
            best.Whose);
    }

    /// <summary>
    /// Of everything that landed on somebody, how much of it was theirs to avoid. The denominator is
    /// real damage rather than an allowance invented for the purpose, which is why this needs no
    /// threshold and cannot drift: a fight that hits harder moves both halves of it at once.
    /// </summary>
    private static Score Survival(PlayerStats player, IReadOnlyList<Finding> mine, int deaths)
    {
        if (player.MaxHealth <= 0)
        {
            return Score.Missing(Axis.Survival, "the log never reported a health pool for them");
        }

        double taken = player.DamageTaken / (double)player.MaxHealth;

        if (taken <= 0)
        {
            return new Score(Axis.Survival, 1, "nothing landed on them", "of everything that hit them");
        }

        double avoidable = mine
            .Where(f => AxisOf(f.Category) == Axis.Survival && f.Cost.Toll == Toll.Damage)
            .Sum(f => f.Cost.Amount) / (double)player.MaxHealth;

        // Deaths are named here and deliberately left out of the number. A ten-minute fight puts
        // forty health pools through somebody, so adding a death to that denominator moved the
        // score by three points and every person in the raid scored ninety-seven - which is not a
        // measurement, it is a formula saturating. What a death cost is carried by the pools, the
        // worst line and the lane, all three of which say it louder than a percentage would.
        string facts = Display.Decimal(avoidable) + " of " + Display.Decimal(taken) +
            " health pools was avoidable";
        if (deaths > 0) facts += ", and they took " + Deaths(deaths);

        return new Score(Axis.Survival, Math.Clamp(1 - avoidable / taken, 0, 1),
            facts, "of everything that hit them");
    }

    /// <summary>
    /// Of the times a mechanic went out, how often it caught them. Only the mechanics that caught
    /// them at all are counted: a player nothing landed on is not being credited against a list of
    /// things that were never aimed their way, they simply have nothing to answer for.
    /// </summary>
    private Score Mechanics(PullRecord pull, PlayerStats player, IReadOnlyList<Finding> mine)
    {
        var spells = mine
            .Where(f => AxisOf(f.Category) == Axis.Mechanics && f.SpellId > 0)
            .Select(f => f.SpellId)
            .Distinct()
            .ToList();

        if (spells.Count == 0)
        {
            return new Score(Axis.Mechanics, 1, "nothing that was judged avoidable caught them",
                "of the mechanics this fight throws");
        }

        long caught = 0;
        long went = 0;

        foreach (int spell in spells)
        {
            long times = Times(pull, player.Name, spell);
            if (times == 0) times = mine.Count(f => f.SpellId == spell);

            caught += times;
            went += Occasions(pull, spell, times);
        }

        return new Score(Axis.Mechanics, Math.Clamp(1 - caught / (double)went, 0, 1),
            "caught " + caught + " of the " + went + " times it went out",
            "of the times those mechanics went out");
    }

    /// <summary>
    /// The job the role was there to do. It is the only axis whose content differs, and each of the
    /// three is a share of something the log states outright rather than a judgement about a class.
    /// </summary>
    private Score Duty(PullRecord pull, PlayerStats player, Role role) => role switch
    {
        Role.Tank => Threat(pull),
        Role.Healer => Held(pull),
        _ => Uptime(pull, player),
    };

    /// <summary>
    /// Where the enemy's swings landed. A tank's job is to be the one they land on, and both tanks
    /// share the number because they shared the job.
    /// </summary>
    private static Score Threat(PullRecord pull)
    {
        long all = pull.Roster.Sum(p => p.MeleeTaken);
        if (all <= 0) return Score.Missing(Axis.Duty, "the enemy landed no melee at all");

        long tanks = pull.Roster.Where(p => Specs.RoleOf(p.SpecId) == Role.Tank).Sum(p => p.MeleeTaken);

        return new Score(Axis.Duty, tanks / (double)all,
            Display.Amount(tanks) + " of " + Display.Amount(all) + " melee damage landed on a tank",
            "the enemy swings at whoever it is looking at");
    }

    /// <summary>
    /// How many of the deaths were inside what this group has been seen to heal through. Above the
    /// ceiling the death is not the healers' question; below it, it is - and the ceiling is their
    /// own best five seconds all evening, so it is not a standard anybody else set for them.
    /// </summary>
    private Score Held(PullRecord pull)
    {
        double ceiling = _attempts.HealingCeiling;
        if (ceiling <= 0) return Score.Missing(Axis.Duty, "no healing landed all evening to measure by");

        var deaths = pull.Roster.SelectMany(p => p.Deaths).ToList();
        if (deaths.Count == 0) return new Score(Axis.Duty, 1, "nobody died", "of the deaths this attempt had");

        int pullable = deaths.Count(d => d.Rate <= ceiling);

        return new Score(Axis.Duty, 1 - pullable / (double)deaths.Count,
            pullable + " of " + deaths.Count + " deaths were inside what this group has healed through",
            "your own best five seconds of healing, all evening");
    }

    /// <summary>Time spent casting nothing, which is the one thing a damage dealer owes the fight.</summary>
    private static Score Uptime(PullRecord pull, PlayerStats player)
    {
        double seconds = pull.Duration.TotalSeconds;
        if (seconds <= 0) return Score.Missing(Axis.Duty, "the attempt has no length");

        var dead = TimeSpan.FromSeconds(Math.Min(player.DeadSeconds, seconds));

        return new Score(Axis.Duty, Math.Clamp(1 - dead.TotalSeconds / seconds, 0, 1),
            Display.Duration(dead) + " doing nothing in " + Display.Duration(pull.Duration),
            "the length of the fight itself");
    }

    private Score Group(PullRecord pull, Axis axis, double rate, double best)
    {
        if (pull.Duration < LongEnough)
        {
            return Score.Missing(axis, "attempt shorter than a minute, nothing to judge");
        }

        if (best <= 0 || _attempts.Pulls.Count < 2)
        {
            return Score.Missing(axis, "no other attempt to measure this one against");
        }

        return new Score(axis, Math.Clamp(rate / best, 0, 1),
            Display.Rate(rate) + " against " + Display.Rate(best),
            "the group's own best attempt at this fight");
    }

    private static Score RaidSurvival(PullRecord pull, IReadOnlyList<Finding> all)
    {
        double taken = 0;
        double avoidable = 0;
        int deaths = 0;

        foreach (var player in pull.Roster)
        {
            if (player.MaxHealth <= 0) continue;

            taken += player.DamageTaken / (double)player.MaxHealth;
            deaths += player.Deaths.Count;
            avoidable += Mine(all, player.Name)
                .Where(f => AxisOf(f.Category) == Axis.Survival && f.Cost.Toll == Toll.Damage)
                .Sum(f => f.Cost.Amount) / (double)player.MaxHealth;
        }

        if (taken <= 0) return Score.Missing(Axis.Survival, "nothing landed on anybody");

        string facts = Display.Decimal(avoidable) + " of " + Display.Decimal(taken) +
            " health pools was avoidable";
        if (deaths > 0) facts += ", and the group lost " + Deaths(deaths);

        return new Score(Axis.Survival, Math.Clamp(1 - avoidable / taken, 0, 1),
            facts, "of everything that hit the group");
    }

    private static Score RaidMechanics(PullRecord pull, IReadOnlyList<Finding> all)
    {
        var spells = all
            .Where(f => AxisOf(f.Category) == Axis.Mechanics && f.SpellId > 0)
            .Select(f => f.SpellId)
            .Distinct()
            .ToList();

        if (spells.Count == 0)
        {
            return new Score(Axis.Mechanics, 1, "nothing that was judged avoidable caught anybody",
                "of the mechanics this fight throws");
        }

        long caught = 0;
        long chances = 0;
        long castings = 0;

        // The chances are per person, not per casting. A player's own score asks how often a thing
        // caught them out of the times it went out; the group's has to ask the same question of
        // twenty people at once, so the denominator grows with the roster. Counting castings alone
        // put twenty people's hits over one group's castings and scored a clean attempt at seven
        // per cent.
        foreach (int spell in spells)
        {
            long times = pull.Roster.Sum(p => Times(pull, p.Name, spell));
            if (times == 0) times = all.Count(f => f.SpellId == spell);

            long went = Occasions(pull, spell, 1);

            caught += times;
            castings += went;
            chances += went * Math.Max(1, pull.Roster.Count);
        }

        return new Score(Axis.Mechanics, Math.Clamp(1 - caught / (double)Math.Max(chances, caught), 0, 1),
            "caught somebody " + caught + " times over " + castings + " castings",
            "of every chance the group had to be caught");
    }

    /// <summary>
    /// How many times an ability went out at all. The log does not number the occasions, so this
    /// takes the most anybody was hit by it - it cannot have gone out fewer times than that - and
    /// the enemy's own cast list when it has one, which is the better answer where it exists.
    /// </summary>
    private static long Occasions(PullRecord pull, int spell, long floor)
    {
        long casts = pull.Casts.Count(c => c.SpellId == spell);
        long landings = pull.Roster.Select(p => Times(pull, p.Name, spell)).DefaultIfEmpty(0).Max();

        return Math.Max(Math.Max(casts, landings), floor);
    }

    /// <summary>
    /// How often one ability caught one person. Counted from both ends of what the log records,
    /// because the rules behind these findings do not agree on which end matters: the avoidable
    /// damage rule reads blows, and the mechanics rule reads the debuff going on somebody. A
    /// mechanic recognised by its debuff lands no damage at all, and counting only blows scored it
    /// as though it had gone out once and caught them every time.
    /// </summary>
    private static long Times(PullRecord pull, string player, int spell)
        => pull.Blows
            .Where(b => b.SpellId == spell && string.Equals(b.Player, player, StringComparison.Ordinal))
            .Sum(b => (long)b.Times)
        + pull.Debuffs
            .Count(d => d.SpellId == spell && string.Equals(d.Player, player, StringComparison.Ordinal));

    private static IReadOnlyList<Finding> Mine(IReadOnlyList<Finding> all, string player)
        => all.Where(f => string.Equals(f.Player, player, StringComparison.Ordinal)).ToArray();

    private static string Deaths(int count) => count == 1 ? "one death" : count + " deaths";

    /// <summary>
    /// Which index a finding belongs under, and which belong under none.
    ///
    /// The opening of a pull is the clearest of the last kind. It is a real mistake with a real
    /// cost, and an attempt offers exactly one opening - so a percentage built from it would be a
    /// sample of one dressed as a measurement. It is shown, it is priced, and it is not averaged.
    /// </summary>
    private static Axis? AxisOf(string category) => category switch
    {
        "mechanics" or "interrupts" or "stacks" => Axis.Mechanics,
        "deaths" or AvoidableDamageDetector.Name => Axis.Survival,
        "idle" or "cooldowns" or "uptime" or "builds" => Axis.Output,
        _ => null,
    };

    private static IEnumerable<Measured> Rates(Attempts attempts, bool healing)
    {
        foreach (var pull in attempts.Pulls)
        {
            if (pull.Duration < LongEnough) continue;

            foreach (var player in pull.Roster)
            {
                if ((Specs.RoleOf(player.SpecId) == Role.Healer) != healing) continue;

                yield return new Measured(pull, player.Name, player.SpecId,
                    Rate(healing ? player.Healing : player.Damage, pull.Duration));
            }
        }
    }

    private static double Rate(long total, TimeSpan over)
        => over.TotalSeconds > 0.5 ? total / over.TotalSeconds : 0;
}
