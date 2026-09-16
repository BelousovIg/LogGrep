using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LogGrep.Services;

/// <summary>
/// The real journal, over HTTP. A token is fetched once and reused: it lasts a day and the whole
/// walk takes seconds, so nothing here refreshes one mid-flight.
/// </summary>
public sealed class BlizzardApi : IJournalSource, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _region;
    private string? _token;

    public BlizzardApi(string clientId, string clientSecret, string region = "eu", HttpClient? http = null)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _region = region;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<JsonElement> GetAsync(string path, CancellationToken ct)
    {
        await EnsureTokenAsync(ct).ConfigureAwait(false);

        string separator = path.Contains('?') ? "&" : "?";
        string url = $"https://{_region}.api.blizzard.com{path}{separator}namespace=static-{_region}&locale=en_US";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        return document.RootElement.Clone();
    }

    public void Dispose() => _http.Dispose();

    /// <summary>
    /// The client credentials grant: the id and secret are exchanged for a bearer token. They are
    /// sent once, over TLS, and never written anywhere.
    /// </summary>
    private async Task EnsureTokenAsync(CancellationToken ct)
    {
        if (_token != null) return;

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.battle.net/token")
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
            }),
        };

        string pair = Convert.ToBase64String(Encoding.UTF8.GetBytes(_clientId + ":" + _clientSecret));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", pair);

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                "Blizzard refused the client id and secret (" + (int)response.StatusCode + "). " +
                "Check them in settings, or make sure the client is still listed at develop.battle.net.");
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        _token = document.RootElement.GetProperty("access_token").GetString();
    }
}
