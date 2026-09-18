using LogGrep.Models;
using LogGrep.ViewModels;
using LogGrep.Tests.Logs;

namespace LogGrep.Tests.Framework;

/// <summary>
/// Everything a scenario does to the app. No assertions live here - those belong to
/// <see cref="Verification"/> - and nothing here reaches past the page object.
/// </summary>
public sealed class TestMethods
{
    private readonly LogGrepPage _page;

    public TestMethods(LogGrepPage page) => _page = page;

    public void OpenLog(params CombatLogBuilder[] logs) => _page.Open(logs);

    public void ToggleEncounter(Boss boss) => _page.ToggleEncounter(boss);

    public void LookAtEncounter(Boss boss) => _page.LookAtEncounter(boss);

    public void TogglePull(int number) => _page.TogglePull(number);

    public void LookAtPull(int number) => _page.LookAtPull(number);

    public void LookAtPlayer(string name) => _page.LookAtPlayer(name);

    public void SortPlayersBy(PlayerColumn column) => _page.SortPlayersBy(column);

    public void ReadTheFightFrom(TimeSpan from, TimeSpan to) => _page.ReadTheFightFrom(from, to);

    public void ReadTheWholeAttempt() => _page.ReadTheWholeAttempt();

    public void SwitchLine(string name, bool on) => _page.SwitchLine(name, on);

    public void RemoveLog(string name) => _page.RemoveLog(name);

    public void Reopen() => _page.Reopen();

    public void MarkAsOurs(string name, bool ours) => _page.MarkAsOurs(name, ours);

    public void OpenAlso(CombatLogBuilder log) => _page.OpenAlso(log);

    public void AnalyseEncounter(Boss boss) => _page.AnalyseEncounter(boss);

    public void AnalyseChosen(Boss boss, int[] numbers) => _page.AnalyseChosen(boss, numbers);

    public void NarrowTo(Outcome which) => _page.NarrowTo(which);

    public void ClimbTo(int depth) => _page.ViewModel.Analysis.GoTo(depth);

    public void GoBack() => _page.ViewModel.BackCommand.Execute(null);

    public void GoForward() => _page.ViewModel.ForwardCommand.Execute(null);

    public void LookAtPullInTheReport(int number) => _page.LookAtPullInTheReport(number);

    public void NarrowTo(Role role) => _page.NarrowTo(role);

    public void AnalysePull(Boss boss, int number) => _page.AnalysePull(boss, number);

    public void AnalysePlayer(Boss boss, int number, string player) => _page.AnalysePlayer(boss, number, player);

    public void GrowLog(string name, CombatLogBuilder more) => _page.GrowLog(name, more);

    public void DeleteFile(string name) => _page.DeleteFile(name);

    public void WriteRule(Ability spell, string roles, string advice) => _page.WriteRule(spell, roles, advice);

    /// <summary>Opens an encounter and one of its pulls in one go, which most scenarios want.</summary>
    public void OpenPull(Boss boss, int number)
    {
        ToggleEncounter(boss);
        TogglePull(number);
    }

    public void OpenPull(Dungeon dungeon, int number)
    {
        _page.ToggleEncounter(dungeon);
        TogglePull(number);
    }
}
