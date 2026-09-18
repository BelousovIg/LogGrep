using LogGrep.Models;

namespace LogGrep.Analysis;

/// <summary>One number measured for one player on one attempt - the unit a baseline is built from.</summary>
public readonly record struct Measured(PullRecord Pull, string Player, int SpecId, double Value);

/// <summary>
/// What "usual" turned out to be, and whose usual it is. The second half is not decoration: advice
/// is accepted in the form "you normally do this and this time you did not", and refused in the
/// form "somebody does this" - so the sentence has to say which of the two it is.
/// </summary>
public readonly record struct Normal(double Value, string Whose)
{
    public bool Exists => Whose.Length > 0;

    public static readonly Normal None = new(0, string.Empty);
}

/// <summary>
/// The baselines, in one place, so a detector picks one instead of inventing one.
///
/// Three exist and they are tried in order of how much a person will accept them. Your own
/// attempts at this fight come first: nobody argues with their own log. Failing that - a night
/// where you only pulled three times - another player of the same specialization in the same log
/// is the next best thing, because whatever a spec is supposed to do, they were doing it under the
/// same conditions on the same night.
///
/// The third baseline, the group on one attempt, belongs to the mechanics rules rather than here:
/// those compare people to each other within a single moment, which is a different shape of
/// question and already has its own arithmetic.
/// </summary>
public sealed class Yardstick
{
    /// <summary>
    /// How many attempts somebody else's numbers need before they can stand in for yours. Fewer
    /// than your own take, deliberately: this baseline exists for the short night, and holding it
    /// to the same bar means it never applies on the nights that need it. The sentence says whose
    /// numbers they are, so a reader can weigh it themselves.
    /// </summary>
    private const int Borrowed = 3;

    /// <summary>
    /// Keyed by the person and what they were playing, not by the person. Somebody who tanks a night
    /// and heals three pulls of it is doing two jobs, and a baseline that averages across the respec
    /// is a number about nobody: it would tell a tank their healing was down and a healer their
    /// damage was up, in the same sentence, about the same evening.
    /// </summary>
    private readonly Dictionary<(string Player, int Spec), List<double>> _mine = new();
    private readonly Dictionary<int, List<(string Player, double Value)>> _spec = new();
    private readonly int _minimum;

    private Yardstick(int minimum) => _minimum = minimum;

    /// <summary>
    /// Builds the baselines from every measurement taken. <paramref name="minimum"/> is how many
    /// attempts it takes before a number counts as somebody's normal rather than as an accident.
    /// </summary>
    public static Yardstick Of(IEnumerable<Measured> measurements, int minimum)
    {
        var stick = new Yardstick(minimum);

        foreach (var measured in measurements)
        {
            if (!stick._mine.TryGetValue((measured.Player, measured.SpecId), out var mine))
            {
                stick._mine[(measured.Player, measured.SpecId)] = mine = new List<double>();
            }

            mine.Add(measured.Value);

            if (measured.SpecId <= 0) continue;

            if (!stick._spec.TryGetValue(measured.SpecId, out var spec))
            {
                stick._spec[measured.SpecId] = spec = new List<(string, double)>();
            }

            spec.Add((measured.Player, measured.Value));
        }

        return stick;
    }

    /// <summary>
    /// What this player usually manages, or failing that what somebody else of their spec manages.
    /// Returns <see cref="Normal.None"/> when neither has enough behind it to be worth quoting.
    /// </summary>
    public Normal For(string player, int specId)
    {
        if (_mine.TryGetValue((player, specId), out var mine) && mine.Count >= _minimum)
        {
            return new Normal(Median(mine), "you average");
        }

        if (specId > 0 && _spec.TryGetValue(specId, out var spec))
        {
            // Somebody else's numbers, or it is not another player's baseline at all.
            var others = spec.Where(e => !string.Equals(e.Player, player, StringComparison.Ordinal))
                .Select(e => e.Value)
                .ToList();

            if (others.Count >= Borrowed)
            {
                return new Normal(Median(others), "another " + Specs.SpecOf(specId) + " in this log averages");
            }
        }

        return Normal.None;
    }

    /// <summary>
    /// The best that has actually been shown, in the same order of preference. A score wants a
    /// ceiling rather than a normal - "how close to your best was this" - and the one thing the log
    /// cannot supply is the best that was possible. It can only ever report the best that happened,
    /// which is why 100% here means "your best so far" and never "perfect".
    /// </summary>
    public Normal Best(string player, int specId)
    {
        if (_mine.TryGetValue((player, specId), out var mine) && mine.Count >= _minimum)
        {
            return new Normal(mine.Max(), "your best of " + mine.Count + " attempts");
        }

        if (specId > 0 && _spec.TryGetValue(specId, out var spec))
        {
            var others = spec.Where(e => !string.Equals(e.Player, player, StringComparison.Ordinal))
                .Select(e => e.Value)
                .ToList();

            if (others.Count >= Borrowed)
            {
                return new Normal(others.Max(), "the best another " + Specs.SpecOf(specId) + " managed here");
            }
        }

        return Normal.None;
    }

    public static double Median(List<double> values)
    {
        if (values.Count == 0) return 0;

        values.Sort();
        int middle = values.Count / 2;

        return values.Count % 2 == 1
            ? values[middle]
            : (values[middle - 1] + values[middle]) / 2;
    }
}
