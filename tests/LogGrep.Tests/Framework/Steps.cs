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

    /// <summary>Several nights at once, which is what the ten-attempt floor is usually waiting for.</summary>
    public Given IOpenedLogs(params CombatLogBuilder[] logs)
    {
        _act.OpenLog(logs);
        return this;
    }

    /// <summary>A rules file already sitting where the app looks for one.</summary>
    public Given ARulesFileSaying(Ability spell, string roles, string advice)
    {
        _act.WriteRule(spell, roles, advice);
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

    public When IRemoveTheLog(string name)
    {
        _act.RemoveLog(name);
        return this;
    }

    public When IMarkAsOurs(string name)
    {
        _act.MarkAsOurs(name, ours: true);
        return this;
    }

    public When IUnmarkAsOurs(string name)
    {
        _act.MarkAsOurs(name, ours: false);
        return this;
    }

    public When TheGameWritesMore(string name, CombatLogBuilder more)
    {
        _act.GrowLog(name, more);
        return this;
    }

    public When IAnalyseTheEncounter(Boss boss)
    {
        _act.AnalyseEncounter(boss);
        return this;
    }

    public When IAnalyseThePull(Boss boss, int number)
    {
        _act.AnalysePull(boss, number);
        return this;
    }

    public When IAnalyseThePlayer(Boss boss, int number, string player)
    {
        _act.AnalysePlayer(boss, number, player);
        return this;
    }

    public When IReopenTheApp()
    {
        _act.Reopen();
        return this;
    }

    public When IDeleteTheFile(string name)
    {
        _act.DeleteFile(name);
        return this;
    }

    public When ISortPlayersBy(PlayerColumn column)
    {
        _act.SortPlayersBy(column);
        return this;
    }
}
