using LogGrep.Models;
using LogGrep.Tests.Logs;

namespace LogGrep.Tests.Framework;

/// <summary>
/// What was already true when the scenario starts. Every step returns the builder, so a setup of
/// several steps reads as one sentence:
/// <c>Given.IOpenedLog(log).And.IOpenedPull(Boss.TheSoulcoiler, 1);</c>
/// </summary>
public sealed class Given
{
    private readonly TestMethods _act;

    public Given(TestMethods act) => _act = act;

    public Given And => this;

    public Given IOpenedLog(CombatLogBuilder log)
    {
        _act.OpenLog(log);
        return this;
    }

    public Given IExpandedEncounter(Boss boss)
    {
        _act.ToggleEncounter(boss);
        return this;
    }

    public Given IOpenedPull(Boss boss, int number)
    {
        _act.OpenPull(boss, number);
        return this;
    }
}

/// <summary>The one thing the scenario is about.</summary>
public sealed class When
{
    private readonly TestMethods _act;

    public When(TestMethods act) => _act = act;

    public When And => this;

    public When IToggleEncounter(Boss boss)
    {
        _act.ToggleEncounter(boss);
        return this;
    }

    public When ILookAtEncounter(Boss boss)
    {
        _act.LookAtEncounter(boss);
        return this;
    }

    public When ITogglePull(int number)
    {
        _act.TogglePull(number);
        return this;
    }

    public When ILookAtPull(int number)
    {
        _act.LookAtPull(number);
        return this;
    }

    public When ILookAtPlayer(string name)
    {
        _act.LookAtPlayer(name);
        return this;
    }

    public When ISortPlayersBy(PlayerColumn column)
    {
        _act.SortPlayersBy(column);
        return this;
    }
}
