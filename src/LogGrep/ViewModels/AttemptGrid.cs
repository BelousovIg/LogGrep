using System.Windows.Media;
using LogGrep.Analysis;
using LogGrep.Models;

namespace LogGrep.ViewModels;

/// <summary>
/// One person in one attempt, as a cell.
///
/// <see cref="Present"/> is the one that has to be got right from the first day. A cell where
/// somebody was not in the raid and a cell where they were and nothing went wrong are two completely
/// different answers, and drawn the same way the person who missed half the evening reads as the
/// cleanest player in it.
/// </summary>
public sealed record GridCell(
    bool Present, int Mistakes, int Serious, string Text, string Tooltip, int Attempt, string Player, Brush Paint);

/// <summary>
/// One person's row across the attempts of a selection.
///
/// It carries the same columns an attempt's roster does, in the same order, because somebody
/// reading down a night and somebody reading across one attempt are the same person and should not
/// have to learn two tables. The rates are over the attempts they were actually in - a night's
/// damage divided by a night's length would charge them for the pulls they sat out.
/// </summary>
public sealed record GridRow(
    string Name,
    string FullName,
    Brush ClassBrush,
    string ClassName,
    string SpecName,
    bool IsTank,
    bool IsHealer,
    string DpsText,
    string HpsText,
    string DtpsText,
    IReadOnlyList<GridCell> Cells)
{
    /// <summary>How many mistakes the whole selection found against them, and how many were serious.</summary>
    public int Mistakes => Cells.Where(c => c.Present).Sum(c => c.Mistakes);

    public int Serious => Cells.Where(c => c.Present).Sum(c => c.Serious);

    public string MistakesText => Mistakes == 0 ? "—" : Display.Mistakes(Mistakes, Serious);

    /// <summary>How many of the attempts they were actually in, for the hover.</summary>
    public string PresenceText
    {
        get
        {
            int there = Cells.Count(c => c.Present);
            return there == Cells.Count
                ? "In every attempt of this selection"
                : "In " + there + " of " + Cells.Count + " attempts";
        }
    }
}

/// <summary>
/// The selection as a grid: rows are who, columns are when.
///
/// This is the one control the whole report is made of, and the four views are states of it rather
/// than four screens. What changes between them is only how many rows there are and what a column
/// stands for - a person or a group, a second or an attempt - so somebody learns to read it once.
///
/// The colour scale is the selection's own worst cell. Nothing here knows what a bad night looks
/// like in the abstract; it knows what this one held, which is the only scale that can be argued
/// with.
/// </summary>
public sealed class AttemptGrid
{
    private static readonly Brush Missing = Frozen(Color.FromRgb(0x44, 0x47, 0x4F));
    private static readonly Brush Clean = Frozen(Color.FromRgb(0x69, 0xC0, 0x7A));

    public static readonly AttemptGrid Nothing = new(Array.Empty<string>(), Array.Empty<GridRow>());

    private AttemptGrid(IReadOnlyList<string> columns, IReadOnlyList<GridRow> rows)
    {
        Columns = columns;
        Rows = rows;
    }

    /// <summary>The attempts, numbered the way the list numbers them.</summary>
    public IReadOnlyList<string> Columns { get; }

    public IReadOnlyList<GridRow> Rows { get; }

    public bool HasAnything => Rows.Count > 0 && Columns.Count > 0;

