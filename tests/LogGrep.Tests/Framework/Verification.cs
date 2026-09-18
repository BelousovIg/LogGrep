using LogGrep.Analysis;
using LogGrep.Models;
using LogGrep.Tests.Logs;
using LogGrep.ViewModels;

namespace LogGrep.Tests.Framework;

/// <summary>
/// Every assertion in the suite. Failures say what was expected against what the app actually
/// shows, because a scenario that fails on "Assert.Equal(3, 2)" tells nobody anything.
///
/// Where an argument names something the log contains - a boss, a spell, a role, a moment, a
/// column - it is a type, so a scenario cannot ask about a spell it never wrote. Where an argument
/// is the app's own wording - a formatted rate, "wiped", "Holy" - it stays literal text, because
/// pinning that wording down is the whole reason the assertion exists, and rebuilding it from the
/// app's own formatter would only prove the formatter equals itself.
/// </summary>
public sealed class Verification
{
    private readonly LogGrepPage _page;

    public Verification(LogGrepPage page) => _page = page;

    public void EncountersAreListed(params Boss[] expected)
        => Assert.Equal(expected.Select(b => b.NameOf()).ToArray(), _page.Encounters.Select(e => e.Name).ToArray());


    public void EncountersAreListed(params Dungeon[] expected)
        => Assert.Equal(expected.Select(d => d.NameOf()).ToArray(), _page.Encounters.Select(e => e.Name).ToArray());

    /// <summary>Which difficulty each row landed under, which is what keeps two of them apart.</summary>
    public void EncounterDifficultiesAre(params string[] expected)
        => Assert.Equal(expected, _page.Encounters.Select(e => e.DifficultyText).ToArray());
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

    /// <summary>
    /// Which mark the row carries next to the class. Damage is the "no mark" answer rather than a
    /// separate word for it - the row shows a shield, a cross, or nothing, and nothing is what a
    /// damage dealer gets.
    /// </summary>
    public void PlayerRoleMarkIs(Role expected)
    {
        var actual = _page.Player.IsTank ? Role.Tank : _page.Player.IsHealer ? Role.Healer : Role.Damage;
        Assert.True(expected == actual,
            $"'{_page.Player.Name}' should carry the {Specs.NameOf(expected)} mark, " +
            $"and carries {Specs.NameOf(actual)}.");
    }

    public void PlayerDpsIs(string expected) => Assert.Equal(expected, _page.Player.DpsText);

    public void PlayerHpsIs(string expected) => Assert.Equal(expected, _page.Player.HpsText);

    public void PlayerDtpsIs(string expected) => Assert.Equal(expected, _page.Player.DtpsText);

    /// <summary>
    /// The cell shows the first death and counts the rest, because it is narrow and right-aligned
    /// and a full list ran off its left edge. Both halves are checked: what the cell says, and the
    /// tooltip that still has to carry every time.
    /// </summary>
    public void PlayerDiedAt(params TimeSpan[] expected)
    {
        string cell = expected.Length == 1
            ? Display.Clock(expected[0])
            : Display.Clock(expected[0]) + " +" + (expected.Length - 1);

        Assert.Equal(cell, _page.Player.DeathsText);
        Assert.Equal(string.Join(", ", expected.Select(Display.Clock)), _page.Player.DeathsTooltip);
    }

    public void PlayerDidNotDie()
    {
        Assert.Equal("-:--", _page.Player.DeathsText);
        Assert.Equal("This player did not die", _page.Player.DeathsTooltip);
    }


    public void LogsWereRead(int expected)
        => Assert.True(expected == _page.Reading.Sources.Count,
            $"The reading should hold {expected} files, it holds {_page.Reading.Sources.Count}: " +
            string.Join(", ", _page.Reading.Sources.Select(s => s.Name)));

    /// <summary>The files in the order the evening ran, which is what orders the attempts under it.</summary>
    public void LogsAreOrdered(params DateTime[] evenings)
        => Assert.Equal(evenings, _page.Reading.Sources.Select(s => s.Recorded.Date).ToArray());


