namespace LogGrep.Models;

/// <summary>
/// Several log files read as one body of evidence.
///
/// A tier is fought over several nights and several files, and the app needs ten attempts at a boss
/// before it will call anything a rule - a floor one night's file often cannot reach on its own.
/// Read together they clear it easily, and the keys that group attempts inside one file group them
/// across files unchanged: a boss and a difficulty. Two nights of mythic pulls on one boss become
/// one run of attempts; the heroic ones stay where they are.
/// </summary>
public sealed class Reading
{
    public static readonly Reading Nothing = new(Array.Empty<LogSource>(), Array.Empty<PullRecord>(),
        Array.Empty<string>());

    private Reading(IReadOnlyList<LogSource> sources, IReadOnlyList<PullRecord> pulls, IReadOnlyList<string> notes)
    {
        Sources = sources;
        Pulls = pulls;
        Notes = notes;
    }

    /// <summary>The files, in the order the evening ran.</summary>
    public IReadOnlyList<LogSource> Sources { get; }

    /// <summary>Every attempt from every file, in that order, with the duplicates already gone.</summary>
    public IReadOnlyList<PullRecord> Pulls { get; }

    /// <summary>
    /// What the person should be told about the reading itself rather than about the fights: a file
    /// whose date does not match what is inside it, attempts that were in two of the files at once.
    /// </summary>
    public IReadOnlyList<string> Notes { get; }

    public bool IsEmpty => Pulls.Count == 0;

    public static Reading Of(IEnumerable<ScanResult> scans)
    {
        // Ordered by what the log itself says rather than by what the filesystem says, because the
        // filesystem is the half that can be wrong: copying a log resets its creation date, and an
        // evening presented backwards is worse than one presented with a note attached.
        var ordered = scans
            .OrderBy(s => s.Source.Recorded)
            .ThenBy(s => s.Source.Created)
            .ToList();

        for (int i = 0; i < ordered.Count; i++) ordered[i].Source.Order = i;

        var notes = new List<string>();
        foreach (var scan in ordered)
        {
            if (scan.Source.TimeIsSuspect)
            {
                notes.Add($"{scan.Source.Name} says it was made {scan.Source.Created:d}, but the log inside it " +
                          $"was recorded {scan.Source.Recorded:d}. It has been placed by what is inside it.");
            }

            if (scan.Source.NameIsSuspect)
            {
                notes.Add($"{scan.Source.Name} is named for {scan.Source.Named:g}, and the log inside it starts " +
                          $"at {scan.Source.Recorded:g}. It has been placed by what is inside it.");
            }
        }

        var pulls = Deduplicate(ordered, notes);

        return new Reading(
            ordered.Select(s => s.Source).ToList(),
            pulls,
            notes);
    }

    /// <summary>
    /// An exported slice opened next to the log it was cut from holds the same attempts twice, and
    /// a doubled attempt inflates every count while a doubled mistake looks like a habit - which is
    /// the one thing the ten-attempt floor exists to prevent. The same fight in two files starts at
    /// the same second, so that is what the match rests on.
    ///
    /// Which copy survives is decided by which file holds more of the night: given a full log and a
    /// slice taken out of it, the full log is the one worth keeping open.
    /// </summary>
    private static List<PullRecord> Deduplicate(List<ScanResult> ordered, List<string> notes)
    {
        var richest = ordered
            .OrderByDescending(s => s.Pulls.Count)
            .ThenByDescending(s => s.Source.Size)
            .ToList();

        var kept = new Dictionary<(string Group, DateTime At), PullRecord>();

        foreach (var scan in richest)
        {
            int survived = 0;
            foreach (var pull in scan.Pulls)
            {
                if (kept.TryAdd((pull.GroupKey, Second(pull.StartTime)), pull)) survived++;
            }

            scan.Source.Kept = survived;
        }

        // A file that contributed nothing is a copy of another - the same night under a second
        // name, or the same file reached by a second path. Saying "34 attempts were counted once"
        // about that is technically true and useless; what happened is that a whole file was a
        // duplicate, and that is what the person needs to hear.
        var copies = ordered.Where(s => s.Pulls.Count > 0 && s.Source.Kept == 0).ToList();
        foreach (var copy in copies)
        {
            notes.Add($"{copy.Source.Name} holds the same {copy.Pulls.Count} " +
                      $"{(copy.Pulls.Count == 1 ? "attempt" : "attempts")} as another of these files and " +
                      "nothing else. It has been read once.");
        }

        int shared = ordered.Sum(s => s.Pulls.Count - s.Source.Kept) - copies.Sum(c => c.Pulls.Count);
        if (shared > 0)
        {
            notes.Add($"{shared} {(shared == 1 ? "attempt was" : "attempts were")} in more than one of these " +
                      "files and counted once.");
        }

        // Back into reading order: the files as the evening ran, the attempts as they sit in each.
        return kept.Values
            .OrderBy(p => p.Source.Order)
            .ThenBy(p => p.StartOffset)
            .ToList();
    }

    /// <summary>The same fight written twice can differ by fractions; the second it began cannot.</summary>
    private static DateTime Second(DateTime value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Kind);
}
