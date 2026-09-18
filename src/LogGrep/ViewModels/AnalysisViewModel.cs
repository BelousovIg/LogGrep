using LogGrep.Models;

namespace LogGrep.ViewModels;

/// <summary>
/// What the report is about, and what of it is on screen.
///
/// Two different things, and keeping them apart is the whole of this class. The <b>sample</b> is
/// what every baseline is drawn from - an encounter's attempts, all of them - and the <b>focus</b>
/// is whichever part of it somebody is looking at. Clicking into one attempt narrows the focus and
/// leaves the sample alone, because a person's best is a fact about their evening and drilling in
/// to look closer must not destroy it. Read the other way round, opening one attempt would leave
/// every number on the screen a dash.
/// </summary>
public sealed class AnalysisViewModel : ObservableObject
{
    private EncounterViewModel? _encounter;
    private PullViewModel? _pull;
    private string _player = string.Empty;

    /// <summary>The attempts the baselines are drawn from: an encounter, entire.</summary>
    public EncounterViewModel? Encounter => _encounter;

    /// <summary>The attempt being looked at, or null when the whole sample is.</summary>
    public PullViewModel? Pull => _pull;

    /// <summary>The character being looked at, or empty when the whole group is.</summary>
    public string Player => _player;

    public bool HasAnything => _encounter != null;

    public bool HasPull => _pull != null;

    /// <summary>Nothing has been pointed at yet, which is a state the screen has to explain.</summary>
    public bool HasNothing => _encounter == null;

    /// <summary>Where this is: the sample, then what has been narrowed down to inside it.</summary>
    public string Breadcrumb
    {
        get
        {
            if (_encounter == null) return "Nothing to analyse yet";

            string where = _encounter.Name;
            if (_pull != null) where += "  ›  attempt " + Display.Count(_encounter.Pulls.IndexOf(_pull) + 1);
            if (_player.Length > 0) where += "  ›  " + PlayerName.Character(_player);

            return where;
        }
    }

    /// <summary>What the sample is, said out loud, because every number below it is a share of this.</summary>
    public string SampleText => _encounter == null
        ? string.Empty
        : "measured over " + Display.Count(_encounter.Pulls.Count) +
          (_encounter.Pulls.Count == 1 ? " attempt" : " attempts") + " at " + _encounter.Name;

    /// <summary>
    /// Points the screen at something. The sample is always the encounter entire; the pull and the
    /// player are the focus, and either may be absent.
    /// </summary>
    public void Show(EncounterViewModel encounter, PullViewModel? pull, string player)
    {
        _encounter = encounter;
        _pull = pull ?? (encounter.Pulls.Count == 1 ? encounter.Pulls[0] : null);
        _player = player;

        OnPropertyChanged(nameof(Encounter));
        OnPropertyChanged(nameof(Pull));
        OnPropertyChanged(nameof(Player));
        OnPropertyChanged(nameof(HasAnything));
        OnPropertyChanged(nameof(HasNothing));
        OnPropertyChanged(nameof(HasPull));
        OnPropertyChanged(nameof(Breadcrumb));
        OnPropertyChanged(nameof(SampleText));
    }

    /// <summary>Drops the focus back to the whole sample, which is what the breadcrumb's root does.</summary>
    public void WidenToEncounter()
    {
        if (_encounter == null) return;

        Show(_encounter, pull: null, player: string.Empty);
    }

    /// <summary>Clears everything, for when the logs behind it are gone.</summary>
    public void Forget()
    {
        _encounter = null;
        _pull = null;
        _player = string.Empty;

        OnPropertyChanged(nameof(Encounter));
        OnPropertyChanged(nameof(Pull));
        OnPropertyChanged(nameof(Player));
        OnPropertyChanged(nameof(HasAnything));
        OnPropertyChanged(nameof(HasNothing));
        OnPropertyChanged(nameof(HasPull));
        OnPropertyChanged(nameof(Breadcrumb));
        OnPropertyChanged(nameof(SampleText));
    }
}
