using System.Collections.ObjectModel;
using System.Windows.Media;
using LogGrep.Analysis;
using LogGrep.Models;

namespace LogGrep.ViewModels;

/// <summary>One player who took a mechanic that is not theirs, on one attempt.</summary>
public sealed class FindingRowViewModel
{
    private readonly Finding _finding;

    public FindingRowViewModel(Finding finding) => _finding = finding;

    public string Player => PlayerName.Character(_finding.Player);

    public string FullName => PlayerName.Format(_finding.Player);

    public string ClassName => Specs.ClassOf(_finding.SpecId);

    public string SpecName => Specs.SpecOf(_finding.SpecId);

    public Brush ClassBrush => ClassBrushes.For(Specs.ColorOf(_finding.SpecId));

    public string RoleName => Specs.NameOf(_finding.Role);

    public string PullText => "pull " + _finding.PullNumber;

    public string AtText => Display.Clock(_finding.At);
}

/// <summary>
/// One mechanic, with everyone who took it out of turn. Rules carry their evidence because a
/// pattern drawn from nine applications deserves far less trust than one drawn from seventy, and
/// that has to be visible without opening the row.
/// </summary>
public sealed class RuleViewModel : ObservableObject
{
    private readonly Action _changed;
    private bool _isExpanded = true;
    private bool _isIgnored;

    public RuleViewModel(MechanicRule rule, IEnumerable<Finding> findings, Action changed)
    {
        Rule = rule;
        _changed = changed;
        foreach (var finding in findings.OrderBy(f => f.PullNumber).ThenBy(f => f.At))
        {
            Findings.Add(new FindingRowViewModel(finding));
        }

        IgnoreCommand = new RelayCommand(() => IsIgnored = !IsIgnored);
    }

    public MechanicRule Rule { get; }

    public ObservableCollection<FindingRowViewModel> Findings { get; } = new();

    public RelayCommand IgnoreCommand { get; }

    public string Title => Rule.Spell + " — " + Specs.NameOf(Rule.Owner) + " mechanic";

    public string Encounter => Rule.Encounter;

    public string Evidence => Rule.Evidence + " (" + Rule.Share.ToString("P0") + ")";

    public string CountText => Findings.Count == 1 ? "1 player" : Findings.Count + " players";

    /// <summary>
    /// A mechanic is sometimes taken by somebody else on purpose, and such a rule would fire every
    /// pull. Without a way to silence it the first few false alarms would cost the whole list its
    /// credibility, so an ignored rule drops to the bottom rather than being argued with.
    /// </summary>
    public bool IsIgnored
    {
        get => _isIgnored;
        set
        {
            if (!Set(ref _isIgnored, value)) return;
            OnPropertyChanged(nameof(IgnoreText));
            _changed();
        }
    }

    public string IgnoreText => IsIgnored ? "restore" : "ignore";

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }
}
