using System.Text.Json.Serialization;

namespace LogGrep.Services;

/// <summary>
/// Everything the app remembers between runs. The Blizzard secret is never held here in the clear -
/// only the protected form, which is what gets written to disk.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Client id of a Blizzard API client, from develop.battle.net. Not secret.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>The client secret, encrypted for this Windows account and base64 encoded.</summary>
    public string ProtectedClientSecret { get; set; } = string.Empty;

    /// <summary>Worked out, not stored - it has no business being written to the file.</summary>
    [JsonIgnore]
    public bool HasCredentials => ClientId.Length > 0 && ProtectedClientSecret.Length > 0;
}
