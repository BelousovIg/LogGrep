using LogGrep.Analysis;
using LogGrep.Models;
using LogGrep.Tests.Logs;

namespace LogGrep.Tests.Framework;

/// <summary>What has to hold afterwards. Chains the same way the setup does.</summary>
public sealed class Then
{
    private readonly Verification _check;

    public Then(Verification check) => _check = check;

    public Then And => this;

    public Then EncountersAreListed(params Boss[] bosses)
    {
        _check.EncountersAreListed(bosses);
        return this;
    }

    public Then EncountersAreListed(params Dungeon[] dungeons)
    {
        _check.EncountersAreListed(dungeons);
        return this;
    }

    public Then EncounterDifficultiesAre(params string[] expected)
    {
        _check.EncounterDifficultiesAre(expected);
        return this;
    }

    public Then PullsCameFrom(params string[] expected)
    {
        _check.PullsCameFrom(expected);
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

    public Then PlayerRoleMarkIs(Role expected)
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

    public Then PlayerDiedAt(params TimeSpan[] expected)
    {
        _check.PlayerDiedAt(expected);
        return this;
    }

    public Then PlayerDidNotDie()
    {
        _check.PlayerDidNotDie();
        return this;
    }

    public Then LogsWereRead(int expected)
    {
        _check.LogsWereRead(expected);
        return this;
    }

    public Then LogsAreOrdered(params DateTime[] evenings)
    {
        _check.LogsAreOrdered(evenings);
        return this;
    }

    public Then PullsLasted(params TimeSpan[] expected)
    {
        _check.PullsLasted(expected);
        return this;
    }

    public Then ReadingNoted(string expected)
    {
        _check.ReadingNoted(expected);
        return this;
    }

    public Then NothingWasRemarkedOn()
    {
        _check.NothingWasRemarkedOn();
        return this;
    }

    public Then PlayerWasKilledBy(Ability spell)
    {
        _check.PlayerWasKilledBy(spell);
        return this;
    }

    public Then PlayerWasKilledByAvoidableDamage(Ability spell)
    {
        _check.PlayerWasKilledByAvoidableDamage(spell);
        return this;
    }

    public Then TheDeathBreakdownOpensWith(string expected)
    {
        _check.TheDeathBreakdownOpensWith(expected);
        return this;
    }

    public Then PlayerWasNotKilledBy(Ability spell)
    {
        _check.PlayerWasNotKilledBy(spell);
        return this;
    }

    public Then EncounterMistakesRead(int withMistakes, int ofPulls)
    {
        _check.EncounterMistakesRead(withMistakes, ofPulls);
        return this;
    }

    public Then PullMistakesRead(int expected)
    {
        _check.PullMistakesRead(expected);
        return this;
    }

    public Then PlayerMistakesRead(params AMistake[] expected)
    {
        _check.PlayerMistakesRead(expected);
        return this;
    }

    public Then PlayerHasNoMistakes()
    {
        _check.PlayerHasNoMistakes();
        return this;
    }

    public Then PlayerMistakesTooltipReads(params AMistake[] expected)
    {
        _check.PlayerMistakesTooltipReads(expected);
        return this;
    }

    public Then FindingsSummaryCounts(int mistakes, int players)
    {
        _check.FindingsSummaryCounts(mistakes, players);
        return this;
    }

    public Then TheStackFindingReads(string player, string expected)
    {
        _check.TheStackFindingReads(player, expected);
        return this;
    }

    public Then TheStackEvidenceReads(string player, string expected)
    {
        _check.TheStackEvidenceReads(player, expected);
        return this;
    }

    public Then NothingWasSaidAboutStacks()
    {
        _check.NothingWasSaidAboutStacks();
        return this;
    }

    public Then TheBuildFindingReads(string player, string expected)
    {
        _check.TheBuildFindingReads(player, expected);
        return this;
    }

    public Then TheBuildEvidenceReads(string player, string expected)
    {
        _check.TheBuildEvidenceReads(player, expected);
        return this;
    }

    public Then NothingWasSaidAboutTheBuild(string player)
    {
        _check.NothingWasSaidAboutTheBuild(player);
        return this;
    }

    public Then LostUptime(string player, Ability spell)
    {
        _check.LostUptime(player, spell);
        return this;
    }

    public Then KeptTheirUptime(string player)
    {
        _check.KeptTheirUptime(player);
        return this;
    }

    public Then TheUptimeFindingReads(string player, string expected)
    {
        _check.TheUptimeFindingReads(player, expected);
        return this;
    }

    public Then GotFewerUses(string player, Ability spell)
    {
        _check.GotFewerUses(player, spell);
        return this;
    }

    public Then GotNoCooldownFinding(string player)
    {
        _check.GotNoCooldownFinding(player);
        return this;
    }

    public Then TheCooldownFindingReads(string player, string expected)
    {
        _check.TheCooldownFindingReads(player, expected);
        return this;
    }

    public Then TheCooldownEvidenceReads(string player, string expected)
    {
        _check.TheCooldownEvidenceReads(player, expected);
        return this;
    }

    public Then WasIdle(string player)
    {
        _check.WasIdle(player);
        return this;
    }

    public Then WasNotIdle(string player)
    {
        _check.WasNotIdle(player);
        return this;
    }

    public Then NobodyElseWasIdle(string player)
    {
        _check.NobodyElseWasIdle(player);
        return this;
    }

    public Then IdleEvidenceMentions(string player, string expected)
    {
        _check.IdleEvidenceMentions(player, expected);
        return this;
    }

    public Then ADeathWasReported(string player, TimeSpan at)
    {
        _check.ADeathWasReported(player, at);
        return this;
    }

    public Then TheDeathReads(string player, string expected)
    {
        _check.TheDeathReads(player, expected);
        return this;
    }

    public Then DeathEvidenceReads(string player, string expected)
    {
        _check.DeathEvidenceReads(player, expected);
        return this;
    }

    public Then DeathEvidenceMentions(string player, string expected)
    {
        _check.DeathEvidenceMentions(player, expected);
        return this;
    }

    public Then DeathAdvises(string player, string expected)
    {
        _check.DeathAdvises(player, expected);
        return this;
    }

    public Then TheLogListHolds(params string[] expected)
    {
        _check.TheLogListHolds(expected);
        return this;
    }

    public Then NothingIsListed()
    {
        _check.NothingIsListed();
        return this;
    }

    public Then TheLogListIsEmpty()
    {
        _check.TheLogListIsEmpty();
        return this;
    }

    public Then TheLogRowReads(string name, string pulls, string encounters, string started)
    {
        _check.TheLogRowReads(name, pulls, encounters, started);
        return this;
    }

    public Then TheLogRowSaysItIsGone(string name)
    {
        _check.TheLogRowSaysItIsGone(name);
        return this;
    }

    public Then TheNightSays(string player, string expected)
    {
        _check.TheNightSays(player, expected);
        return this;
    }

    public Then TheNightDoesNotSay(string player, string fragment)
    {
        _check.TheNightDoesNotSay(player, fragment);
        return this;
    }

    public Then TheNightSaysOfNobody(string expected)
    {
        _check.TheNightSaysOfNobody(expected);
        return this;
    }

    public Then TheNightSaysNothing()
    {
        _check.TheNightSaysNothing();
        return this;
    }

    public Then TheRulesSay(string expected)
    {
        _check.TheRulesSay(expected);
        return this;
    }

    public Then TheRulesSayNothing()
    {
        _check.TheRulesSayNothing();
        return this;
    }

    public Then ThePullSays(string player, string expected)
    {
        _check.ThePullSays(player, expected);
        return this;
    }

    public Then ThePullWasClean()
    {
        _check.ThePullWasClean();
        return this;
    }

    public Then ThePullCost(string player, string expected)
    {
        _check.ThePullCost(player, expected);
        return this;
    }

    public Then NothingWasFound()
    {
        _check.NothingWasFound();
        return this;
    }

    public Then MechanicBelongsTo(Ability spell, Role owner)
    {
        _check.MechanicBelongsTo(spell, owner);
        return this;
    }

    public Then MechanicEvidenceReads(Ability spell, string expected)
    {
        _check.MechanicEvidenceReads(spell, expected);
        return this;
    }

    public Then TookMechanicOutOfTurn(Ability spell, params string[] players)
    {
        _check.TookMechanicOutOfTurn(spell, players);
        return this;
    }

    public Then MechanicWasNotFlagged(Ability spell)
    {
        _check.MechanicWasNotFlagged(spell);
        return this;
    }

    public Then FindingPointsAt(Ability spell, string player, int pull, TimeSpan at)
    {
        _check.FindingPointsAt(spell, player, pull, at);
        return this;
    }

    /// <summary>The order the report is read in, which is the order the findings are given in.</summary>
    public Then FindingsAreOrderedByCost()
    {
        _check.FindingsAreOrderedByCost();
        return this;
    }

    public Then DamageWasAvoidable(Ability spell)
    {
        _check.DamageWasAvoidable(spell);
        return this;
    }

    public Then NobodyWasNamedFor(Ability spell)
    {
        _check.NobodyWasNamedFor(spell);
        return this;
    }

    public Then InterruptWasMissed(Ability spell)
    {
        _check.InterruptWasMissed(spell);
        return this;
    }

    public Then MistakeCost(Ability spell, string player, string expected)
    {
        _check.MistakeCost(spell, player, expected);
        return this;
    }

    public Then NothingWasFoundFor(Ability spell)
    {
        _check.NothingWasFoundFor(spell);
        return this;
    }

    public Then MistakeKilled(Ability spell, string player, TimeSpan at)
    {
        _check.MistakeKilled(spell, player, at);
        return this;
    }

    public Then MistakeWasSurvived(Ability spell, string player)
    {
        _check.MistakeWasSurvived(spell, player);
        return this;
    }

    public Then FindingAdvises(Ability spell, string expected)
    {
        _check.FindingAdvises(spell, expected);
        return this;
    }

    public Then PeopleAreListed(params string[] expected)
    {
        _check.PeopleAreListed(expected);
        return this;
    }

    public Then PersonIsOn(string name, string realm)
    {
        _check.PersonIsOn(name, realm);
        return this;
    }

    public Then PersonPlayed(string name, string expected)
    {
        _check.PersonPlayed(name, expected);
        return this;
    }

    public Then PersonWasInPulls(string name, string expected)
    {
        _check.PersonWasInPulls(name, expected);
        return this;
    }

    public Then PersonIsOurs(string name, bool expected)
    {
        _check.PersonIsOurs(name, expected);
        return this;
    }

    public Then OursAre(params string[] expected)
    {
        _check.OursAre(expected);
        return this;
    }

    public Then TheRegistrySays(string expected)
    {
        _check.TheRegistrySays(expected);
        return this;
    }

    public Then PlayerScores(Axis axis, string expected)
    {
        _check.PlayerScores(axis, expected);
        return this;
    }

    public Then PlayerScoreSays(Axis axis, string expected)
    {
        _check.PlayerScoreSays(axis, expected);
        return this;
    }

    public Then PlayerWorstIs(string expected)
    {
        _check.PlayerWorstIs(expected);
        return this;
    }

    public Then PlayerLaneHas(int marks)
    {
        _check.PlayerLaneHas(marks);
        return this;
    }

    public Then PlayerLaneShows(TimeSpan at, string expected)
    {
        _check.PlayerLaneShows(at, expected);
        return this;
    }

    public Then PlayerLaneSharedIt(TimeSpan at, bool expected)
    {
        _check.PlayerLaneSharedIt(at, expected);
        return this;
    }

    public Then TheEnemyLaneHas(int casts)
    {
        _check.TheEnemyLaneHas(casts);
        return this;
    }

    public Then TheEnemyLaneShows(TimeSpan at, string expected)
    {
        _check.TheEnemyLaneShows(at, expected);
        return this;
    }

    public Then TheAttemptCost(string expected)
    {
        _check.TheAttemptCost(expected);
        return this;
    }

    public Then TheAttemptBlames(string player, string pools)
    {
        _check.TheAttemptBlames(player, pools);
        return this;
    }

    public Then TheRaidScores(Axis axis, string expected)
    {
        _check.TheRaidScores(axis, expected);
        return this;
    }
}
