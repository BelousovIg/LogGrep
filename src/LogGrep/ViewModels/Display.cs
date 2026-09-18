using System.Globalization;

namespace LogGrep.ViewModels;


/// <summary>
/// Every value this app turns into text goes through here.
///
/// Not a convenience - a rule. Three separate locale bugs were shipped before it was one: a rate
/// written "2,5" on a Russian install, a month written "вер.", a file name that would carry a
/// different year under a non-Gregorian calendar. Each was one call site that formatted a value
/// itself, and each read as correct on the machine it was written on.
///
/// So the formatting is invariant, and it lives in one place. A log is a technical artefact: it
/// gets pasted into chats, compared between people and read by other tools, and a number that
/// changes shape with the reader's regional settings is a number nobody can quote back.
/// </summary>
public static class Display
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Rate(double value) => value switch
    {
        >= 1_000_000_000 => (value / 1_000_000_000).ToString("0.##", Inv) + "B",
        >= 1_000_000 => (value / 1_000_000).ToString("0.##", Inv) + "M",
        >= 1_000 => (value / 1_000).ToString("0.#", Inv) + "K",
        > 0 => value.ToString("0", Inv),
        _ => "—",
    };

    /// <summary>
    /// A stretch of seconds, as seconds. Whole ones: a rotation measured to the tenth of a second is
    /// a precision the measure does not have, and a column that reads "35s" is read at a glance
    /// where "0:35" is read as a moment of the fight.
    /// </summary>
    public static string Seconds(double value)
        => value < 0.5 ? "—" : Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", Inv) + "s";

    /// <summary>
    /// The time of day a thing happened, to the minute. Invariant and twenty-four hour, because an
    /// evening of pulls is read as a sequence and "9:05 PM" sorts by eye no better than it reads.
    /// </summary>
    public static string TimeOfDay(DateTime when) => when.ToString("HH:mm", Inv);

    /// <summary>
    /// A count of mistakes with the serious ones marked beside it: nothing, "!", or "!!" for two or
    /// more. Twelve small things and one serious one are the same number and not the same night, and
    /// a mark is the cheapest thing that can say so inside a cell the width of two characters.
    /// </summary>
    public static string Mistakes(int all, int serious)
        => all == 0 ? string.Empty : Count(all) + serious switch
        {
            0 => string.Empty,
            1 => " !",
            _ => " !!",
        };

    /// <summary>A plain count of things. Invariant so that no locale puts a separator in it.</summary>
    public static string Count(int value) => value.ToString(Inv);

    /// <summary>A raw total, scaled the same way as a rate.</summary>
    public static string Amount(long value) => Rate(value);

    /// <summary>
    /// A share, whole-numbered. A hit can land for more than a full health pool, and rounding that
    /// down to 100% would hide exactly how far past survivable it was.
    /// </summary>
    public static string Percent(double value) => Math.Round(value * 100).ToString("0", Inv) + "%";

    /// <summary>
    /// A small number with at most one decimal. Invariant, like everything else here: the app is
    /// read on a machine whose locale writes 2,5 and copied into a chat where that is a second
    /// number.
    /// </summary>
    public static string Decimal(double value) => value.ToString("0.#", Inv);

    /// <summary>
    /// A day and a time, invariant like the rest. A log is a technical artefact that gets pasted
    /// into chats and compared across machines, and "15 Sep 20:00" means the same thing on all of
    /// them where a localised month does not.
    /// </summary>
    public static string Moment(DateTime value) => value.ToString("d MMM HH:mm", Inv);

    /// <summary>Time inside a pull as "m:ss", the shape death times are read in.</summary>
    public static string Clock(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        return (int)value.TotalMinutes + ":" + value.Seconds.ToString("00", Inv);
    }

    public static string Duration(TimeSpan value)
    {
        if (value <= TimeSpan.Zero) return "—";
        return value.TotalHours >= 1
            ? ((int)value.TotalHours) + ":" + value.ToString(@"mm\:ss")
            : ((int)value.TotalMinutes) + ":" + value.ToString(@"ss", Inv);
    }
}
