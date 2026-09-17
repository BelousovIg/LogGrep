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

    public Then DeathAdvises(string player, string expected)
    {
        _check.DeathAdvises(player, expected);
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
}