    /// <summary>Which file each attempt in the open encounter was kept from.</summary>
    public void PullsCameFrom(params string[] expected)
        => Assert.Equal(expected, _page.Encounter.Pulls.Select(p => p.Record.Source.Name).Distinct().ToArray());
    public void PullsLasted(params TimeSpan[] expected)
        => Assert.Equal(expected, _page.Encounter.Pulls.Select(p => p.Record.Duration).ToArray());

    /// <summary>
    /// What the app says about the reading itself rather than about the fights. The wording is the
    /// app's own, so a scenario quotes the part of it that carries the meaning.
    /// </summary>
    public void ReadingNoted(string expected)
        => Assert.True(_page.Reading.Notes.Any(n => n.Contains(expected, StringComparison.Ordinal)),
            $"Nothing said '{expected}'. What was said: " +
            (_page.Reading.Notes.Count == 0 ? "nothing" : string.Join(" ", _page.Reading.Notes)));

    public void NothingWasRemarkedOn()
        => Assert.True(_page.Reading.Notes.Count == 0,
            "The reading should have passed without remark, and said: " +
            string.Join(" ", _page.Reading.Notes));
    public void PlayerWasKilledBy(Ability spell)
        => Assert.True(_page.Player.CausesText.Contains(spell.NameOf(), StringComparison.Ordinal),
            $"'{_page.Player.Name}' should have been killed by '{spell.NameOf()}'. " +
            $"What is shown: {_page.Player.CausesText}");


    /// <summary>
    /// The death breakdown naming a spell and saying the group gets out of it. Half the value of a
    /// breakdown is whether what killed you was yours to avoid.
    /// </summary>
    public void PlayerWasKilledByAvoidableDamage(Ability spell)
        => Assert.True(_page.Player.CausesText.Contains("(avoidable) " + spell.NameOf(), StringComparison.Ordinal),
            $"'{spell.NameOf()}' should have been marked avoidable in the death of " +
            $"'{_page.Player.Name}'. What is shown: {_page.Player.CausesText}");

    /// <summary>
    /// The mark has to open the cell, not trail off the end of it - the column is trimmed on the
    /// right, so a mark at the back is the first thing the reader loses.
    /// </summary>
    public void TheDeathBreakdownOpensWith(string expected)
        => Assert.StartsWith(expected, _page.Player.CausesText, StringComparison.Ordinal);
    public void PlayerWasNotKilledBy(Ability spell)
        => Assert.True(!_page.Player.CausesText.Contains(spell.NameOf(), StringComparison.Ordinal),
            $"'{spell.NameOf()}' should not be blamed for the death of '{_page.Player.Name}'. " +
            $"What is shown: {_page.Player.CausesText}");

    public void EncounterMistakesRead(int withMistakes, int ofPulls)
        => Assert.Equal(withMistakes + "/" + ofPulls, _page.Encounter.MistakesText);

    public void PullMistakesRead(int expected)
        => Assert.Equal(expected.ToString(), _page.Pull.MistakesText);

    public void PlayerMistakesRead(params AMistake[] expected)
        => Assert.Equal(string.Join("; ", expected.Select(m => m.Line)), _page.Player.MistakesText);

    public void PlayerHasNoMistakes() => Assert.Equal("—", _page.Player.MistakesText);

    /// <summary>Each mistake is a block of its own, separated by a blank line.</summary>
    public void PlayerMistakesTooltipReads(params AMistake[] expected)
        => Assert.Equal(
            string.Join(Environment.NewLine + Environment.NewLine, expected.Select(m => m.Block)),
            _page.Player.MistakesTooltip);

    public void FindingsSummaryCounts(int mistakes, int players)
        => Assert.Equal(
            $"{mistakes} {(mistakes == 1 ? "mistake" : "mistakes")} across " +
            $"{players} {(players == 1 ? "player." : "players.")}",
            _page.ViewModel.FindingsSummary);

    /// <summary>A stack count this fight turned out to stop forgiving.</summary>
    public void TheStackFindingReads(string player, string expected)
        => Assert.Equal(expected, Stacks(player).FirstOrDefault()?.Headline ??
            throw new InvalidOperationException("Nothing was said about " + player + "'s stacks. " + What()));

