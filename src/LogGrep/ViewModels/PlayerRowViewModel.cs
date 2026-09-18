using System.Windows;
using System.Windows.Media;
using LogGrep.Analysis;
using LogGrep.Models;

namespace LogGrep.ViewModels;

/// <summary>One group member inside the roster table of a pull.</summary>
public sealed class PlayerRowViewModel
{
    private readonly PlayerStats _stats;
    private readonly double _seconds;
    private readonly Action<string> _report;
    private readonly IReadOnlyList<Finding> _mistakes;
    private readonly Scorecard? _card;

    public PlayerRowViewModel(PlayerStats stats, TimeSpan duration, Action<string> report,
        IReadOnlyList<Finding> mistakes, Scorecard? card = null,
        IReadOnlyList<TimeSpan>? collective = null)
    {
        _stats = stats;
        _seconds = duration.TotalSeconds;
        _report = report;
        _mistakes = mistakes;
        _card = card;
        Marks = LaneMark.For(stats, mistakes, collective ?? Array.Empty<TimeSpan>());
    }

    /// <summary>How long the attempt ran, which is what the lane's width stands for.</summary>
    public double Seconds => _seconds;

    /// <summary>Everything that happened to them, placed on the fight's own clock.</summary>
    public IReadOnlyList<LaneMark> Marks { get; }

    /// <summary>
    /// The four axes, as text. A dash where there is not enough behind a number to print one, which
    /// is a real answer rather than a gap - the tooltip says which it is and why.
    /// </summary>
    public string OutputText => Axis(Analysis.Axis.Output).Text;

    public string SurvivalText => Axis(Analysis.Axis.Survival).Text;

    public string MechanicsText => Axis(Analysis.Axis.Mechanics).Text;

    public string DutyText => Axis(Analysis.Axis.Duty).Text;

    /// <summary>The raw numbers behind the four cells, so the columns sort on them. Null sorts last.</summary>
    public double? OutputValue => Axis(Analysis.Axis.Output).Value;

    public double? SurvivalValue => Axis(Analysis.Axis.Survival).Value;

    public double? MechanicsValue => Axis(Analysis.Axis.Mechanics).Value;

    public double? DutyValue => Axis(Analysis.Axis.Duty).Value;

    /// <summary>What the attempt cost them, in their own health pools.</summary>
    public double PoolsValue => _card?.Pools ?? 0;

    public string OutputTooltip => Explain(Analysis.Axis.Output, "Output");

    public string SurvivalTooltip => Explain(Analysis.Axis.Survival, "Survival");

    public string MechanicsTooltip => Explain(Analysis.Axis.Mechanics, "Mechanics");

    public string DutyTooltip => Explain(Analysis.Axis.Duty, "Duty");

    /// <summary>
    /// The single most expensive thing that happened to them, which is the only sentence the row
    /// shows. A list in a cell does not survive a fourteenth rule being added; a maximum does.
    /// </summary>
    public string WorstText => _card?.Worst is { } worst ? worst.Line : "—";

    public bool HasWorst => _card?.Worst != null;

    private Score Axis(Axis axis)
        => _card?[axis] ?? Score.Missing(axis, "nothing has been analysed yet");

    private string Explain(Axis axis, string title)
    {
        var score = Axis(axis);

        string head = title + ": " + score.Text;
        if (!score.Known) return head + Environment.NewLine + score.Facts;

        return head + Environment.NewLine + score.Facts +
            (score.Against.Length == 0 ? string.Empty : Environment.NewLine + score.Against);
    }

    /// <summary>The character on its own; the realm lives in the tooltip.</summary>
    public string Name => PlayerName.Character(_stats.Name);

    /// <summary>"Name - Realm", what the cell shows on hover.</summary>
    public string FullName => PlayerName.Format(_stats.Name);

    /// <summary>The log's own spelling, which is what the analysis looks somebody up by.</summary>
    public string RawName => _stats.Name;

    public string ClassName => _stats.ClassName;

    public string SpecName => _stats.SpecName;


    /// <summary>
    /// The role, exposed as flags because the row draws a mark for two of the three and nothing
    /// for the third. WoW marks a tank with a shield and a healer with a cross; damage carries no
    /// mark here, since it is what most of a group is and a mark on almost every row says nothing.
    /// </summary>
    public bool IsTank => Specs.RoleOf(_stats.SpecId) == Role.Tank;

    public bool IsHealer => Specs.RoleOf(_stats.SpecId) == Role.Healer;
    /// <summary>Class colour for the class cell, the palette WoW itself uses.</summary>
    public Brush ClassBrush => ClassBrushes.For(_stats.ClassColor);


    /// <summary>How many mechanics this player took that were not theirs; the column sorts on it.</summary>
    public int MistakeCount => _mistakes.Count;

    public bool HasMistakes => _mistakes.Count > 0;

    /// <summary>Each mistake as "m:ss spell - role mechanic", separated for one line.</summary>
    public string MistakesText => _mistakes.Count == 0
        ? "—"
        : string.Join("; ", _mistakes.Select(Describe));

