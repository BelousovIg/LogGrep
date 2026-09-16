using LogGrep.Models;
using LogGrep.ViewModels;

namespace LogGrep.Analysis;

/// <summary>
/// What a finding cost, and how much that matters next to the others. The weight is only ever
/// compared, never shown: a list of forty findings is useless unless something sorts it, and a
/// dropped buff worth two percent must not sit level with a death that cost the pull.
/// </summary>
public readonly record struct Cost(double Weight, string Text)
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
            : from + " killed you at " + Display.Clock(at));

    public static Cost Damage(long amount) => new(amount, Display.Amount(amount) + " taken");

    public static Cost Nothing(string why) => new(0, why);
}

/// <summary>
/// One thing somebody did that is worth telling them about.
///
/// Four fields carry the weight, and the fourth is the one that makes the list worth opening: what
/// happened, why it counts as a mistake, what it cost, and what to do differently. The third is
/// what sorts them; the second is what stops an argument.
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
    TimeSpan At)
{
    public Role Role => Specs.RoleOf(SpecId);

    /// <summary>"0:31 took a tank mechanic" - the line a row shows before anybody opens anything.</summary>
    public string Line => Display.Clock(At) + " " + Headline;
}