    public void TheStackEvidenceReads(string player, string expected)
        => Assert.Equal(expected, Stacks(player).First().Evidence);

    public void NothingWasSaidAboutStacks()
        => Assert.True(!_page.Findings.Any(f => f.Category == "stacks"),
            "Stacks should have gone unmentioned, and this was said: " +
            string.Join(", ", _page.Findings.Where(f => f.Category == "stacks").Select(f => f.Line)));

    private IEnumerable<Finding> Stacks(string player)
        => _page.Findings.Where(f => f.Category == "stacks" && PlayerName.Character(f.Player) == player);

    /// <summary>A talent change the numbers moved with.</summary>
    public void TheBuildFindingReads(string player, string expected)
        => Assert.Equal(expected, Builds(player).FirstOrDefault()?.Headline ??
            throw new InvalidOperationException("Nothing was said about " + player + "'s build. " + What()));

    public void TheBuildEvidenceReads(string player, string expected)
        => Assert.Equal(expected, Builds(player).First().Evidence);

    public void NothingWasSaidAboutTheBuild(string player)
        => Assert.True(!Builds(player).Any(),
            "Nothing should have been said about " + player + "'s build, and this was: " +
            string.Join(", ", Builds(player).Select(f => f.Headline)));

    private IEnumerable<Finding> Builds(string player)
        => _page.Findings.Where(f => f.Category == "builds" && PlayerName.Character(f.Player) == player);

    /// <summary>An attempt where a buff the player normally holds up was not held up.</summary>
    public void LostUptime(string player, Ability spell)
        => Assert.True(Uptime(player).Any(f => f.Headline.StartsWith(spell.NameOf(), StringComparison.Ordinal)),
            $"'{player}' should have been short on '{spell.NameOf()}'. " + What());

    public void KeptTheirUptime(string player)
        => Assert.True(!Uptime(player).Any(),
            $"'{player}' should have had nothing said about uptime, and got: " +
            string.Join(", ", Uptime(player).Select(f => f.Headline)));

    public void TheUptimeFindingReads(string player, string expected)
        => Assert.Equal(expected, Uptime(player).First().Headline);

    private IEnumerable<Finding> Uptime(string player)
        => _page.Findings.Where(f => f.Category == "uptime" && PlayerName.Character(f.Player) == player);

    /// <summary>An attempt where one of the player's own spells went out far less than it usually does.</summary>
    public void GotFewerUses(string player, Ability spell)
        => Assert.True(Cooldowns(player).Any(f => f.Headline.Contains(spell.NameOf(), StringComparison.Ordinal)),
            $"'{player}' should have been short of '{spell.NameOf()}'. " + What());

    public void GotNoCooldownFinding(string player)
        => Assert.True(!Cooldowns(player).Any(),
            $"'{player}' should have had nothing said about their cooldowns, and got: " +
            string.Join(", ", Cooldowns(player).Select(f => f.Headline)));

    public void TheCooldownFindingReads(string player, string expected)
        => Assert.Equal(expected, Cooldowns(player).First().Headline);

    public void TheCooldownEvidenceReads(string player, string expected)
        => Assert.Equal(expected, Cooldowns(player).First().Evidence);

    private IEnumerable<Finding> Cooldowns(string player)
        => _page.Findings.Where(f => f.Category == "cooldowns" && PlayerName.Character(f.Player) == player);

    /// <summary>An attempt where the app says this player stood about far more than they usually do.</summary>
    public void WasIdle(string player)
        => Assert.True(Idle(player).Any(),
            $"'{player}' should have been reported as idle. " + What());

    public void WasNotIdle(string player)
        => Assert.True(!Idle(player).Any(),
            $"'{player}' should not have been reported as idle, and was: " +
            string.Join(", ", Idle(player).Select(f => f.Line)));

    public void NobodyElseWasIdle(string player)
        => Assert.Equal(
            new[] { player },
            _page.Findings.Where(f => f.Category == "idle")
                .Select(f => PlayerName.Character(f.Player))
                .Distinct()
                .ToArray());

