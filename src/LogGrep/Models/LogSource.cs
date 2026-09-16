using System.Globalization;

namespace LogGrep.Models;

/// <summary>
/// One file the attempts were read out of.
///
/// Every byte range in the model - the fight, the header, the zone and map context - is an offset
/// into one particular file, and export copies raw bytes back out of it. So the file travels with
/// the attempt rather than with the window: once several logs are open at once, "the source" is no
/// longer a single thing the window can remember.
/// </summary>
public sealed class LogSource
{
    public required string Path { get; init; }

    /// <summary>Just the file name, which is what a message about this file should say.</summary>
    public required string Name { get; init; }

    public long Size { get; init; }

    /// <summary>The COMBAT_LOG_VERSION line, re-emitted at the top of every exported file.</summary>
    public ByteRange Header { get; set; } = ByteRange.Empty;

    /// <summary>
    /// When the first event in the file happened. This is the one time that cannot be wrong: it was
    /// written by the game as the log was being recorded, and copying the file cannot touch it.
    /// </summary>
    public DateTime Recorded { get; set; }

    /// <summary>When the filesystem says the file was made, which a copy quietly resets to today.</summary>
    public DateTime Created { get; init; }

    /// <summary>What the file name claims, when it is named the way the game names one.</summary>
    public DateTime? Named { get; init; }

    public int Pulls { get; set; }

    /// <summary>How many of those survived the joining - none, if this file is a copy of another.</summary>
    public int Kept { get; set; }

    /// <summary>Where this file sits in the reading, so anything ordering attempts can follow it.</summary>
    public int Order { get; set; }

    /// <summary>
    /// Whether the filesystem and the log itself disagree about when this was. A day is generous on
    /// purpose - a timezone or a slow copy is not worth remarking on, a file carried over from
    /// another machine is.
    /// </summary>
    public bool TimeIsSuspect => Recorded != default && (Created - Recorded).Duration() > TimeSpan.FromDays(1);

    /// <summary>
    /// The name claims one night and the log inside it holds another, which means a renamed or
    /// misfiled log. An hour of slack covers the gap between the game opening the file and the
    /// first event landing in it.
    /// </summary>
    public bool NameIsSuspect => Named is { } named && Recorded != default
        && (named - Recorded).Duration() > TimeSpan.FromHours(1);

    /// <summary>
    /// "WoWCombatLog-091526_195625.txt" is month, day, year, then the time. Anything else is
    /// somebody's own name for the file and says nothing about when it was.
    /// </summary>
    public static DateTime? TimeInName(string fileName)
    {
        int dash = fileName.IndexOf('-');
        if (dash < 0) return null;

        string stamp = System.IO.Path.GetFileNameWithoutExtension(fileName)[(dash + 1)..];
        return DateTime.TryParseExact(stamp, "MMddyy_HHmmss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed) ? parsed : null;
    }
}
