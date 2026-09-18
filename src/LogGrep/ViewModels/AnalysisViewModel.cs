using LogGrep.Models;

namespace LogGrep.ViewModels;

/// <summary>
/// What the report is about, and what of it is on screen.
///
/// Two different things, and keeping them apart is the whole of this class. The <b>sample</b> is
/// what every baseline is drawn from - a selection of attempts, named in a sentence that stays on
/// screen - and the <b>focus</b> is whichever part of it somebody is looking at. Clicking into one
/// attempt narrows the focus and leaves the sample alone, because a person's best is a fact about
/// their evening and drilling in to look closer must not destroy it. Read the other way round,
/// opening one attempt would leave every number on the screen a dash.
///
/// The other half of the selection is who: the characters the registry counts as ours, narrowed
/// further by role if somebody wants only the tanks. A role is a property of an attempt rather than
/// of a person - somebody who respecs did two jobs - so the chips pick attempts out of an evening
/// rather than picking people out of a raid.
/// </summary>
public sealed class AnalysisViewModel : ObservableObject
{
    private readonly Func<IReadOnlyCollection<string>> _ours;
    private readonly Func<Selection, Role?, AttemptGrid> _judge;
    private Selection _selection = Selection.Nothing;
    private PullViewModel? _pull;
    private string _player = string.Empty;
    private Role? _role;
    private bool _replaying;

    /// <summary>Raised whenever the report moves somewhere, so the trail can note it.</summary>
    public event Action? Moved;

    public AnalysisViewModel(Func<IReadOnlyCollection<string>> ours, Func<Selection, Role?, AttemptGrid> judge)
    {
        _ours = ours;
        _judge = judge;
    }

    /// <summary>The attempts the baselines are drawn from, and how that choice was made.</summary>
    public Selection Selection => _selection;

    public EncounterViewModel? Encounter => _selection.Encounter;

    /// <summary>The attempt being looked at, or null when the whole sample is.</summary>
    public PullViewModel? Pull => _pull;

    /// <summary>The character being looked at, or empty when the whole group is.</summary>
    public string Player => _player;

    public bool HasAnything => !_selection.IsEmpty;

    public bool HasNothing => _selection.IsEmpty;

    public bool HasPull => _pull != null;

    /// <summary>
    /// The selection as a grid: rows are who, columns are when. Shown when no single attempt is in
    /// focus, which is the whole point of the zoom - a row is a person, a column is an attempt, and
    /// clicking either one is how the report narrows.
    /// </summary>
    public AttemptGrid Grid { get; private set; } = AttemptGrid.Nothing;

    public bool ShowGrid => _pull == null && Grid.HasAnything;

    /// <summary>Which role the rows are narrowed to, or null for all of them.</summary>
    public Role? Role => _role;

    public bool OnlyTanks => _role == Models.Role.Tank;

    public bool OnlyHealers => _role == Models.Role.Healer;

    public bool OnlyDamage => _role == Models.Role.Damage;

    /// <summary>
    /// The trail, as pieces somebody can click rather than as one sentence. A report that can be
    /// drilled into and not climbed out of is a report with a dead end in it, and the way back was
    /// already written on screen - it just was not doing anything.
    /// </summary>
    public IReadOnlyList<Crumb> Crumbs
    {
        get
        {
            if (_selection.Encounter == null) return Array.Empty<Crumb>();

            var trail = new List<Crumb> { new(_selection.Encounter.Name, 0, _pull != null || _player.Length > 0) };

            if (_pull != null)
            {
                trail.Add(new Crumb(
                    "attempt " + Display.Count(_selection.Encounter.Pulls.IndexOf(_pull) + 1),
                    1, _player.Length > 0));
            }

            if (_player.Length > 0) trail.Add(new Crumb(PlayerName.Character(_player), 2, false));

            return trail;
        }
    }

    /// <summary>Climbs back to one of them: the sample, or the attempt inside it.</summary>
    public void GoTo(int depth)
    {
        if (depth == 0)
        {
            _pull = null;
            _player = string.Empty;
        }
        else if (depth == 1)
        {
            _player = string.Empty;
        }
        else
        {
            return;
        }

        Changed();
    }

    /// <summary>Where this is: the sample, then what has been narrowed down to inside it.</summary>
    public string Breadcrumb
    {
        get
        {
            if (_selection.Encounter == null) return "Nothing to analyse yet";

            string where = _selection.Encounter.Name;
            if (_pull != null)
            {
                where += "  ›  attempt " + Display.Count(_selection.Encounter.Pulls.IndexOf(_pull) + 1);
            }

            if (_player.Length > 0) where += "  ›  " + PlayerName.Character(_player);

            return where;
        }
    }

