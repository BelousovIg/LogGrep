namespace LogGrep.ViewModels;

/// <summary>Which of an encounter's attempts a report is about.</summary>
public enum Outcome
{
    /// <summary>Every one of them.</summary>
    All,

    /// <summary>Only the ones that ended in a kill.</summary>
    Kills,

    /// <summary>Only the ones that did not.</summary>
    Wipes,
}

/// <summary>
/// What a report is measured over, and how that choice was made.
///
/// The second half matters as much as the first. A selection made by a rule - "every attempt at this
/// boss", "the wipes" - grows when the log does, which is what somebody wants on a raid night: the
/// numbers take account of the pull that just happened without anybody touching anything. A selection
/// made by hand does not, because a set somebody picked out is theirs, and having it quietly grow
/// behind them would change every number on the screen for reasons they did not ask for.
///
/// Either way the selection is a sentence with numbers in it, kept on screen the whole time. Every
/// figure in a report is a share of something, and two different selections give the same person two
/// different scores - which must never be able to look like a bug.
/// </summary>
public sealed class Selection
{
    private readonly EncounterViewModel? _encounter;
    private readonly IReadOnlyList<PullViewModel> _chosen;

    private Selection(EncounterViewModel? encounter, IReadOnlyList<PullViewModel> chosen, Outcome which)
    {
        _encounter = encounter;
        _chosen = chosen;
        Which = which;
    }

    /// <summary>Everything this fight holds, and everything it comes to hold later.</summary>
    public static Selection Of(EncounterViewModel encounter, Outcome which = Outcome.All)
        => new(encounter, Array.Empty<PullViewModel>(), which);

    /// <summary>Exactly these attempts, and no others however the log grows.</summary>
    public static Selection Of(IReadOnlyList<PullViewModel> chosen)
        => new(chosen.Count > 0 ? chosen[0].Owner : null, chosen, Outcome.All);

    public static readonly Selection Nothing =
        new(null, Array.Empty<PullViewModel>(), Outcome.All);

    public Outcome Which { get; }

    /// <summary>Whether a new attempt at this fight joins it on its own.</summary>
    public bool Grows => _chosen.Count == 0 && _encounter != null;

    public EncounterViewModel? Encounter => _encounter;

    public bool IsEmpty => Pulls.Count == 0;

    /// <summary>The attempts themselves, resolved against the reading as it stands now.</summary>
    public IReadOnlyList<PullViewModel> Pulls
    {
        get
        {
            var all = _chosen.Count > 0
                ? _chosen
                : _encounter?.Pulls.ToList() ?? (IReadOnlyList<PullViewModel>)Array.Empty<PullViewModel>();

            return Which switch
            {
                Outcome.Kills => all.Where(p => p.IsSuccess).ToList(),
                Outcome.Wipes => all.Where(p => !p.IsSuccess).ToList(),
                _ => all,
            };
        }
    }

    /// <summary>The same selection, narrowed differently. Hand-picked sets keep their own list.</summary>
    public Selection With(Outcome which) => new(_encounter, _chosen, which);

    /// <summary>The sentence the bar carries, which is what stops a number being read out of context.</summary>
    public string Text
    {
        get
        {
            if (_encounter == null) return "nothing selected";

            int count = Pulls.Count;
            string what = Display.Count(count) + (count == 1 ? " attempt" : " attempts") + " at " + _encounter.Name;

            string how = Which switch
            {
                Outcome.Kills => ", kills only",
                Outcome.Wipes => ", wipes only",
                _ => string.Empty,
            };

            // A hand-picked set says what it was picked out of, because "10 attempts" and "10 of 31"
            // are answers to different questions and only the second one can be argued with.
            if (_chosen.Count > 0 && _encounter.Pulls.Count > _chosen.Count)
            {
                what = Display.Count(count) + " of " + Display.Count(_encounter.Pulls.Count) +
                       " attempts at " + _encounter.Name;
            }

            return what + how;
        }
    }
}
