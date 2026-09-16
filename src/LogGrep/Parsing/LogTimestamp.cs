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
}
