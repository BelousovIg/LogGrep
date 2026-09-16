using LogGrep.Models;

namespace LogGrep.ViewModels;

/// <summary>One group member inside the roster table of a pull.</summary>
public sealed class PlayerRowViewModel
{
    private readonly PlayerStats _stats;
    private readonly double _seconds;

    public PlayerRowViewModel(PlayerStats stats, TimeSpan duration)
    {
        _stats = stats;
        _seconds = duration.TotalSeconds;
    }

    public string Name => PlayerName.Format(_stats.Name);

    public string ClassName => _stats.ClassName;

    public string SpecName => _stats.SpecName;

    public string DpsText => Display.Rate(Rate(_stats.Damage));

    public string HpsText => Display.Rate(Rate(_stats.Healing));

    /// <summary>Damage taken per second, the other half of what a pull costs a player.</summary>
    public string DtpsText => Display.Rate(Rate(_stats.DamageTaken));

    public bool Died => _stats.Deaths.Count > 0;

    /// <summary>Every death as "m:ss", or "-:--" for a player who survived the pull.</summary>
    public string DeathsText => _stats.Deaths.Count == 0
        ? "-:--"
        : string.Join(", ", _stats.Deaths.Select(d => Display.Clock(d.At)));

    /// <summary>
    /// What was hitting the player over the last seconds before each death. A single death reads
    /// as a plain list; several are bracketed so it stays clear which list belongs to which death.
    /// </summary>
    public string CausesText
    {
        get
        {
            if (_stats.Deaths.Count == 0) return "—";
            if (_stats.Deaths.Count == 1) return Causes(_stats.Deaths[0]);

            return string.Join(" ", _stats.Deaths.Select(d => "[" + Causes(d) + "]"));
        }
    }

    public string CausesTooltip => _stats.Deaths.Count == 0
        ? "This player did not die"
        : string.Join(Environment.NewLine, _stats.Deaths.Select(
            (d, i) => "Death " + (i + 1) + " at " + Display.Clock(d.At) + ": " + Causes(d)));

    private static string Causes(DeathRecord death) => death.Causes.Count == 0
        ? "no damage logged"
        : string.Join(", ", death.Causes.Select(c => c.Label + " " + Display.Amount(c.Amount)));

    private double Rate(long total) => _seconds > 0.5 ? total / _seconds : 0;
}
