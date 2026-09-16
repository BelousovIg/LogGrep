using System.IO.Abstractions.TestingHelpers;
using System.Windows.Threading;
using LogGrep.Tests.Logs;
using LogGrep.ViewModels;

namespace LogGrep.Tests.Framework;

/// <summary>
/// The only thing in the tests that touches a view model. Everything above it - the actions and the
/// checks - speaks in encounters, pulls and players, so a change to how the window is wired lands
/// here and nowhere else.
///
/// It also remembers what was last looked at, which is what lets a check read as
/// "Then.EncounterIsOpened(true)" rather than repeating the name on every line.
/// </summary>
public sealed class LogGrepPage
{
    public const string LogPath = @"C:\logs\WoWCombatLog.txt";

    private readonly MockFileSystem _disk = new();
    private readonly Dispatcher _dispatcher;

    private EncounterViewModel? _encounter;
    private PullViewModel? _pull;
    private PlayerRowViewModel? _player;

    public LogGrepPage()
    {
        // The window's collection views may only be changed from the thread that built them. Giving
        // the test thread a dispatcher, and pumping it while a scan runs, puts the test in exactly
        // the position the UI thread is in - which is the point of driving the real view models.
        _dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(_dispatcher));
        ViewModel = new MainViewModel(_disk);
    }

    public MainViewModel ViewModel { get; }

    public IReadOnlyList<EncounterViewModel> Encounters => ViewModel.Encounters;

    public IReadOnlyList<RuleViewModel> Rules => ViewModel.Rules;

    public EncounterViewModel Encounter => _encounter ?? throw new InvalidOperationException(
        "No encounter is being looked at. Open one first.");

    public PullViewModel Pull => _pull ?? throw new InvalidOperationException(
        "No pull is being looked at. Open one first.");

    public PlayerRowViewModel Player => _player ?? throw new InvalidOperationException(
        "No player is being looked at. Look at one first.");

    public void Open(string path, string log)
    {
        _disk.AddFile(path, new MockFileData(log));
        Pump(ViewModel.LoadAsync(path));
    }

    /// <summary>Runs the dispatcher until the scan is done, the way a running window would.</summary>
    private void Pump(Task task)
    {
        var frame = new DispatcherFrame();
        task.ContinueWith(
            _ => _dispatcher.BeginInvoke(new Action(() => frame.Continue = false)),
            TaskScheduler.Default);

        Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }

    public void Open(CombatLogBuilder log) => Open(LogPath, log.Build());

    public void LookAtEncounter(string name)
        => _encounter = Encounters.FirstOrDefault(e => e.Name == name)
            ?? throw new InvalidOperationException(
                $"No encounter called '{name}'. The log has: {Names(Encounters.Select(e => e.Name))}");

    public void ToggleEncounter(string name)
    {
        LookAtEncounter(name);
        Encounter.IsExpanded = !Encounter.IsExpanded;
    }

    public void LookAtPull(int number)
    {
        if (number < 1 || number > Encounter.Pulls.Count)
        {
            throw new InvalidOperationException(
                $"'{Encounter.Name}' has {Encounter.Pulls.Count} pulls, so there is no pull {number}.");
        }

        _pull = Encounter.Pulls[number - 1];
    }

    public void TogglePull(int number)
    {
        LookAtPull(number);
        Pull.IsExpanded = !Pull.IsExpanded;
    }

    public void LookAtPlayer(string name)
        => _player = Players().FirstOrDefault(p => p.Name == name)
            ?? throw new InvalidOperationException(
                $"'{name}' is not in this pull. It has: {Names(Players().Select(p => p.Name))}");

    public IReadOnlyList<PlayerRowViewModel> Players() => Pull.PlayersView.Cast<PlayerRowViewModel>().ToList();

    public void SortPlayersBy(string column)
    {
        ViewModel.Sorting.Players.Toggle(column);
        foreach (var encounter in Encounters) encounter.ApplySorting();
    }

    public void ShowFindings(bool on) => ViewModel.ShowFindings = on;

    public RuleViewModel Rule(string spell)
        => Rules.FirstOrDefault(r => r.Rule.Spell == spell)
            ?? throw new InvalidOperationException(
                $"Nothing was found for '{spell}'. Findings: {Names(Rules.Select(r => r.Rule.Spell))}");

    public void IgnoreRule(string spell) => Rule(spell).IsIgnored = true;

    private static string Names(IEnumerable<string> values)
    {
        string joined = string.Join(", ", values);
        return joined.Length == 0 ? "nothing" : joined;
    }
}