    public static AttemptGrid Of(IReadOnlyList<PullViewModel> pulls, Scorecards cards, Role? role,
        IReadOnlyCollection<string> ours)
    {
        if (pulls.Count == 0) return Nothing;

        var columns = pulls.Select((_, i) => Display.Count(i + 1)).ToArray();

        // Everybody who appeared in any of the attempts, not just in the first: a raid changes
        // between pulls, and a roster taken from one of them would lose whoever came later.
        var people = pulls
            .SelectMany(p => p.Record.Roster)
            .Where(p => ours.Count == 0 || ours.Contains(p.Guid))
            .GroupBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        var gathered = new List<(PlayerStats Latest, Totals Totals, List<Raw> Cells)>(people.Count);
        double worst = 0;

        foreach (var person in people)
        {
            var cells = new List<Raw>(pulls.Count);
            var totals = new Totals();
            PlayerStats? latest = null;

            for (int i = 0; i < pulls.Count; i++)
            {
                var stats = pulls[i].Record.Roster
                    .FirstOrDefault(p => string.Equals(p.Name, person.Key, StringComparison.Ordinal));

                if (stats == null || (role != null && Specs.RoleOf(stats.SpecId) != role))
                {
                    cells.Add(new Raw(false, 0, 0, "They were not in this attempt", i));
                    continue;
                }

                // The last attempt they were in, not the first: somebody who changed spec halfway
                // through the night is playing the one they finished on.
                latest = stats;
                totals.Add(stats, pulls[i].Record.Duration.TotalSeconds);

                // What a cell counts is mistakes, not what they cost. A cost is one number made of
                // several judgements about what a thing was worth; a count is the app saying "these
                // five things were found", so when the rule for what counts as a mistake changes,
                // the cell changes with it and says so plainly.
                var card = cards.For(pulls[i].Record, stats);
                worst = Math.Max(worst, card.Findings.Count);

                cells.Add(new Raw(true, card.Findings.Count, card.Findings.Count(f => f.Serious),
                    card.Worst?.Line ?? "Nothing was found for them in this attempt", i));
            }

            if (latest != null) gathered.Add((latest, totals, cells));
        }

        // The same order every list of players in the window is in: the tanks, then the healers,
        // then everyone else, each by what their role is there to do. Sorting these by what the
        // night cost them read as a different table from the roster right underneath it.
        var rows = gathered
            .OrderBy(entry => Group(entry.Latest))
            .ThenByDescending(entry => Rank(entry.Latest, entry.Totals))
            .ThenBy(entry => PlayerName.Character(entry.Latest.Name), StringComparer.CurrentCulture)
            .Select(entry => new GridRow(
                PlayerName.Character(entry.Latest.Name),
                PlayerName.Format(entry.Latest.Name),
                ClassBrushes.For(entry.Latest.ClassColor),
                entry.Latest.ClassName,
                entry.Latest.SpecName,
                Specs.RoleOf(entry.Latest.SpecId) == Role.Tank,
                Specs.RoleOf(entry.Latest.SpecId) == Role.Healer,
                Display.Rate(entry.Totals.Per(entry.Totals.Damage)),
                Display.Rate(entry.Totals.Per(entry.Totals.Healing)),
                Display.Rate(entry.Totals.Per(entry.Totals.Taken)),
                entry.Cells.Select(c => Finish(c, entry.Latest.Name, worst)).ToArray()))
            .ToArray();

        return new AttemptGrid(columns, rows);
    }

    /// <summary>The tanks, then the healers, then everyone else, the way a group is talked about.</summary>
    private static int Group(PlayerStats player) => Specs.RoleOf(player.SpecId) switch
    {
        Role.Tank => 0,
        Role.Healer => 1,
        _ => 2,
    };

    /// <summary>Within a group, by what that group is there to do over the attempts they were in.</summary>
    private static double Rank(PlayerStats player, Totals totals) => Specs.RoleOf(player.SpecId) switch
    {
        Role.Tank => totals.Per(totals.Taken),
        Role.Healer => totals.Per(totals.Healing),
        _ => totals.Per(totals.Damage),
    };

    /// <summary>What somebody did across the attempts they were in, and over how long.</summary>
    private sealed class Totals
    {
        public long Damage { get; private set; }

        public long Healing { get; private set; }

        public long Taken { get; private set; }

        public double Seconds { get; private set; }

        public void Add(PlayerStats stats, double seconds)
        {
            Damage += stats.Damage;
            Healing += stats.Healing;
            Taken += stats.DamageTaken;
            Seconds += seconds;
        }

        public double Per(long total) => Seconds > 0.5 ? total / Seconds : 0;
    }

    private static GridCell Finish(Raw raw, string player, double worst)
        => new(raw.Present, raw.Mistakes, raw.Serious,
            !raw.Present ? "·" : Display.Mistakes(raw.Mistakes, raw.Serious),
            raw.Tooltip, raw.Attempt, player, Paint(raw, worst));

    /// <summary>
    /// How heavy a cell is, as one colour ramp from amber to red. One ramp, because the question a
    /// cell answers is "how much" and a second hue would be answering a different one.
    /// </summary>
    private static Brush Paint(Raw raw, double worst)
    {
        if (!raw.Present) return Missing;
        if (raw.Mistakes == 0) return Clean;

        double share = worst <= 0 ? 0 : Math.Clamp(raw.Mistakes / worst, 0, 1);

        return Frozen(Color.FromRgb(
            (byte)(0xE0 + (0xFF - 0xE0) * share),
            (byte)(0xA5 - (0xA5 - 0x70) * share),
            (byte)(0x54 - (0x54 - 0x6D) * share)));
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private readonly record struct Raw(bool Present, int Mistakes, int Serious, string Tooltip, int Attempt);
}
