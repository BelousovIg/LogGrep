using System.IO.Abstractions;
using System.Text.Json;

namespace LogGrep.Services;

/// <summary>
/// The journal as it arrived, kept on disk. Fetching needs a key and takes a minute; reading what
/// was fetched needs neither, so the rules can be rebuilt as often as the parse changes without
/// asking Blizzard again - which is the difference between iterating on a parser and waiting on a
/// network round trip for every idea.
/// </summary>
public sealed class JournalCache
{
    private readonly IFileSystem _fileSystem;

    /// <param name="keepRaw">
    /// Whether what arrives is also written to disk. A debug build keeps it, because changing the
    /// parse then costs a rebuild rather than another minute of somebody else.s API quota. A release
    /// build does not: nobody using the app has a reason to carry a hundred files of raw JSON.
    /// </param>
    public JournalCache(IFileSystem fileSystem, string folder, bool keepRaw)
    {
        _fileSystem = fileSystem;
        Folder = folder;
        KeepsRaw = keepRaw;
    }

    public string Folder { get; }

    public bool KeepsRaw { get; }

    public bool HasAnything => _fileSystem.Directory.Exists(Folder) && Files().Count > 0;

    /// <summary>Walks the newest expansion and returns every encounter it finds, raw and unchanged.</summary>
    public async Task<(string Expansion, List<JsonElement> Encounters)> FetchAsync(
        IJournalSource journal, IProgress<string>? progress, CancellationToken ct)
    {
        var expansion = await NewestExpansionAsync(journal, ct).ConfigureAwait(false);
        progress?.Report("Reading " + expansion.Name + "…");

        if (KeepsRaw) _fileSystem.Directory.CreateDirectory(Folder);
        var found = new List<JsonElement>();

        foreach (var instance in await InstancesOfAsync(journal, expansion.Id, ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(instance.Name + "…");

            foreach (int encounter in await EncountersOfAsync(journal, instance.Id, ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();

                var body = await journal.GetAsync("/data/wow/journal-encounter/" + encounter, ct)
                    .ConfigureAwait(false);

                found.Add(body);
                if (KeepsRaw) _fileSystem.File.WriteAllText(PathOf(encounter), body.ToString());
            }
        }

        if (KeepsRaw)
        {
            _fileSystem.File.WriteAllText(_fileSystem.Path.Combine(Folder, "expansion.txt"), expansion.Name);
        }

        return (expansion.Name, found);
    }

    public string Expansion
    {
        get
        {
            string path = _fileSystem.Path.Combine(Folder, "expansion.txt");
            return _fileSystem.File.Exists(path) ? _fileSystem.File.ReadAllText(path).Trim() : "an unnamed expansion";
        }
    }

    /// <summary>Every encounter kept from a previous fetch. Anything unreadable is skipped.</summary>
    public IEnumerable<JsonElement> Saved()
    {
        foreach (string file in Files())
        {
            JsonElement body;
            try
            {
                using var document = JsonDocument.Parse(_fileSystem.File.ReadAllText(file));
                body = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                continue;
            }

            yield return body;
        }
    }

    private List<string> Files()
        => _fileSystem.Directory.Exists(Folder)
            ? _fileSystem.Directory.GetFiles(Folder, "*.json").OrderBy(f => f, StringComparer.Ordinal).ToList()
            : new List<string>();

    private string PathOf(int encounter) => _fileSystem.Path.Combine(Folder, encounter + ".json");

    private static async Task<(int Id, string Name)> NewestExpansionAsync(IJournalSource journal, CancellationToken ct)
    {
        var index = await journal.GetAsync("/data/wow/journal-expansion/index", ct).ConfigureAwait(false);
        if (!index.TryGetProperty("tiers", out var tiers) || tiers.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The expansion index did not list any tiers.");
        }

        var newest = tiers.EnumerateArray()
            .Select(t => (Id: t.GetProperty("id").GetInt32(), Name: Journal.Text(t, "name")))
            .OrderByDescending(t => t.Id)
            .FirstOrDefault();

        if (newest.Id == 0) throw new InvalidOperationException("The expansion index was empty.");
        return newest;
    }

    private static async Task<List<(int Id, string Name)>> InstancesOfAsync(
        IJournalSource journal, int expansion, CancellationToken ct)
    {
        var body = await journal.GetAsync("/data/wow/journal-expansion/" + expansion, ct).ConfigureAwait(false);
        var instances = new List<(int, string)>();

        foreach (string group in new[] { "raids", "dungeons" })
        {
            if (!body.TryGetProperty(group, out var list) || list.ValueKind != JsonValueKind.Array) continue;

            instances.AddRange(list.EnumerateArray()
                .Select(i => (i.GetProperty("id").GetInt32(), Journal.Text(i, "name"))));
        }

        return instances;
    }

    private static async Task<List<int>> EncountersOfAsync(IJournalSource journal, int instance, CancellationToken ct)
    {
        var body = await journal.GetAsync("/data/wow/journal-instance/" + instance, ct).ConfigureAwait(false);
        if (!body.TryGetProperty("encounters", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return new List<int>();
        }

        return list.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();
    }
}