    public void IdleEvidenceMentions(string player, string expected)
        => Assert.Contains(expected, Idle(player).First().Evidence, StringComparison.Ordinal);

    private IEnumerable<Finding> Idle(string player)
        => _page.Findings.Where(f => f.Category == "idle" && PlayerName.Character(f.Player) == player);

    /// <summary>A death the app decided was that player's own rather than the attempt ending.</summary>
    public void ADeathWasReported(string player, TimeSpan at)
        => Assert.True(Deaths(player).Any(f => f.At == at),
            $"'{player}' should have a death reported at {Display.Clock(at)}. " + What());

    public void TheDeathReads(string player, string expected)
        => Assert.Equal(expected, TheDeath(player).Headline);

    public void DeathEvidenceReads(string player, string expected)
        => Assert.Equal(expected, TheDeath(player).Evidence);

    public void DeathEvidenceMentions(string player, string expected)
        => Assert.Contains(expected, TheDeath(player).Evidence, StringComparison.Ordinal);

    public void DeathAdvises(string player, string expected)
        => Assert.Contains(expected, TheDeath(player).Advice, StringComparison.Ordinal);

    private IEnumerable<Finding> Deaths(string player)
        => _page.Findings.Where(f => f.Category == "deaths" && PlayerName.Character(f.Player) == player);

    private Finding TheDeath(string player)
        => Deaths(player).FirstOrDefault()
           ?? throw new InvalidOperationException($"No death was reported for '{player}'. " + What());


    /// <summary>The files the window is holding, by name and in order.</summary>
    public void TheLogListHolds(params string[] expected)
        => Assert.Equal(expected, _page.ViewModel.Logs.Select(l => l.Name).ToArray());

    public void NothingIsListed()
        => Assert.True(_page.Encounters.Count == 0,
            "Nothing should be listed, and this is: " +
            string.Join(", ", _page.Encounters.Select(e => e.Name)));

    public void TheLogListIsEmpty()
        => Assert.True(_page.ViewModel.Logs.Count == 0,
            "The list should be empty, and holds: " +
            string.Join(", ", _page.ViewModel.Logs.Select(l => l.Name)));

    /// <summary>What a row says about its own file, which is empty until the file has been read.</summary>
    public void TheLogRowReads(string name, string pulls, string encounters, string started)
    {
        var row = Row(name);
        Assert.Equal(pulls, row.PullsText);
        Assert.Equal(encounters, row.EncountersText);
        Assert.Equal(started, row.StartedText);
    }

    public void TheLogRowSaysItIsGone(string name)
        => Assert.Equal("file is gone", Row(name).StartedText);

    private LogRowViewModel Row(string name)
        => _page.ViewModel.Logs.FirstOrDefault(l => l.Name == name)
           ?? throw new InvalidOperationException(
               "No log called '" + name + "' is open. These are: " +
               string.Join(", ", _page.ViewModel.Logs.Select(l => l.Name)));
    /// <summary>A fact about the evening rather than about one attempt.</summary>
    public void TheNightSays(string player, string expected)
        => Assert.True(Nightly(player).Any(h => h == expected),
            "The evening should have said '" + expected + "' about " + player + ". It said: " +
            (Nightly(player).Count == 0 ? "nothing" : string.Join("; ", Nightly(player))));

    public void TheNightDoesNotSay(string player, string fragment)
        => Assert.True(!Nightly(player).Any(h => h.Contains(fragment, StringComparison.Ordinal)),
            "The evening should not have mentioned '" + fragment + "' for " + player + ". It said: " +
            string.Join("; ", Nightly(player)));

    private List<string> Nightly(string player)
        => _page.Findings
            .Where(f => f.Category == "the night" && PlayerName.Character(f.Player) == player)
            .Select(f => f.Headline)
            .ToList();

    /// <summary>A fact about the evening that belongs to nobody in particular.</summary>
    public void TheNightSaysOfNobody(string expected)
        => Assert.Equal(expected, _page.Findings
            .FirstOrDefault(f => f.Category == "the night" && f.Player.Length == 0)
            ?.Headline ?? "nothing about the evening");

