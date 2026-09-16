using LogGrep.ViewModels;

namespace LogGrep.Tests.Framework;

/// <summary>
/// Every assertion in the suite. Failures say what was expected against what the app actually
/// shows, because a scenario that fails on "Assert.Equal(3, 2)" tells nobody anything.
/// </summary>
public sealed class Verification
{
    private readonly LogGrepPage _page;

    public Verification(LogGrepPage page) => _page = page;

    public void EncountersAreListed(params string[] expected)
        => Assert.Equal(expected, _page.Encounters.Select(e => e.Name).ToArray());

    public void EncounterIsOpened(bool expected)
        => Assert.True(expected == _page.Encounter.IsExpanded,
            $"'{_page.Encounter.Name}' should be {(expected ? "open" : "closed")} and is not.");

    public void EncounterCanBeOpened(bool expected)
        => Assert.True(expected == _page.Encounter.IsExpandable,
            $"'{_page.Encounter.Name}' should {(expected ? "" : "not ")}be expandable.");

    public void EncounterHasPulls(int expected)
        => Assert.True(expected == _page.Encounter.Pulls.Count,
            $"'{_page.Encounter.Name}' should have {expected} pulls, it has {_page.Encounter.Pulls.Count}.");

    public void EncounterShowsDifficulty(string expected)
        => Assert.Equal(expected, _page.Encounter.DifficultyText);

    public void EncounterWasKilled(bool expected)
        => Assert.True(expected == _page.Encounter.HasKill,
            $"'{_page.Encounter.Name}' should read as {(expected ? "killed" : "wiped")}.");

    public void PullIsOpened(bool expected)
        => Assert.True(expected == _page.Pull.IsExpanded, "The pull should be " + (expected ? "open." : "closed."));

    public void PullResultIs(string expected) => Assert.Equal(expected, _page.Pull.ResultText);

    public void PullLasted(string expected) => Assert.Equal(expected, _page.Pull.DurationText);

    public void PullShowsPlayers(int expected)
        => Assert.True(expected == _page.Players().Count,
            $"The pull should show {expected} players, it shows {_page.Players().Count}.");

    public void PlayersAreOrdered(params string[] expected)
        => Assert.Equal(expected, _page.Players().Select(p => p.Name).ToArray());

    public void PlayerIsShownAs(string className, string spec)
    {
        Assert.Equal(className, _page.Player.ClassName);
        Assert.Equal(spec, _page.Player.SpecName);
    }

    public void PlayerDpsIs(string expected) => Assert.Equal(expected, _page.Player.DpsText);

    public void PlayerHpsIs(string expected) => Assert.Equal(expected, _page.Player.HpsText);

    public void PlayerDtpsIs(string expected) => Assert.Equal(expected, _page.Player.DtpsText);

    public void PlayerDeathsRead(string expected) => Assert.Equal(expected, _page.Player.DeathsText);

    public void PlayerWasKilledBy(string expected)
        => Assert.True(_page.Player.CausesText.Contains(expected, StringComparison.Ordinal),
            $"'{_page.Player.Name}' should have been killed by '{expected}'. " +
            $"What is shown: {_page.Player.CausesText}");

    public void PlayerWasNotKilledBy(string spell)
        => Assert.True(!_page.Player.CausesText.Contains(spell, StringComparison.Ordinal),
            $"'{spell}' should not be blamed for the death of '{_page.Player.Name}'. " +
            $"What is shown: {_page.Player.CausesText}");


    public void EncounterMistakesRead(string expected)
        => Assert.Equal(expected, _page.Encounter.MistakesText);

    public void PullMistakesRead(string expected)
        => Assert.Equal(expected, _page.Pull.MistakesText);

    public void PlayerMistakesRead(string expected)
        => Assert.Equal(expected, _page.Player.MistakesText);

    /// <summary>
    /// Each mistake is a block of its own, so blocks are given separately and the newline inside
    /// one is written as \n - a scenario should not have to spell out what the platform calls a
    /// line break.
    /// </summary>
    public void PlayerMistakesTooltipReads(params string[] blocks)
        => Assert.Equal(
            string.Join(
                Environment.NewLine + Environment.NewLine,
                blocks.Select(block => block.Replace("\n", Environment.NewLine, StringComparison.Ordinal))),
            _page.Player.MistakesTooltip);

    public void FindingsRead(string expected) => Assert.Contains(expected, _page.ViewModel.FindingsSummary);

    public void NothingWasFound()
        => Assert.True(_page.Rules.Count == 0,
            "Nothing should have been found, but these were: " +
            string.Join(", ", _page.Rules.Select(r => r.Rule.Spell)));

    public void MechanicBelongsTo(string spell, string role)
        => Assert.Equal(role, LogGrep.Models.Specs.NameOf(_page.Rule(spell).Rule.Owner));

    public void MechanicEvidenceReads(string spell, string expected)
        => Assert.Equal(expected, _page.Rule(spell).Rule.Evidence);

    public void TookMechanicOutOfTurn(string spell, params string[] expected)
        => Assert.Equal(expected, _page.Rule(spell).Findings.Select(f => f.Player).Distinct().ToArray());

    public void MechanicWasNotFlagged(string spell)
        => Assert.True(_page.Rules.All(r => r.Rule.Spell != spell),
            $"'{spell}' should not have been flagged, but it was.");

    public void FindingPointsAt(string spell, string player, string pull, string at)
    {
        var finding = _page.Rule(spell).Findings.FirstOrDefault(f => f.Player == player)
            ?? throw new InvalidOperationException($"'{player}' was not reported for '{spell}'.");

        Assert.Equal(pull, finding.PullText);
        Assert.Equal(at, finding.AtText);
    }

    public void RulesAreOrdered(params string[] expected)
        => Assert.Equal(expected, _page.ViewModel.RulesView.Cast<RuleViewModel>().Select(r => r.Rule.Spell).ToArray());

    public void RuleIsIgnored(string spell, bool expected)
        => Assert.True(expected == _page.Rule(spell).IsIgnored, $"'{spell}' ignored should be {expected}.");
}
