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
    private readonly Action<Selection, Role?> _judge;
    private Selection _selection = Selection.Nothing;
    private PullViewModel? _pull;
    private string _player = string.Empty;
    private Role? _role;

    public AnalysisViewModel(Func<IReadOnlyCollection<string>> ours, Action<Selection, Role?> judge)
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

    /// <summary>Which role the rows are narrowed to, or null for all of them.</summary>
    public Role? Role => _role;

    public bool OnlyTanks => _role == Models.Role.Tank;

    public bool OnlyHealers => _role == Models.Role.Healer;

    public bool OnlyDamage => _role == Models.Role.Damage;

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

    private void Changed()
    {
        // The selection is what every baseline is drawn from, so changing it changes the numbers -
        // all of them, every time. Measured at 150ms for the rules and 74ms for every scorecard in
        // an evening, which is cheap enough that nothing has to be kept half-fresh.
        _judge(_selection, _role);

        OnPropertyChanged(nameof(Selection));
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
