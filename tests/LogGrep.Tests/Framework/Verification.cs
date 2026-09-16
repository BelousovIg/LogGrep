using LogGrep.Analysis;
using LogGrep.Models;
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


    /// <summary>Which mark the row carries next to the class: the tank shield, the healer cross, or none.</summary>
    public void PlayerRoleMarkIs(string expected)
    {
        string actual = _page.Player.IsTank ? "tank" : _page.Player.IsHealer ? "healer" : "none";
        Assert.True(expected == actual,
            $"'{_page.Player.Name}' should carry the {expected} mark, and carries {actual}.");
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
        => Assert.True(_page.Findings.Count == 0,
            "Nothing should have been found, but these were: " +
            string.Join(", ", _page.Findings.Select(f => f.Line)));

    public void MechanicBelongsTo(string spell, string role)
    {
        var found = First(spell);
        Assert.Equal(spell + " - " + role + " mechanic", found.Headline);
    }

    public void MechanicEvidenceReads(string spell, string expected)
        => Assert.Contains(expected, First(spell).Evidence, StringComparison.Ordinal);

    public void TookMechanicOutOfTurn(string spell, params string[] expected)
        => Assert.Equal(expected,
            _page.FindingsFor(spell).Select(f => PlayerName.Character(f.Player)).Distinct().ToArray());

    public void MechanicWasNotFlagged(string spell)
        => Assert.True(_page.FindingsFor(spell).Count == 0, $"'{spell}' should not have been flagged, but was.");

    public void FindingPointsAt(string spell, string player, string pull, string at)
    {
        var found = _page.FindingsFor(spell)
            .FirstOrDefault(f => PlayerName.Character(f.Player) == player)
            ?? throw new InvalidOperationException($"'{player}' was not reported for '{spell}'.");

        Assert.Equal(pull, "pull " + found.PullNumber);
        Assert.Equal(at, Display.Clock(found.At));
    }

    /// <summary>What a finding cost is what sorts the list, so the order is worth checking.</summary>
    public void FindingsAreOrderedByCost()
    {
        var weights = _page.Findings.Select(f => f.Cost.Weight).ToList();
        Assert.True(weights.SequenceEqual(weights.OrderByDescending(w => w)),
            "Findings should come heaviest first, and came in this order: " + string.Join(", ", weights));
    }

    public void FindingCost(string spell, string player, string expected)
    {
        var found = _page.FindingsFor(spell).First(f => PlayerName.Character(f.Player) == player);
        Assert.Equal(expected, found.Cost.Text);
    }

    public void FindingAdvises(string spell, string expected)
        => Assert.Contains(expected, First(spell).Advice, StringComparison.Ordinal);

    private Finding First(string spell)
        => _page.FindingsFor(spell).FirstOrDefault()
           ?? throw new InvalidOperationException(
               $"Nothing was found for '{spell}'. Findings: " +
               string.Join(", ", _page.Findings.Select(f => f.Line)));
}
