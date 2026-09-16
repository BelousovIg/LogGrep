using System.Text.Json;

namespace LogGrep.Services;

/// <summary>Small readers shared by everything that walks a journal document.</summary>
internal static class Journal
{
    public static string Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
