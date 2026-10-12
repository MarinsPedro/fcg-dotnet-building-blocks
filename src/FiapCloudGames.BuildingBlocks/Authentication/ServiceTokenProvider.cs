using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace FiapCloudGames.BuildingBlocks.Authentication;

internal sealed class ServiceTokenProvider : IServiceTokenProvider, IDisposable
{
    internal const string HttpClientName =
        "FiapCloudGames.ServiceAuthentication";

    private readonly IHttpClientFactory httpClientFactory;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrentDictionary<string, CachedToken> cache = new();
    private readonly Dictionary<string, LockEntry> locks = new(StringComparer.Ordinal);
    private readonly object locksSync = new();
    private readonly object cacheSync = new();
    private readonly SemaphoreSlim discoveryLock = new(1, 1);
    private readonly IDisposable? changeSubscription;
    private ConfigurationSnapshot configuration;
    private long configurationVersion;
    private CachedTokenEndpoint? cachedTokenEndpoint;

    public ServiceTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<ServiceTokenOptions> optionsMonitor,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.httpClientFactory = httpClientFactory;
        this.timeProvider = timeProvider;
        configuration = new ConfigurationSnapshot(
            optionsMonitor.CurrentValue,
            configurationVersion);
        changeSubscription = optionsMonitor.OnChange((options, _) =>
        {
            var version = Interlocked.Increment(ref configurationVersion);
            Volatile.Write(
                ref configuration,
                new ConfigurationSnapshot(options, version));
            cache.Clear();
            Volatile.Write(ref cachedTokenEndpoint, null);
        });
    }

    public async ValueTask<string> GetAccessTokenAsync(
        string audience,
        string scopes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopes);

        var normalizedAudience = audience.Trim();
        var normalizedScopes = NormalizeScopes(scopes);

        while (true)
        {
            var snapshot = Volatile.Read(ref configuration);
            var cacheKey = CreateCacheKey(
                snapshot.Version,
                normalizedAudience,
                normalizedScopes);

            if (TryGetValidToken(cacheKey, snapshot.Version, out var token))
            {
                return token;
            }

            var gate = RegisterLock(cacheKey);
            var entered = false;
            try
            {
                await gate.Semaphore.WaitAsync(cancellationToken);
                entered = true;

                if (snapshot.Version !=
                    Volatile.Read(ref configuration).Version)
                {
                    continue;
                }

                if (TryGetValidToken(
                        cacheKey,
                        snapshot.Version,
                        out token))
                {
                    return token;
                }

                var tokenEndpoint = await ResolveTokenEndpointAsync(
                    snapshot,
                    cancellationToken);
                var issued = await RequestTokenAsync(
                    snapshot.Options,
                    tokenEndpoint,
                    normalizedAudience,
                    normalizedScopes,
                    cancellationToken);

                if (snapshot.Version !=
                    Volatile.Read(ref configuration).Version)
                {
                    continue;
                }

                if (issued.ValidUntilUtc is { } validUntilUtc)
                {
                    AddToCache(
                        cacheKey,
                        new CachedToken(
                            issued.AccessToken,
                            validUntilUtc,
                            snapshot.Version),
                        snapshot.Options.MaxCachedTokens);
                }

                return issued.AccessToken;
            }
            finally
            {
                if (entered)
                {
                    gate.Semaphore.Release();
                }

                UnregisterLock(cacheKey, gate);
            }
        }
    }

    public void Dispose()
    {
        changeSubscription?.Dispose();
        discoveryLock.Dispose();

        lock (locksSync)
        {
            foreach (var entry in locks.Values)
            {
                entry.Semaphore.Dispose();
            }

            locks.Clear();
        }
    }

    private bool TryGetValidToken(
        string key,
        long version,
        out string token)
    {
        if (cache.TryGetValue(key, out var cached) &&
            cached.ConfigurationVersion == version &&
            cached.ValidUntilUtc > timeProvider.GetUtcNow())
        {
            token = cached.AccessToken;
            return true;
        }

        cache.TryRemove(key, out _);
        token = string.Empty;
        return false;
    }

    private void AddToCache(
        string key,
        CachedToken token,
        int maximumEntries)
    {
        lock (cacheSync)
        {
            var now = timeProvider.GetUtcNow();
            var currentVersion = Volatile.Read(ref configuration).Version;

            foreach (var item in cache)
            {
                if (item.Value.ConfigurationVersion != currentVersion ||
                    item.Value.ValidUntilUtc <= now)
                {
                    cache.TryRemove(item.Key, out _);
                }
            }

            while (cache.Count >= maximumEntries)
            {
                var oldest = cache.MinBy(item => item.Value.ValidUntilUtc);
                if (oldest.Key is null)
                {
                    break;
                }

                cache.TryRemove(oldest.Key, out _);
            }

            cache[key] = token;
        }
    }

    private async Task<Uri> ResolveTokenEndpointAsync(
        ConfigurationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var options = snapshot.Options;
        if (options.TokenEndpoint is not null)
        {
            if (options.RequireHttps &&
                options.TokenEndpoint.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    "O endpoint de token configurado deve usar HTTPS.");
            }

            return options.TokenEndpoint;
        }

        var metadataAddress = ResolveMetadataAddress(options);
        var cached = Volatile.Read(ref cachedTokenEndpoint);
        if (IsValidCachedEndpoint(cached, snapshot, metadataAddress))
        {
            return cached!.TokenEndpoint;
        }

        await discoveryLock.WaitAsync(cancellationToken);
        try
        {
            cached = Volatile.Read(ref cachedTokenEndpoint);
            if (IsValidCachedEndpoint(cached, snapshot, metadataAddress))
            {
                return cached!.TokenEndpoint;
            }

            var discovered = await RequestDiscoveryDocumentAsync(
                metadataAddress,
                options,
                cancellationToken);
            Volatile.Write(
                ref cachedTokenEndpoint,
                new CachedTokenEndpoint(
                    metadataAddress,
                    discovered,
                    timeProvider.GetUtcNow().Add(
                        options.DiscoveryRefreshInterval),
                    snapshot.Version));
            return discovered;
        }
        finally
        {
            discoveryLock.Release();
        }
    }

    private bool IsValidCachedEndpoint(
        CachedTokenEndpoint? cached,
        ConfigurationSnapshot snapshot,
        Uri metadataAddress) =>
        cached is not null &&
        cached.ConfigurationVersion == snapshot.Version &&
        cached.MetadataAddress == metadataAddress &&
        cached.ValidUntilUtc > timeProvider.GetUtcNow();

    private async Task<Uri> RequestDiscoveryDocumentAsync(
        Uri metadataAddress,
        ServiceTokenOptions options,
        CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            options,
            () => new HttpRequestMessage(HttpMethod.Get, metadataAddress),
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
            options.RequireHttps &&
            tokenEndpoint.Scheme != Uri.UriSchemeHttps ||
            !IsTrustedDiscoveredEndpoint(
                metadataAddress,
                tokenEndpoint,
                options))
        {
            throw new InvalidOperationException(
                "O documento de Discovery retornou um token_endpoint inválido " +
                "ou não confiável.");
        }

        return tokenEndpoint;
    }

    private async Task<IssuedToken> RequestTokenAsync(
        ServiceTokenOptions options,
        Uri tokenEndpoint,
        string audience,
        string scopes,
        CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            options,
            () => CreateTokenRequest(
                options,
                tokenEndpoint,
                audience,
                scopes),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"O servidor de autorização recusou a emissão do token de " +
                $"serviço com status {(int)response.StatusCode}.",
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

        var now = timeProvider.GetUtcNow();
        var usableUntil = now
            .AddSeconds(payload.ExpiresIn)
            .Subtract(options.RefreshBeforeExpiration);

        return new IssuedToken(
            payload.AccessToken,
            usableUntil > now ? usableUntil : null);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        ServiceTokenOptions options,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        for (var attempt = 0; ; attempt++)
        {
            using var request = requestFactory();
            try
            {
                var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (attempt >= options.RetryCount ||
                    !IsTransient(response.StatusCode))
                {
                    return response;
                }

                response.Dispose();
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested &&
                    attempt < options.RetryCount)
            {
                // Timeout do HttpClient: uma nova tentativa ainda é segura.
            }
            catch (HttpRequestException exception)
                when (attempt < options.RetryCount &&
                    IsTransient(exception.StatusCode))
            {
                // Falha de transporte ou status transitório.
            }

            await DelayBeforeRetryAsync(
                options.RetryBaseDelay,
                attempt,
                cancellationToken);
        }
    }

    private static HttpRequestMessage CreateTokenRequest(
        ServiceTokenOptions options,
        Uri tokenEndpoint,
        string audience,
        string scopes) =>
        new(HttpMethod.Post, tokenEndpoint)
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

    private static bool IsTransient(HttpStatusCode? statusCode) =>
        statusCode is null or
            HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static async Task DelayBeforeRetryAsync(
        TimeSpan baseDelay,
        int attempt,
        CancellationToken cancellationToken)
    {
        if (baseDelay <= TimeSpan.Zero)
        {
            return;
        }

        var delayMilliseconds = Math.Min(
            baseDelay.TotalMilliseconds * Math.Pow(2, attempt),
            TimeSpan.FromSeconds(30).TotalMilliseconds);
        await Task.Delay(
            TimeSpan.FromMilliseconds(delayMilliseconds),
            cancellationToken);
    }

    private static bool IsTrustedDiscoveredEndpoint(
        Uri metadataAddress,
        Uri tokenEndpoint,
        ServiceTokenOptions options)
    {
        if (!options.RequireSameHostForDiscoveredTokenEndpoint ||
            string.Equals(
                metadataAddress.Host,
                tokenEndpoint.Host,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return options.AllowedTokenEndpointHosts.Contains(
            tokenEndpoint.Host,
            StringComparer.OrdinalIgnoreCase);
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

    private static string CreateCacheKey(
        long version,
        string audience,
        string scopes) =>
        $"{version}::{audience}::{scopes}";

    private LockEntry RegisterLock(string key)
    {
        lock (locksSync)
        {
            if (!locks.TryGetValue(key, out var entry))
            {
                entry = new LockEntry();
                locks.Add(key, entry);
            }

            entry.Users++;
            return entry;
        }
    }

    private void UnregisterLock(string key, LockEntry entry)
    {
        lock (locksSync)
        {
            entry.Users--;
            if (entry.Users == 0 &&
                locks.Remove(key, out var removed))
            {
                removed.Semaphore.Dispose();
            }
        }
    }

    private sealed class LockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int Users { get; set; }
    }

    private sealed record ConfigurationSnapshot(
        ServiceTokenOptions Options,
        long Version);

    private sealed record IssuedToken(
        string AccessToken,
        DateTimeOffset? ValidUntilUtc);

    private sealed record CachedToken(
        string AccessToken,
        DateTimeOffset ValidUntilUtc,
        long ConfigurationVersion);

    private sealed record CachedTokenEndpoint(
        Uri MetadataAddress,
        Uri TokenEndpoint,
        DateTimeOffset ValidUntilUtc,
        long ConfigurationVersion);

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
