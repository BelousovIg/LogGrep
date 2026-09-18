using LogGrep.Models;

namespace LogGrep.ViewModels;

/// <summary>
/// What the chart says at one second: the clock, then every line being drawn, then whatever happened
/// there that is a mark rather than a line.
///
/// Four lines against four different scales leave no honest way to put numbers on an axis, so the
/// numbers live in the hover instead - which makes this sentence the chart's only reading of itself,
/// and worth testing as such. The control holds the pointer; this holds the words.
///
/// A line that has been switched off is left out. Turning a switch off is somebody saying they are
/// not asking about that line, and a readout that answers anyway is the reason they turned it off.
/// </summary>
public static class ChartReadout
{
    public static IReadOnlyList<string> At(IReadOnlyList<Trace>? traces, IReadOnlyList<int>? deaths,
        IReadOnlyList<BossKill>? kills, int second)
    {
        var said = new List<string> { "at " + Display.Clock(TimeSpan.FromSeconds(second)) };

        foreach (var trace in traces ?? Array.Empty<Trace>())
        {
            if (!trace.IsOn) continue;

            string line = trace.At(second);
            if (line.Length > 0) said.Add(line);
        }

        // A death is a mark rather than a line, and so is a kill, so neither has a switch to obey.
        int died = deaths?.Count(d => Math.Abs(d - second) <= 1) ?? 0;
        if (died > 0) said.Add(died == 1 ? "somebody died here" : died + " died here");

        // Named rather than "the enemy died here": inside a keystone run there are three of these on
        // one chart, and which of them is under the pointer is the whole question.
        foreach (var kill in kills ?? Array.Empty<BossKill>())
        {
            if (Math.Abs(kill.Second - second) <= 1) said.Add(kill.Name + " died here");
        }

        return said;
    }
}
