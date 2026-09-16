namespace LogGrep.Tests.Logs;

/// <summary>
/// Moments in a fight, written as what they are. "1:30" is a string that has to be parsed and can
/// be mistyped as "1:60"; <c>1.Minutes(30)</c> is a TimeSpan the compiler has already agreed with,
/// and it sorts, adds and compares without anybody writing a parser for it.
/// </summary>
public static class Clock
{
    public static TimeSpan Seconds(this int seconds) => TimeSpan.FromSeconds(seconds);

    public static TimeSpan Minutes(this int minutes) => TimeSpan.FromMinutes(minutes);

    public static TimeSpan Minutes(this int minutes, int seconds) => new(0, minutes, seconds);
}
