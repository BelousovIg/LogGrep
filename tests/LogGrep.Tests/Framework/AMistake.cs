using LogGrep.Models;
using LogGrep.Tests.Logs;
using LogGrep.ViewModels;

namespace LogGrep.Tests.Framework;

/// <summary>
/// One entry a scenario expects to find in the mistakes column, given as the things it is made of
/// rather than as a sentence. The wording belongs to the app, so a scenario says <em>2:51, this
/// spell, a tank's</em> and lets the check assemble what that has to read as - which means a change
/// to the wording is one edit here instead of one in every scenario that quotes it.
/// </summary>
public sealed record AMistake(TimeSpan At, Ability Spell, Role Owner)
{
    /// <summary>Why the app calls it a mistake, in its own words.</summary>
    public string Evidence { get; init; } = string.Empty;

    /// <summary>What it cost, in the app's words.</summary>
    public string Cost { get; init; } = string.Empty;

    /// <summary>The written fix, in the app's words.</summary>
    public string Advice { get; init; } = string.Empty;

    /// <summary>What the cell shows: "2:51 Possession Barrage - tank mechanic".</summary>
    public string Line => Display.Clock(At) + " " + Spell.NameOf() + " - " + Specs.NameOf(Owner) + " mechanic";

    /// <summary>The same entry in the tooltip, with all four fields of the finding under it.</summary>
    public string Block => string.Join(
        Environment.NewLine, Line, "      " + Evidence, "      " + Cost, "      " + Advice);
}