    public void TheNightSaysNothing()
        => Assert.True(!_page.Findings.Any(f => f.Category == "the night"),
            "The evening should have gone unremarked, and this was said: " +
            string.Join(", ", _page.Findings.Where(f => f.Category == "the night").Select(f => f.Headline)));

    /// <summary>
    /// Nothing was found that accuses anybody. Facts about the evening - who led, who opened it -
    /// are not mistakes, and a scenario about mechanics or rotation should not have to care that
    /// somebody also happened to top the healing.
    /// </summary>
    /// <summary>What the app says about its own rules file rather than about anybody's play.</summary>
    public void TheRulesSay(string expected)
        => Assert.Equal(expected, _page.Findings.FirstOrDefault(f => f.Category == "rules")?.Headline
            ?? "nothing about the rules");

    public void TheRulesSayNothing()
        => Assert.True(!_page.Findings.Any(f => f.Category == "rules"),
            "The rules should have passed without remark, and this was said: " +
            string.Join("; ", _page.Findings.Where(f => f.Category == "rules").Select(f => f.Headline)));

    /// <summary>What the app says about how the fight began.</summary>
    public void ThePullSays(string player, string expected)
        => Assert.True(Pulls(player).Any(f => f.Headline == expected),
            "The pull should have said '" + expected + "' about " + player + ". It said: " +
            (Pulls(player).Count == 0 ? "nothing" : string.Join("; ", Pulls(player).Select(f => f.Headline))));

    public void ThePullWasClean()
        => Assert.True(!_page.Findings.Any(f => f.Category == "the pull"),
            "The pull should have opened cleanly, and this was said: " +
            string.Join("; ", _page.Findings.Where(f => f.Category == "the pull").Select(f => f.Headline)));

    public void ThePullCost(string player, string expected)
        => Assert.Equal(expected, Pulls(player).First().Cost.Text);

    private List<Finding> Pulls(string player)
        => _page.Findings
            .Where(f => f.Category == "the pull" && PlayerName.Character(f.Player) == player)
            .ToList();

    public void NothingWasFound()
    {
        var blame = _page.Findings.Where(f => f.Category != "the night").ToList();
        Assert.True(blame.Count == 0,
            "Nothing should have been found, but these were: " + string.Join(", ", blame.Select(f => f.Line)));
    }

    public void MechanicBelongsTo(Ability spell, Role owner)
        => Assert.Equal(spell.NameOf() + " - " + Specs.NameOf(owner) + " mechanic", First(spell).Headline);

    public void MechanicEvidenceReads(Ability spell, string expected)
        => Assert.Contains(expected, First(spell).Evidence, StringComparison.Ordinal);

    public void TookMechanicOutOfTurn(Ability spell, params string[] expected)
        => Assert.Equal(expected,
            _page.FindingsFor(spell).Select(f => PlayerName.Character(f.Player)).Distinct().ToArray());

    /// <summary>
    /// Nobody was accused over this spell. What the app says about its own rules file is not an
    /// accusation, so it does not count here - and a muted rule says exactly that.
    /// </summary>
    public void MechanicWasNotFlagged(Ability spell)
    {
        var blamed = _page.FindingsFor(spell).Where(f => f.Category == "mechanics").ToList();
        Assert.True(blamed.Count == 0,
            "'" + spell.NameOf() + "' should not have been flagged, and was: " +
            string.Join("; ", blamed.Select(f => f.Line)));
    }

    public void FindingPointsAt(Ability spell, string player, int pull, TimeSpan at)
    {
        var found = Found(spell, player);

        Assert.Equal(pull, found.PullNumber);
        Assert.Equal(at, found.At);
    }

    /// <summary>What a finding cost is what sorts the list, so the order is worth checking.</summary>
    public void FindingsAreOrderedByCost()
    {
        var weights = _page.Findings.Select(f => f.Cost.Weight).ToList();
        Assert.True(weights.SequenceEqual(weights.OrderByDescending(w => w)),
            "Findings should come heaviest first, and came in this order: " + string.Join(", ", weights));
    }


    /// <summary>Damage the group is getting out of, which is a finding of its own kind.</summary>
    public void DamageWasAvoidable(Ability spell)
        => Assert.Equal(spell.NameOf() + " - avoidable", First(spell).Headline);



