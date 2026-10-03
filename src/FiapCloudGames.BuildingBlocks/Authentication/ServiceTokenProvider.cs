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
    private readonly SemaphoreSlim discoveryLock = new(1, 1);
    private CachedTokenEndpoint? cachedTokenEndpoint;

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

            var options = optionsMonitor.CurrentValue;
            var tokenEndpoint = await ResolveTokenEndpointAsync(
                options,
                cancellationToken);
            var issued = await RequestTokenAsync(
                options,
                tokenEndpoint,
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

    private async Task<Uri> ResolveTokenEndpointAsync(
        ServiceTokenOptions options,
        CancellationToken cancellationToken)
    {
        if (options.TokenEndpoint is not null)
        {
            return options.TokenEndpoint;
        }

        var metadataAddress = ResolveMetadataAddress(options);
        var cached = cachedTokenEndpoint;
        if (cached is not null &&
            cached.MetadataAddress == metadataAddress &&
            cached.ValidUntilUtc > timeProvider.GetUtcNow())
        {
            return cached.TokenEndpoint;
        }

        await discoveryLock.WaitAsync(cancellationToken);
        try
        {
            cached = cachedTokenEndpoint;
            if (cached is not null &&
                cached.MetadataAddress == metadataAddress &&
                cached.ValidUntilUtc > timeProvider.GetUtcNow())
            {
                return cached.TokenEndpoint;
            }

            var discovered = await RequestDiscoveryDocumentAsync(
                metadataAddress,
                options.RequireHttps,
                cancellationToken);
            cachedTokenEndpoint = new CachedTokenEndpoint(
                metadataAddress,
                discovered,
                timeProvider.GetUtcNow().Add(options.DiscoveryRefreshInterval));
            return discovered;
        }
        finally
        {
            discoveryLock.Release();
        }
    }

    private async Task<Uri> RequestDiscoveryDocumentAsync(
        Uri metadataAddress,
        bool requireHttps,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(
            metadataAddress,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"O servidor de autorização recusou o Discovery com status " +
                $"{(int)response.StatusCode}.",
                inner: null,
                response.StatusCode);
        }

        var document = await response.Content.ReadFromJsonAsync<DiscoveryDocument>(
            cancellationToken);
        if (!Uri.TryCreate(
                document?.TokenEndpoint,
                UriKind.Absolute,
                out var tokenEndpoint) ||
            requireHttps && tokenEndpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "O documento de Discovery retornou um token_endpoint inválido.");
        }

        return tokenEndpoint;
    }

    private async Task<CachedToken> RequestTokenAsync(
        ServiceTokenOptions options,
        Uri tokenEndpoint,
        string audience,
        string scopes,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            tokenEndpoint)
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
                $"O servidor de autorização recusou a emissão do token de " +
                "serviço com status " +
                $"{(int)response.StatusCode}.",
                inner: null,
                response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(
            cancellationToken);

        if (string.IsNullOrWhiteSpace(payload?.AccessToken) ||
            payload.ExpiresIn <= 0 ||
            !string.Equals(
                payload.TokenType,
                "Bearer",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "O servidor de autorização retornou uma resposta de token " +
                "inválida.");
        }

        var usableLifetime = TimeSpan.FromSeconds(payload.ExpiresIn) -
            options.RefreshBeforeExpiration;
        if (usableLifetime <= TimeSpan.Zero)
        {
            usableLifetime = TimeSpan.FromSeconds(1);
        }

        return new CachedToken(
            payload.AccessToken,
            timeProvider.GetUtcNow().Add(usableLifetime));
    }

    private static Uri ResolveMetadataAddress(ServiceTokenOptions options)
    {
        if (options.MetadataAddress is not null)
        {
            return options.MetadataAddress;
        }

        var authority = options.Authority ??
            throw new InvalidOperationException(
                "Configure Authority, MetadataAddress ou TokenEndpoint.");
        return new Uri(
            $"{authority.AbsoluteUri.TrimEnd('/')}/.well-known/openid-configuration",
            UriKind.Absolute);
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

    private sealed record CachedTokenEndpoint(
        Uri MetadataAddress,
        Uri TokenEndpoint,
        DateTimeOffset ValidUntilUtc);

    private sealed class DiscoveryDocument
    {
        [JsonPropertyName("token_endpoint")]
        public string? TokenEndpoint { get; init; }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }
    }
}
