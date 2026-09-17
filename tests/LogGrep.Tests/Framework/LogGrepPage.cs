using System.IO.Abstractions.TestingHelpers;
using System.Windows.Threading;
using LogGrep.Analysis;
using LogGrep.Models;
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
    /// <summary>Where the fake disk keeps the logs a scenario writes.</summary>
    public const string Folder = @"C:\logs\";

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

    public MainViewModel ViewModel { get; private set; }

    public IReadOnlyList<EncounterViewModel> Encounters => ViewModel.Encounters;

    public IReadOnlyList<Finding> Findings => ViewModel.Findings;

    public Reading Reading => ViewModel.Reading;

    /// <summary>Findings whose headline names that spell, which is how a scenario asks about one.</summary>
    public IReadOnlyList<Finding> FindingsFor(Ability spell)
        => Findings.Where(f => f.Headline.StartsWith(spell.NameOf(), StringComparison.Ordinal)).ToList();

    public EncounterViewModel Encounter => _encounter ?? throw new InvalidOperationException(
        "No encounter is being looked at. Open one first.");

    public PullViewModel Pull => _pull ?? throw new InvalidOperationException(
        "No pull is being looked at. Open one first.");

    public PlayerRowViewModel Player => _player ?? throw new InvalidOperationException(
        "No player is being looked at. Look at one first.");

    /// <summary>Closes the app and opens it again, on the same disk - which is what a restart is.</summary>
    public void Reopen()
    {
        _encounter = null;
        _pull = null;
        _player = null;
        ViewModel = new MainViewModel(_disk);
        Pump(ViewModel.RestoreAsync());
    }

    public void DeleteFile(string name) => _disk.File.Delete(Folder + name);

    /// <summary>
    /// Writes a rules file where the app looks for one, in the shape the journal generator produces:
    /// an id, a name and the roles, with Blizzard's own sentence indented under it.
    /// </summary>
    public void WriteRule(Ability spell, string roles, string advice)
    {
        string path = new LogGrep.Services.SettingsService(_disk).RulesPath;
        string existing = _disk.File.Exists(path)
            ? _disk.File.ReadAllText(path)
            : "# rules" + Environment.NewLine;

        _disk.Directory.CreateDirectory(_disk.Path.GetDirectoryName(path)!);
        _disk.File.WriteAllText(path,
            existing + (int)spell + "  " + spell.NameOf() + "  " + roles + Environment.NewLine +
            "    " + advice + Environment.NewLine);
    }

    public void RemoveLog(string name)
    {
        var row = ViewModel.Logs.FirstOrDefault(l => l.Name == name)
            ?? throw new InvalidOperationException(
                "No log called '" + name + "' is open. These are: " +
                string.Join(", ", ViewModel.Logs.Select(l => l.Name)));

        Pump(ViewModel.RemoveAsync(row));
    }

    /// <summary>
    /// Puts the logs on the fake disk under the names the game would have given them, with the
    /// creation dates each one claims, and adds them to the list.
    /// </summary>
    public void Open(params CombatLogBuilder[] logs)
    {
        var paths = new string[logs.Length];

        for (int i = 0; i < logs.Length; i++)
        {
            paths[i] = Folder + logs[i].FileName;
            _disk.AddFile(paths[i], new MockFileData(logs[i].Build()) { CreationTime = logs[i].Created });
        }

        Pump(ViewModel.LoadAsync(paths));
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


    public void LookAtEncounter(Boss boss)
        => _encounter = Encounters.FirstOrDefault(e => e.Name == boss.NameOf())
            ?? throw new InvalidOperationException(
                $"No encounter called '{boss.NameOf()}'. The log has: {Names(Encounters.Select(e => e.Name))}");

    public void ToggleEncounter(Boss boss)
    {
        LookAtEncounter(boss);
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

    public void SortPlayersBy(PlayerColumn column)
    {
        ViewModel.Sorting.Players.Toggle(column.ToString());
        foreach (var encounter in Encounters) encounter.ApplySorting();
    }

    private static string Names(IEnumerable<string> values)
    {
        string joined = string.Join(", ", values);
        return joined.Length == 0 ? "nothing" : joined;
    }
}