    /// <summary>The finding exists but carries no name, because the log cannot honestly give one.</summary>
    public void NobodyWasNamedFor(Ability spell)
        => Assert.True(_page.FindingsFor(spell).All(f => f.Player.Length == 0),
            $"'{spell.NameOf()}' should have been reported without a name, and named: " +
            string.Join(", ", _page.FindingsFor(spell).Select(f => f.Player).Where(p => p.Length > 0)));
    /// <summary>A cast that went off where this group usually stops it.</summary>
    public void InterruptWasMissed(Ability spell)
        => Assert.Equal(spell.NameOf() + " - interrupt missed", First(spell).Headline);
    public void MistakeCost(Ability spell, string player, string expected)
        => Assert.Equal(expected, Found(spell, player).Cost.Text);

    public void NothingWasFoundFor(Ability spell)
        => Assert.True(_page.FindingsFor(spell).Count == 0,
            $"'{spell.NameOf()}' should not have been flagged, but was. " + What());
    public void MistakeKilled(Ability spell, string player, TimeSpan at)
        => Assert.Equal(spell.NameOf() + " killed you at " + Display.Clock(at), Found(spell, player).Cost.Text);

    public void MistakeWasSurvived(Ability spell, string player)
        => Assert.Equal("survived it", Found(spell, player).Cost.Text);

    public void FindingAdvises(Ability spell, string expected)
        => Assert.Contains(expected, First(spell).Advice, StringComparison.Ordinal);

    private Finding Found(Ability spell, string player)
        => _page.FindingsFor(spell).FirstOrDefault(f => PlayerName.Character(f.Player) == player)
           ?? throw new InvalidOperationException(
               $"'{player}' was not reported for '{spell.NameOf()}'. " + What());

    private Finding First(Ability spell)
        => _page.FindingsFor(spell).FirstOrDefault()
           ?? throw new InvalidOperationException(
               $"Nothing was found for '{spell.NameOf()}'. " + What());

    private string What() => "Findings: " + string.Join(", ", _page.Findings.Select(f => f.Line));

    // The registry of people.

    /// <summary>Everybody the loaded logs have seen, most attempts first.</summary>
    public void PeopleAreListed(params string[] expected)
        => Assert.Equal(expected, _page.People.Select(p => p.Name).ToArray());

    /// <summary>Which realm the registry has them on, since a transfer changes it and not them.</summary>
    public void PersonIsOn(string name, string realm)
        => Assert.Equal(realm, _page.Person(name).Realm);

    /// <summary>Every role they were seen in. Two of them is a respec, not a mistake in the data.</summary>
    public void PersonPlayed(string name, string expected)
        => Assert.Equal(expected, _page.Person(name).RolesText);

    public void PersonWasInPulls(string name, string expected)
        => Assert.Equal(expected, _page.Person(name).PullsText);

    public void PersonIsOurs(string name, bool expected)
        => Assert.True(expected == _page.Person(name).IsOurs,
            name + " should " + (expected ? "" : "not ") + "be marked as ours.");

