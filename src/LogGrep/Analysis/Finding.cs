using LogGrep.Models;
using LogGrep.ViewModels;

namespace LogGrep.Analysis;

/// <summary>
/// What kind of thing a cost is. The weight sorts findings against each other and nothing else; a
/// score has to add them up, and adding damage taken to output missed to a death is three different
/// sums. This is what lets it tell them apart without reading the sentence.
/// </summary>
public enum Toll
{
    None,
    Damage,
    Missed,
    Death,
}

/// <summary>
/// What a finding cost, and how much that matters next to the others. The weight is only ever
/// compared, never shown: a list of forty findings is useless unless something sorts it, and a
/// dropped buff worth two percent must not sit level with a death that cost the pull.
/// </summary>
public readonly record struct Cost(double Weight, string Text, Toll Toll = Toll.None, long Amount = 0)
{
    public static readonly Cost Unknown = new(0, "cost not worked out");

    /// <summary>
    /// A death outweighs any amount of damage, because the pull carries on without you. The number
    /// is far above anything a night of damage can reach - a whole evening measures in the hundreds
    /// of millions - so no pile of damage findings can ever outrank one death.
    /// </summary>
    public static Cost Death(TimeSpan at, string? from = null)
        => new(1e12, from == null
            ? "died at " + Display.Clock(at)
            : from + " killed you at " + Display.Clock(at),
            Toll.Death, 1);

    public static Cost Damage(long amount)
        => new(amount, Display.Amount(amount) + " taken", Toll.Damage, amount);

    /// <summary>
    /// Output that did not happen. Weighed the same as damage taken, because a cooldown left unused
    /// and a hit that should have been dodged cost the attempt the same kind of thing, and the list
    /// has to be able to sort one against the other.
    /// </summary>
    public static Cost Missed(long amount)
        => new(amount, Display.Amount(amount) + " of output missed", Toll.Missed, amount);

    public static Cost Nothing(string why) => new(0, why);
}

/// <summary>
/// One thing somebody did that is worth telling them about.
///
/// Four fields carry the weight, and the fourth is the one that makes the list worth opening: what
/// happened, why it counts as a mistake, what it cost, and what to do differently. The third is
/// what sorts them; the second is what stops an argument.
///
/// <see cref="SpellId"/> is zero when the finding is not about one ability. It is what lets a score
/// count how often the thing that caught somebody went out at all, which is the difference between
/// "you stood in it twice" and "you stood in two of eleven".
/// </summary>
public sealed record Finding(
    string Category,
    string Headline,
    string Evidence,
    string Advice,
    Cost Cost,
    int PullNumber,
    PullRecord Pull,
    string Player,
    int SpecId,
    TimeSpan At,
    int SpellId = 0)
{
    public Role Role => Specs.RoleOf(SpecId);

    /// <summary>
    /// Whether this one happened at a moment at all.
    ///
    /// Plenty of them do not. "Used Shadow Word: Death four fewer times than usual" is about a whole
    /// attempt, and "four mistakes in the first six attempts" is about a whole evening; neither has
    /// a second to point at. They were all carrying zero as a placeholder, which printed as "0:00"
    /// and read as a time - the one thing a placeholder must never do.
    /// </summary>
    public bool Timeless { get; init; }

    /// <summary>"0:31 took a tank mechanic" - the line a row shows before anybody opens anything.</summary>
    public string Line => Timeless ? Headline : Display.Clock(At) + " " + Headline;
}
