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
    public IReadOnlyList<PersonRowViewModel> People => ViewModel.People;

    public PersonRowViewModel Person(string name)
        => People.FirstOrDefault(p => p.Name == name)
           ?? throw new InvalidOperationException(
               $"'{name}' is not in the registry. It holds: " +
               (People.Count == 0 ? "nobody" : string.Join(", ", People.Select(p => p.Name))));

    public void MarkAsOurs(string name, bool ours) => Person(name).IsOurs = ours;

    /// <summary>Adds another file to the ones already open, which re-reads all of them.</summary>
    public void OpenAlso(CombatLogBuilder log)
    {
        string path = Folder + log.FileName;
        _disk.AddFile(path, new MockFileData(log.Build()) { CreationTime = log.Created });
        Pump(ViewModel.LoadAsync(new[] { path }));
    }

    public void AnalyseEncounter(Boss boss) => ViewModel.Analyse(Of(boss));

    public void AnalyseChosen(Boss boss, params int[] numbers)
    {
        foreach (var pull in Of(boss).Pulls) pull.IsSelected = false;
        foreach (int number in numbers) Of(boss).Pulls[number - 1].IsSelected = true;

        ViewModel.AnalyseSelected();
    }

    public void NarrowTo(Outcome which) => ViewModel.Analysis.Narrow(which);

    /// <summary>The rows the report is showing, which the role chips narrow.</summary>
    public IReadOnlyList<PlayerRowViewModel> ReportPlayers()
        => ViewModel.Analysis.Pull?.PlayersView.Cast<PlayerRowViewModel>().ToList()
           ?? (IReadOnlyList<PlayerRowViewModel>)Array.Empty<PlayerRowViewModel>();

    /// <summary>Looks at one attempt inside whatever the report is already measuring.</summary>
    public void LookAtPullInTheReport(int number)
    {
        var selection = ViewModel.Analysis.Selection;
        ViewModel.Analysis.Show(selection, selection.Pulls[number - 1], string.Empty);
        _pull = selection.Pulls[number - 1];
    }

    public void NarrowTo(Role role) => ViewModel.Analysis.Narrow(role);

    public void AnalysePull(Boss boss, int number) => ViewModel.Analyse(Of(boss), Of(boss).Pulls[number - 1]);

    /// <summary>
    /// Clicking a name in whichever table the report is showing. The raw name is what the app knows
    /// people by; a scenario says the one it can read.
    /// </summary>
    public void LookAtInTheReport(string player)
    {
        string? raw = ViewModel.Analysis.Grid.Rows.FirstOrDefault(r => r.Name == player)?.RawName
            ?? ViewModel.Analysis.Pull?.PlayersView.Cast<PlayerRowViewModel>()
                .FirstOrDefault(p => p.Name == player)?.RawName;

        ViewModel.Analysis.LookAt(raw ?? throw new InvalidOperationException(
            "'" + player + "' is not in either table of the report."));
    }

    public void AnalysePlayer(Boss boss, int number, string player)
    {
        var pull = Of(boss).Pulls[number - 1];
        var row = pull.PlayersView.Cast<PlayerRowViewModel>().FirstOrDefault(p => p.Name == player)
            ?? throw new InvalidOperationException($"'{player}' is not in that attempt.");

        ViewModel.Analyse(Of(boss), pull, row.RawName);
    }

    private EncounterViewModel Of(Boss boss)
        => Encounters.FirstOrDefault(e => e.Name == boss.NameOf())
           ?? throw new InvalidOperationException($"'{boss.NameOf()}' is not in the reading.");

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

    /// <summary>
    /// The game writing more into a log that is already open - which is what a log does all evening.
    /// The file keeps its name and its first line, so it is the same log with more in it.
    /// </summary>
    public void GrowLog(string name, CombatLogBuilder more)
    {
        string path = Folder + name;
        _disk.File.AppendAllText(path, more.Body);
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

    /// <summary>A keystone run, which is listed under the dungeon rather than under a boss.</summary>
    public void LookAtEncounter(Dungeon dungeon)
        => _encounter = Encounters.FirstOrDefault(e => e.Name == dungeon.NameOf())
            ?? throw new InvalidOperationException(
                $"No encounter called '{dungeon.NameOf()}'. The log has: {Names(Encounters.Select(e => e.Name))}");

    public void ToggleEncounter(Dungeon dungeon)
    {
        LookAtEncounter(dungeon);
        Encounter.IsExpanded = !Encounter.IsExpanded;
    }

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

    /// <summary>
    /// What dragging across the chart does: picks a stretch of the fight, and every rate under it is
    /// read over that stretch instead of over the whole attempt.
    /// </summary>
    public void ReadTheFightFrom(TimeSpan from, TimeSpan to)
    {
        Pull.From = (int)from.TotalSeconds;
        Pull.To = (int)to.TotalSeconds;
    }

    public void ReadTheWholeAttempt() => Pull.ResetWindow();

    /// <summary>The switch under the chart that takes one line off it.</summary>
    public void SwitchLine(string name, bool on)
    {
        var trace = Pull.Traces.FirstOrDefault(t => t.Name == name)
            ?? throw new InvalidOperationException(
                $"'{name}' is not a line on this chart. It draws: {Names(Pull.Traces.Select(t => t.Name))}");

        trace.IsOn = on;
    }

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
