using System.Text.Json;
using LogGrep.Services;

namespace LogGrep.Tests.Logs;

/// <summary>
/// A journal made of pages a test wrote. Nothing in the suite reaches Blizzard: the real source
/// needs a key, and a key belongs to whoever registered it and to nobody else.
/// </summary>
public sealed class AJournal : IJournalSource
{
    private readonly Dictionary<string, string> _pages = new(StringComparer.Ordinal);

    public AJournal Page(string path, string json)
    {
        _pages[path] = json;
        return this;
    }

    public Task<JsonElement> GetAsync(string path, CancellationToken ct)
    {
        if (!_pages.TryGetValue(path, out string? json))
        {
            throw new InvalidOperationException(
                $"The journal in this test has no page at '{path}'. It has: {string.Join(", ", _pages.Keys)}");
        }

        return Task.FromResult(JsonDocument.Parse(json).RootElement.Clone());
    }
}
