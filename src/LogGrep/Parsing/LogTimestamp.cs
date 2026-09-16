using System.Globalization;

namespace LogGrep.Parsing;

/// <summary>Parses the leading timestamp of a combat log line.</summary>
internal static class LogTimestamp
{
    private static readonly string[] Formats =
    {
        "M/d/yyyy H:mm:ss.fff",
        "M/d/yyyy H:mm:ss.ffff",
        "M/d/yyyy H:mm:ss.ff",
        "M/d/yyyy H:mm:ss",
        "M/d H:mm:ss.fff",
        "M/d H:mm:ss.ffff",
        "M/d H:mm:ss",
    };

    private static readonly double[] Scale = { 1, 10, 100, 1000, 10000 };

    /// <summary>
    /// Handles both "9/15/2026 20:15:31.123-4" (retail, with a UTC offset suffix)
    /// and the older "9/15 20:15:31.123" shape.
    /// </summary>
    public static DateTime Parse(string raw, DateTime fallback)
    {
        string text = raw.Trim();

        // Strip the trailing timezone offset, which starts after the fractional seconds.
        int dot = text.LastIndexOf('.');
        if (dot >= 0)
        {
            int sign = text.IndexOfAny(new[] { '-', '+' }, dot);
            if (sign > 0) text = text[..sign];
        }

        if (DateTime.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;

        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed) ? parsed : fallback;
    }

    /// <summary>
    /// Seconds since midnight, read straight off the raw bytes. Death times need a timestamp on
    /// events that fire millions of times per log, which rules out <see cref="DateTime"/> parsing;
    /// only the "H:mm:ss.fff" tail is read, and the date is ignored. Returns -1 when unreadable.
    /// </summary>
    public static double SecondsOfDay(ReadOnlySpan<byte> line, int eventStart)
    {
        int end = Math.Max(0, eventStart - 2);
        if (end <= 0 || end > line.Length) return -1;

        var region = line[..end];
        int space = region.LastIndexOf((byte)' ');
        var time = space >= 0 ? region[(space + 1)..] : region;

        int at = 0;
        int hours = ReadInt(time, ref at);
        if (at >= time.Length || time[at] != (byte)':') return -1;
        at++;

        int minutes = ReadInt(time, ref at);
        if (at >= time.Length || time[at] != (byte)':') return -1;
        at++;

        int seconds = ReadInt(time, ref at);

        double fraction = 0;
        if (at < time.Length && time[at] == (byte)'.')
        {
            at++;
            int from = at;
            int value = ReadInt(time, ref at);
            int digits = at - from;
            if (digits > 0 && digits < Scale.Length) fraction = value / Scale[digits];
        }

        return hours * 3600 + minutes * 60 + seconds + fraction;
    }

    private static int ReadInt(ReadOnlySpan<byte> span, ref int at)
    {
        int value = 0;
        while (at < span.Length && span[at] >= (byte)'0' && span[at] <= (byte)'9')
        {
            value = value * 10 + (span[at] - '0');
            at++;
        }

        return value;
    }
}
