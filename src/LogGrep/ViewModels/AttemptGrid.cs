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
    bool Present, double Pools, string Text, string Tooltip, int Attempt, string Player, Brush Paint);

/// <summary>One person's row across the attempts of a selection.</summary>
public sealed record GridRow(string Name, string FullName, Brush ClassBrush, IReadOnlyList<GridCell> Cells)
{
    /// <summary>What the whole selection cost them, which is what the rows sort on.</summary>
    public double Pools => Cells.Where(c => c.Present).Sum(c => c.Pools);

    public string PoolsText => Display.Decimal(Pools);

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

        var gathered = new List<(PlayerStats First, List<Raw> Cells)>(people.Count);
        double worst = 0;

        foreach (var person in people)
        {
            var cells = new List<Raw>(pulls.Count);
            bool anywhere = false;

            for (int i = 0; i < pulls.Count; i++)
            {
                var stats = pulls[i].Record.Roster
                    .FirstOrDefault(p => string.Equals(p.Name, person.Key, StringComparison.Ordinal));

                if (stats == null || (role != null && Specs.RoleOf(stats.SpecId) != role))
                {
                    cells.Add(new Raw(false, 0, "They were not in this attempt", i));
                    continue;
                }

                anywhere = true;
                var card = cards.For(pulls[i].Record, stats);
                worst = Math.Max(worst, card.Pools);

                cells.Add(new Raw(true, card.Pools,
                    card.Worst?.Line ?? "Nothing was found for them in this attempt", i));
            }

            if (anywhere) gathered.Add((person.First(), cells));
        }

        var rows = gathered
            .Select(entry => new GridRow(
                PlayerName.Character(entry.First.Name),
                PlayerName.Format(entry.First.Name),
                ClassBrushes.For(entry.First.ClassColor),
                entry.Cells.Select(c => Finish(c, entry.First.Name, worst)).ToArray()))
            .OrderByDescending(r => r.Pools)
            .ThenBy(r => r.Name, StringComparer.CurrentCulture)
            .ToArray();

        return new AttemptGrid(columns, rows);
    }

    private static GridCell Finish(Raw raw, string player, double worst)
        => new(raw.Present, raw.Pools,
            !raw.Present ? "·" : raw.Pools <= 0.01 ? string.Empty : Display.Decimal(raw.Pools),
            raw.Tooltip, raw.Attempt, player, Paint(raw, worst));

    /// <summary>
    /// How heavy a cell is, as one colour ramp from amber to red. One ramp, because the question a
    /// cell answers is "how much" and a second hue would be answering a different one.
    /// </summary>
    private static Brush Paint(Raw raw, double worst)
    {
        if (!raw.Present) return Missing;
        if (raw.Pools <= 0.01) return Clean;

        double share = worst <= 0 ? 0 : Math.Clamp(raw.Pools / worst, 0, 1);

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

    private readonly record struct Raw(bool Present, double Pools, string Tooltip, int Attempt);
}
