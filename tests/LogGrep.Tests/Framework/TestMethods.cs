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

    /// <summary>Opens an encounter and one of its pulls in one go, which most scenarios want.</summary>
    public void OpenPull(Boss boss, int number)
    {
        ToggleEncounter(boss);
        TogglePull(number);
    }
}
