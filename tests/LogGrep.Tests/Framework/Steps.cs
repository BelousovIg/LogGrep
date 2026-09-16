using LogGrep.Tests.Logs;

namespace LogGrep.Tests.Framework;

/// <summary>
/// What was already true when the scenario starts. Every step returns the builder, so a setup of
/// several steps reads as one sentence:
/// <c>Given.IOpenedLog(log).And.IOpenedPull("Boss", 1);</c>
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

    public Given IOpenedLog(string path, string log)
    {
        _act.OpenLog(path, log);
        return this;
    }

    public Given IExpandedEncounter(string name)
    {
        _act.ToggleEncounter(name);
        return this;
    }

    public Given IOpenedPull(string encounter, int number)
    {
        _act.OpenPull(encounter, number);
        return this;
    }

    public Given ILookedAtPlayer(string name)
    {
        _act.LookAtPlayer(name);
        return this;
    }

    public Given IOpenedFindings()
    {
        _act.OpenFindings();
        return this;
    }
}

/// <summary>The one thing the scenario is about.</summary>
public sealed class When
{
    private readonly TestMethods _act;

    public When(TestMethods act) => _act = act;

    public When And => this;

    public When IToggleEncounter(string name)
    {
        _act.ToggleEncounter(name);
        return this;
    }

    public When ILookAtEncounter(string name)
    {
        _act.LookAtEncounter(name);
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

    public When ISortPlayersBy(string column)
    {
        _act.SortPlayersBy(column);
        return this;
    }

    public When IOpenFindings()
    {
        _act.OpenFindings();
        return this;
    }

    public When IIgnoreRule(string spell)
    {
        _act.IgnoreRule(spell);
        return this;
    }
}

/// <summary>What has to hold afterwards. Chains the same way the setup does.</summary>
public sealed class Then
{
    private readonly Verification _check;

    public Then(Verification check) => _check = check;

    public Then And => this;

    public Then EncountersAreListed(params string[] names)
    {
        _check.EncountersAreListed(names);
        return this;
    }

    public Then EncounterIsOpened(bool expected)
    {
        _check.EncounterIsOpened(expected);
        return this;
    }

    public Then EncounterCanBeOpened(bool expected)
    {
        _check.EncounterCanBeOpened(expected);
        return this;
    }

    public Then EncounterHasPulls(int expected)
    {
        _check.EncounterHasPulls(expected);
        return this;
    }

    public Then EncounterShowsDifficulty(string expected)
    {
        _check.EncounterShowsDifficulty(expected);
        return this;
    }

    public Then EncounterWasKilled(bool expected)
    {
        _check.EncounterWasKilled(expected);
        return this;
    }

    public Then PullIsOpened(bool expected)
    {
        _check.PullIsOpened(expected);
        return this;
    }

    public Then PullResultIs(string expected)
    {
        _check.PullResultIs(expected);
        return this;
    }

    public Then PullLasted(string expected)
    {
        _check.PullLasted(expected);
        return this;
    }

    public Then PullShowsPlayers(int expected)
    {
        _check.PullShowsPlayers(expected);
        return this;
    }

    public Then PlayersAreOrdered(params string[] expected)
    {
        _check.PlayersAreOrdered(expected);
        return this;
    }

    public Then PlayerIsShownAs(string className, string spec)
    {
        _check.PlayerIsShownAs(className, spec);
        return this;
    }


    public Then PlayerRoleMarkIs(string expected)
    {
        _check.PlayerRoleMarkIs(expected);
        return this;
    }
    public Then PlayerDpsIs(string expected)
    {
        _check.PlayerDpsIs(expected);
        return this;
    }

    public Then PlayerHpsIs(string expected)
    {
        _check.PlayerHpsIs(expected);
        return this;
    }

    public Then PlayerDtpsIs(string expected)
    {
        _check.PlayerDtpsIs(expected);
        return this;
    }

    public Then PlayerDeathsRead(string expected)
    {
        _check.PlayerDeathsRead(expected);
        return this;
    }

    public Then PlayerWasKilledBy(string expected)
    {
        _check.PlayerWasKilledBy(expected);
        return this;
    }

    public Then PlayerWasNotKilledBy(string spell)
    {
        _check.PlayerWasNotKilledBy(spell);
        return this;
    }


    public Then EncounterMistakesRead(string expected)
    {
        _check.EncounterMistakesRead(expected);
        return this;
    }

    public Then PullMistakesRead(string expected)
    {
        _check.PullMistakesRead(expected);
        return this;
    }

    public Then PlayerMistakesRead(string expected)
    {
        _check.PlayerMistakesRead(expected);
        return this;
    }

    public Then PlayerMistakesTooltipReads(params string[] lines)
    {
        _check.PlayerMistakesTooltipReads(lines);
        return this;
    }
    public Then FindingsRead(string expected)
    {
        _check.FindingsRead(expected);
        return this;
    }

    public Then NothingWasFound()
    {
        _check.NothingWasFound();
        return this;
    }

    public Then MechanicBelongsTo(string spell, string role)
    {
        _check.MechanicBelongsTo(spell, role);
        return this;
    }

    public Then MechanicEvidenceReads(string spell, string expected)
    {
        _check.MechanicEvidenceReads(spell, expected);
        return this;
    }

    public Then TookMechanicOutOfTurn(string spell, params string[] players)
    {
        _check.TookMechanicOutOfTurn(spell, players);
        return this;
    }

    public Then MechanicWasNotFlagged(string spell)
    {
        _check.MechanicWasNotFlagged(spell);
        return this;
    }

    public Then FindingPointsAt(string spell, string player, string pull, string at)
    {
        _check.FindingPointsAt(spell, player, pull, at);
        return this;
    }

    public Then RulesAreOrdered(params string[] spells)
    {
        _check.RulesAreOrdered(spells);
        return this;
    }

    public Then RuleIsIgnored(string spell, bool expected)
    {
        _check.RuleIsIgnored(spell, expected);
        return this;
    }
}