    /// <summary>Who an analysis would be about: those marked, or everybody when none are.</summary>
    public void OursAre(params string[] expected)
        => Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
            _page.People.Where(p => _page.ViewModel.Ours.Contains(p.Id))
                .Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());

    public void TheRegistrySays(string expected) => Assert.Equal(expected, _page.ViewModel.PeopleSummary);

    /// <summary>How many attempts the analysis counts, which is not every attempt the list shows.</summary>
    public void TheAttemptsJudgedAre(int expected)
    {
        var judged = new LogGrep.Analysis.Attempts(_page.Encounter.Pulls.Select(p => p.Record).ToList());
        Assert.True(expected == judged.Pulls.Count,
            $"The analysis should judge {expected} attempts and judges {judged.Pulls.Count} of {_page.Encounter.Pulls.Count} listed.");
    }

    // The card, the lane, and what the attempt cost.

    /// <summary>One of the four axes on a player row, exactly as the cell prints it.</summary>
    public void PlayerScores(Axis axis, string expected)
        => Assert.True(expected == Cell(axis),
            $"{_page.Player.Name} should score {expected} for {axis} and scores {Cell(axis)}. " + Facts(axis));

    /// <summary>What the number is built from, which is the half of a score that stops an argument.</summary>
    public void PlayerScoreSays(Axis axis, string expected)
        => Assert.True(Facts(axis).Contains(expected, StringComparison.Ordinal),
            $"The {axis} tooltip should have said '{expected}'. It says: {Facts(axis)}");

    /// <summary>The one sentence the row shows: the most expensive thing that happened to them.</summary>
    public void PlayerWorstIs(string expected) => Assert.Equal(expected, _page.Player.WorstText);

    public void PlayerLaneHas(int marks)
        => Assert.True(marks == _page.Player.Marks.Count,
            $"{_page.Player.Name} should have {marks} marks and has {_page.Player.Marks.Count}: " + Lane());

    /// <summary>A mark at a moment, and what hovering it would say.</summary>
    public void PlayerLaneShows(TimeSpan at, string expected)
        => Assert.True(_page.Player.Marks.Any(m => Math.Abs(m.At - at.TotalSeconds) < 1 && m.Text.Contains(expected, StringComparison.Ordinal)),
            $"The lane should carry '{expected}' at {Display.Clock(at)}. It carries: " + Lane());

    /// <summary>Whether a mark was drawn as one thing that caught the group rather than one person.</summary>
    public void PlayerLaneSharedIt(TimeSpan at, bool expected)
        => Assert.True(expected == _page.Player.Marks.Any(m => Math.Abs(m.At - at.TotalSeconds) < 1 && m.Collective),
            $"The mark at {Display.Clock(at)} should {(expected ? "" : "not ")}read as the group's. Lane: " + Lane());

    public void TheEnemyLaneHas(int casts)
        => Assert.True(casts == _page.Pull.EnemyMarks.Count,
            $"The enemy lane should carry {casts} casts and carries {_page.Pull.EnemyMarks.Count}.");

    public void TheEnemyLaneShows(TimeSpan at, string expected)
        => Assert.True(_page.Pull.EnemyMarks.Any(m => Math.Abs(m.At - at.TotalSeconds) < 1 && m.Text.Contains(expected, StringComparison.Ordinal)),
            $"The enemy lane should carry '{expected}' at {Display.Clock(at)}. It carries: " +
            string.Join("; ", _page.Pull.EnemyMarks.Select(m => m.Text)));

    /// <summary>What the attempt cost everybody in it, in their own health pools.</summary>
    public void TheAttemptCost(string expected) => Assert.Equal(expected, _page.Pull.PoolsText);

    /// <summary>Who it went to. Named rather than averaged, which is the whole point of the line.</summary>
    public void TheAttemptBlames(string player, string pools)
    {
        var line = _page.Pull.Blame.FirstOrDefault(b => b.Name == player)
            ?? throw new InvalidOperationException(
                $"'{player}' was not blamed for anything. Blamed: " +
                string.Join(", ", _page.Pull.Blame.Select(b => b.Name + " " + b.PoolsText)));

        Assert.Equal(pools, line.PoolsText);
    }

    public void TheRaidScores(Axis axis, string expected)
        => Assert.Equal(expected, _page.Pull.Axes.First(s => s.Axis == axis).Text);

    private string Cell(Axis axis) => axis switch
    {
        Axis.Output => _page.Player.OutputText,
        Axis.Survival => _page.Player.SurvivalText,
        Axis.Mechanics => _page.Player.MechanicsText,
        _ => _page.Player.DutyText,
    };

    private string Facts(Axis axis) => axis switch
    {
        Axis.Output => _page.Player.OutputTooltip,
        Axis.Survival => _page.Player.SurvivalTooltip,
        Axis.Mechanics => _page.Player.MechanicsTooltip,
        _ => _page.Player.DutyTooltip,
    };

    private string Lane()
        => _page.Player.Marks.Count == 0
            ? "nothing"
            : string.Join("; ", _page.Player.Marks.Select(m => m.Text));
}
