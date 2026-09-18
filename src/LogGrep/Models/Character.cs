namespace LogGrep.Models;

/// <summary>
/// One character, as everything that has been read about them adds up.
///
/// Keyed by the log's own identifier rather than by a name, so a realm transfer or a rename joins
/// the rows instead of splitting a person in two. The name shown is the most recent one seen, since
/// that is the one somebody would recognise.
///
/// <see cref="Roles"/> is a set on purpose. A damage dealer who respecs into a tank for three pulls
/// is one character who did two jobs, and any list that forces them into one of the two is wrong
/// about the evening. What they were on a given attempt is a property of that attempt.
/// </summary>
public sealed record Character(string Guid, string Name)
{
    /// <summary>Every specialization they were seen in, most-played first.</summary>
    public IReadOnlyList<int> Specs { get; init; } = Array.Empty<int>();

    /// <summary>Every role those specializations amount to, most-played first.</summary>
    public IReadOnlyList<Role> Roles { get; init; } = Array.Empty<Role>();

    public int Pulls { get; init; }

    public int Encounters { get; init; }

    public DateTime FirstSeen { get; init; }

    public DateTime LastSeen { get; init; }

    /// <summary>The character on its own; the realm is what the tooltip carries.</summary>
    public string Who => PlayerName.Character(Name);

    public string Realm => PlayerName.Realm(Name);

    /// <summary>The class of whatever they played most, empty when no spec was ever reported.</summary>
    public string ClassName => Specs.Count == 0 ? string.Empty : Specs.Select(Models.Specs.ClassOf).First();

    public string ClassColor => Specs.Count == 0 ? string.Empty : Models.Specs.ColorOf(Specs[0]);
}

/// <summary>Gathers the characters out of everything that has been read.</summary>
public static class People
{
    public static IReadOnlyList<Character> In(IEnumerable<PullRecord> pulls)
    {
        var seen = new Dictionary<string, Gathering>(StringComparer.Ordinal);

        foreach (var pull in pulls)
        {
            foreach (var player in pull.Roster)
            {
                // A log old enough to predate the identifier, or a line too damaged to carry one,
                // falls back to the name. It is worse, and it is better than dropping the person.
                string key = player.Guid.Length > 0 ? player.Guid : player.Name;

                if (!seen.TryGetValue(key, out var gathering))
                {
                    seen[key] = gathering = new Gathering(key);
                }

                gathering.Add(pull, player);
            }
        }

        return seen.Values
            .Select(g => g.Done())
            .OrderByDescending(c => c.Pulls)
            .ThenBy(c => c.Name, StringComparer.CurrentCulture)
            .ToArray();
    }

    private sealed class Gathering
    {
        private readonly string _key;
        private readonly Dictionary<int, int> _specs = new();
        private readonly HashSet<string> _encounters = new(StringComparer.Ordinal);
        private string _name = string.Empty;
        private DateTime _named;

        public Gathering(string key) => _key = key;

        public int Pulls { get; private set; }

        public DateTime First { get; private set; } = DateTime.MaxValue;

        public DateTime Last { get; private set; } = DateTime.MinValue;

        public void Add(PullRecord pull, PlayerStats player)
        {
            Pulls++;
            _encounters.Add(pull.GroupKey);

            if (pull.StartTime < First) First = pull.StartTime;
            if (pull.StartTime > Last) Last = pull.StartTime;

            // The latest spelling wins: somebody who transferred last week is called by the realm
            // they are on now, not by the one they left.
            if (pull.StartTime >= _named || _name.Length == 0)
            {
                _name = player.Name;
                _named = pull.StartTime;
            }

            if (player.SpecId <= 0) return;

            _specs.TryGetValue(player.SpecId, out int count);
            _specs[player.SpecId] = count + 1;
        }

        public Character Done()
        {
            var specs = _specs.OrderByDescending(e => e.Value).Select(e => e.Key).ToArray();

            return new Character(_key, _name)
            {
                Specs = specs,
                Roles = specs.Select(Models.Specs.RoleOf).Distinct().ToArray(),
                Pulls = Pulls,
                Encounters = _encounters.Count,
                FirstSeen = First == DateTime.MaxValue ? default : First,
                LastSeen = Last == DateTime.MinValue ? default : Last,
            };
        }
    }
}