    /// <summary>What the sample is, said out loud, because every number below it is a share of this.</summary>
    public string SampleText => _selection.Encounter == null
        ? string.Empty
        : "measured over " + _selection.Text + (_selection.Grows ? string.Empty : ", picked by hand");

    /// <summary>Who the report is about, said the same way and for the same reason.</summary>
    public string PeopleText
    {
        get
        {
            int ours = _ours().Count;
            string who = ours == 0 ? "everybody in the group" : Display.Count(ours) + " of ours";

            return _role == null ? who : who + ", " + Specs.NameOf(_role.Value) + " only";
        }
    }

    /// <summary>
    /// Points the screen at a selection. The sample is whatever was selected; the pull and the
    /// player are the focus, and either may be absent.
    /// </summary>
    public void Show(Selection selection, PullViewModel? pull, string player)
    {
        _selection = selection;

        var pulls = selection.Pulls;
        _pull = pull ?? (pulls.Count == 1 ? pulls[0] : null);
        _player = player;

        Changed();
    }

    /// <summary>Narrows the sample to the kills, the wipes, or back to all of them.</summary>
    public void Narrow(Outcome which)
    {
        _selection = _selection.With(which);

        // The attempt that was open may not be in the sample any more, and a report headed
        // "attempt 7" that nothing on it was measured over is worse than showing the whole sample.
        if (_pull != null && !_selection.Pulls.Contains(_pull)) _pull = null;

        Changed();
    }

    /// <summary>Narrows the rows to one role, or back to the whole group.</summary>
    public void Narrow(Role? role)
    {
        _role = _role == role ? null : role;
        Changed();
    }

    /// <summary>Opens one attempt of the sample, and one person in it.</summary>
    public void Open(int attempt, string player)
    {
        var pulls = _selection.Pulls;
        if (attempt < 0 || attempt >= pulls.Count) return;

        _pull = pulls[attempt];
        _player = player;
        Changed();
    }

    /// <summary>Drops the focus back to the whole sample, which is what the breadcrumb's root does.</summary>
    public void WidenToSample()
    {
        _pull = null;
        _player = string.Empty;
        Changed();
    }

    /// <summary>Clears everything, for when the logs behind it are gone.</summary>
    public void Forget()
    {
        _selection = Selection.Nothing;
        _pull = null;
        _player = string.Empty;
        _role = null;
        Changed();
    }

    /// <summary>
    /// Puts the report exactly where it was, without noting the move. Going back is not a place of
    /// its own - recording it would make the back button walk in circles.
    /// </summary>
    internal void Restore(Selection selection, PullViewModel? pull, string player, Role? role)
    {
        _replaying = true;
        _selection = selection;
        _pull = pull;
        _player = player;
        _role = role;
        Changed();
        _replaying = false;
    }

    /// <summary>Everything about where the report is, for the trail to keep.</summary>
    internal Place Here(int screen) => new(screen, _selection, _pull, _player, _role);

    private void Changed()
    {
        OnPropertyChanged(nameof(Crumbs));
        // The selection is what every baseline is drawn from, so changing it changes the numbers -
        // all of them, every time. Measured at 150ms for the rules and 74ms for every scorecard in
        // an evening, which is cheap enough that nothing has to be kept half-fresh.
        Grid = _judge(_selection, _role);
        if (!_replaying) Moved?.Invoke();

        OnPropertyChanged(nameof(Selection));
        OnPropertyChanged(nameof(Grid));
        OnPropertyChanged(nameof(ShowGrid));
        OnPropertyChanged(nameof(Encounter));
        OnPropertyChanged(nameof(Pull));
        OnPropertyChanged(nameof(Player));
        OnPropertyChanged(nameof(HasAnything));
        OnPropertyChanged(nameof(HasNothing));
        OnPropertyChanged(nameof(HasPull));
        OnPropertyChanged(nameof(Role));
        OnPropertyChanged(nameof(OnlyTanks));
        OnPropertyChanged(nameof(OnlyHealers));
        OnPropertyChanged(nameof(OnlyDamage));
        OnPropertyChanged(nameof(Breadcrumb));
        OnPropertyChanged(nameof(SampleText));
        OnPropertyChanged(nameof(PeopleText));
    }
}

/// <summary>
/// One piece of the trail. <see cref="Climbable"/> is false for the last one, because the place you
/// are already standing is not somewhere to go.
/// </summary>
public sealed record Crumb(string Text, int Depth, bool Climbable);
