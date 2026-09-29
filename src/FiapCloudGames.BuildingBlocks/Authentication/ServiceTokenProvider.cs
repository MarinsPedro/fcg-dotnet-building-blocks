using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace FiapCloudGames.BuildingBlocks.Authentication;

internal sealed class ServiceTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<ServiceTokenOptions> optionsMonitor,
    TimeProvider timeProvider) : IServiceTokenProvider
{
    internal const string HttpClientName =
        "FiapCloudGames.ServiceAuthentication";

    private readonly ConcurrentDictionary<string, CachedToken> cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new();

    public async ValueTask<string> GetAccessTokenAsync(
        string audience,
        string scopes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopes);

        var normalizedScopes = NormalizeScopes(scopes);
        var cacheKey = $"{audience.Trim()}::{normalizedScopes}";

        if (TryGetValidToken(cacheKey, out var token))
        {
            return token;
        }

        var gate = locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            if (TryGetValidToken(cacheKey, out token))
            {
                return token;
            }

            var issued = await RequestTokenAsync(
                audience.Trim(),
                normalizedScopes,
                cancellationToken);
            cache[cacheKey] = issued;
            return issued.AccessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool TryGetValidToken(string key, out string token)
    {
        if (cache.TryGetValue(key, out var cached) &&
            cached.ValidUntilUtc > timeProvider.GetUtcNow())
        {
            token = cached.AccessToken;
            return true;
        }

        token = string.Empty;
        return false;
    }

    private async Task<CachedToken> RequestTokenAsync(
        string audience,
        string scopes,
        CancellationToken cancellationToken)
    {
        var options = optionsMonitor.CurrentValue;
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret,
                    ["audience"] = audience,
                    ["scope"] = scopes
                })
        };

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"O Identity recusou a emissão do token de serviço com status " +
                $"{(int)response.StatusCode}.",
                inner: null,
                response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(
            cancellationToken);

        var accessToken = payload?.ResolveAccessToken();
        var expiresIn = payload?.ResolveExpiresIn() ?? 0;
        if (string.IsNullOrWhiteSpace(accessToken) || expiresIn <= 0)
        {
            throw new InvalidOperationException(
                "O Identity retornou uma resposta de token inválida.");
        }

        var usableLifetime = TimeSpan.FromSeconds(expiresIn) -
            options.RefreshBeforeExpiration;
        if (usableLifetime <= TimeSpan.Zero)
        {
            usableLifetime = TimeSpan.FromSeconds(1);
        }

        return new CachedToken(
            accessToken,
            timeProvider.GetUtcNow().Add(usableLifetime));
    }

    private static string NormalizeScopes(string scopes) =>
        string.Join(
            ' ',
            scopes.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));

    private sealed record CachedToken(
        string AccessToken,
        DateTimeOffset ValidUntilUtc);

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? OAuthAccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int? OAuthExpiresIn { get; init; }

        [JsonPropertyName("accessToken")]
        public string? DotNetAccessToken { get; init; }

        [JsonPropertyName("expiresIn")]
        public int? DotNetExpiresIn { get; init; }

        public string? ResolveAccessToken() =>
            OAuthAccessToken ?? DotNetAccessToken;

        public int ResolveExpiresIn() =>
            OAuthExpiresIn ?? DotNetExpiresIn ?? 0;
    }
}
