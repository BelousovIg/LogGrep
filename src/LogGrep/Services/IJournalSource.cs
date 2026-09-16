using System.Text.Json;

namespace LogGrep.Services;

/// <summary>
/// Where the encounter journal comes from. The seam exists so the rule builder can be driven by a
/// fixture in a test: the real one needs a key, and a key belongs to the person who registered it.
/// </summary>
public interface IJournalSource
{
    /// <summary>Fetches one endpoint, such as <c>/data/wow/journal-encounter/2517</c>.</summary>
    Task<JsonElement> GetAsync(string path, CancellationToken ct);
}