    /// <summary>
    /// The same list, one to a block, with all four fields of the finding under it: what happened,
    /// why it counts, what it cost, and what to do about it. The name of a spell says
    /// what was taken and nothing about why that was wrong, and the app cannot explain a boss - but
    /// it can explain itself, and "8 of 9 hit a tank" is the whole of its case. Seeing the size of
    /// that sample is the point: a rule drawn from one attempt is worth arguing with.
    /// </summary>
    public string MistakesTooltip => _mistakes.Count == 0
        ? "This player took nothing that was not theirs"
        : string.Join(Environment.NewLine + Environment.NewLine, _mistakes.Select(Explain));

    private static string Describe(Finding mistake) => mistake.Line;

    private static string Explain(Finding mistake)
        => mistake.Line + Environment.NewLine +
           "      " + mistake.Evidence + Environment.NewLine +
           "      " + mistake.Cost.Text + Environment.NewLine +
           "      " + mistake.Advice;

    /// <summary>Raw values behind the formatted cells, so the columns sort on numbers and times.</summary>
    public double DpsValue => Rate(_stats.Damage);

    public double HpsValue => Rate(_stats.Healing);

    public double DtpsValue => Rate(_stats.DamageTaken);

    /// <summary>Null for a survivor, which keeps them at the bottom whichever way the column points.</summary>
    public TimeSpan? FirstDeath => _stats.Deaths.Count == 0 ? null : _stats.Deaths[0].At;

    /// <summary>Heaviest ability behind the first death, so the column groups everyone killed by the same thing.</summary>
    public string? TopCause => _stats.Deaths.Count == 0 || _stats.Deaths[0].Causes.Count == 0
        ? null
        : _stats.Deaths[0].Causes[0].Label;

    public string DpsText => Display.Rate(DpsValue);

    public string HpsText => Display.Rate(HpsValue);

    /// <summary>Damage taken per second, the other half of what a pull costs a player.</summary>
    public string DtpsText => Display.Rate(DtpsValue);

    public bool Died => _stats.Deaths.Count > 0;

    /// <summary>
    /// The first death as "m:ss", with a count of the ones after it, or "-:--" for a player who
    /// survived. The cell is right-aligned and narrow, so a list of every time ran off its left
    /// edge and left the reader looking at ", 2:10" - a fixed width that cannot do that is worth
    /// more here than the full list, which the tooltip carries.
    /// </summary>
    public string DeathsText => _stats.Deaths.Count switch
    {
        0 => "-:--",
        1 => Display.Clock(_stats.Deaths[0].At),
        _ => Display.Clock(_stats.Deaths[0].At) + " +" + (_stats.Deaths.Count - 1),
    };

    /// <summary>Every death, for the cell that only had room for the first.</summary>
    public string DeathsTooltip => _stats.Deaths.Count == 0
        ? "This player did not die"
        : string.Join(", ", _stats.Deaths.Select(d => Display.Clock(d.At)));

    /// <summary>
    /// What was hitting the player over the last seconds before each death. A single death reads
    /// as a plain list; several are bracketed so it stays clear which list belongs to which death.
    /// </summary>
    public string CausesText
    {
        get
        {
            if (_stats.Deaths.Count == 0) return "—";
            if (_stats.Deaths.Count == 1) return Causes(_stats.Deaths[0]);

            return string.Join(" ", _stats.Deaths.Select(d => "[" + Causes(d) + "]"));
        }
    }

    public string CausesTooltip => _stats.Deaths.Count == 0
        ? "This player did not die"
        : string.Join(Environment.NewLine, _stats.Deaths.Select(
            (d, i) => "Death " + (i + 1) + " at " + Display.Clock(d.At) + ": " + Causes(d)));

    /// <summary>
    /// The word goes in front of the ability rather than after the number, because the column is
    /// trimmed on the right: a mark at the end of a long list is the first thing to disappear, and
    /// the whole point of it is to be read without opening anything.
    /// </summary>
    private string Causes(DeathRecord death) => death.Causes.Count == 0
        ? "no damage logged"
        : string.Join(", ", death.Causes.Select(
            c => (Avoidable(c.Label) ? "(avoidable) " : string.Empty) + c.Label + " " + Display.Amount(c.Amount)));

    /// <summary>
    /// Whether the app worked out, from this run of attempts, that most of the group takes none of
    /// what killed this player. A breakdown that only names the spell leaves the reader to guess
    /// whether it was theirs to get out of, and that guess is the whole question.
    /// </summary>
    private bool Avoidable(string cause)
        => _mistakes.Any(m => m.Category == AvoidableDamageDetector.Name
            && m.Headline.StartsWith(cause + " -", StringComparison.Ordinal));

    private double Rate(long total) => _seconds > 0.5 ? total / _seconds : 0;

    /// <summary>
    /// Puts "Name - Realm" on the clipboard - the form the cell trims away and the tooltip shows,
    /// and the one another tool or a chat actually wants. Returns whether it worked, because the
    /// window says "copied" over the cursor and that should not be a lie.
    /// </summary>
    public bool CopyName()
    {
        try
        {
            Clipboard.SetDataObject(FullName, copy: true);
        }
        catch (Exception ex)
        {
            _report("Could not copy to the clipboard: " + ex.Message);
            return false;
        }

        _report("Copied " + FullName + " to the clipboard.");
        return true;
    }
}
