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

    public void OpenLog(CombatLogBuilder log) => _page.Open(log);

    public void OpenLog(string path, string log) => _page.Open(path, log);

    public void ToggleEncounter(string name) => _page.ToggleEncounter(name);

    public void LookAtEncounter(string name) => _page.LookAtEncounter(name);

    public void TogglePull(int number) => _page.TogglePull(number);

    public void LookAtPull(int number) => _page.LookAtPull(number);

    public void LookAtPlayer(string name) => _page.LookAtPlayer(name);

    public void SortPlayersBy(string column) => _page.SortPlayersBy(column);

    /// <summary>Opens an encounter and one of its pulls in one go, which most scenarios want.</summary>
    public void OpenPull(string encounter, int number)
    {
        ToggleEncounter(encounter);
        TogglePull(number);
    }
}
