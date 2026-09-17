using LogGrep.Models;

namespace LogGrep.Analysis;

/// <summary>
/// The moments one thing caught much of the group at once.
///
/// Separating these from personal mistakes is the difference between a report worth reading and one
/// that cries wolf on every wipe. One mechanic that lands on seven people is not seven mistakes,
/// and the last four seconds of a wipe are not twenty people each choosing to die.
///
/// It is computed from what actually happened rather than from the findings alone, because half of
/// a wipe is deaths no rule has a word about - and a moment that is invisible to the rules is
/// exactly the moment a score must not charge to somebody personally.
/// </summary>
public static class Collective
{
    /// <summary>
    /// How close together two things have to be to count as one moment. A mechanic goes out once
    /// and lands on everybody it catches inside a heartbeat; wider than this and two unrelated
    /// mistakes start reading as one event.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(2);

    public static IReadOnlyList<TimeSpan> In(PullRecord pull, IReadOnlyList<Finding> found)
    {
        int roster = pull.Roster.Count;

        // Below this there is no "much of the group" to speak of: in a party of two, one person is
        // half of it, and calling that collective would excuse every mistake either of them made.
        if (roster < 3) return Array.Empty<TimeSpan>();

        int enough = roster / 2 + 1;

        var moments = found
            .Where(f => f.Player.Length > 0 && string.Equals(f.Pull.GroupKey, pull.GroupKey, StringComparison.Ordinal))
            .Select(f => (f.Player, f.At))
            .Concat(pull.Roster.SelectMany(p => p.Deaths.Select(d => (Player: p.Name, d.At))));

        return moments
            .GroupBy(m => (long)(m.At.TotalSeconds / Window.TotalSeconds))
            .Where(g => g.Select(m => m.Player).Distinct(StringComparer.Ordinal).Count() >= enough)
            .Select(g => TimeSpan.FromSeconds(g.Min(m => m.At.TotalSeconds)))
            .OrderBy(t => t)
            .ToArray();
    }

    /// <summary>Whether a moment falls inside one of them.</summary>
    public static bool Covers(IReadOnlyCollection<TimeSpan> moments, TimeSpan at)
        => moments.Any(m => Math.Abs((m - at).TotalSeconds) <= Window.TotalSeconds);
}
