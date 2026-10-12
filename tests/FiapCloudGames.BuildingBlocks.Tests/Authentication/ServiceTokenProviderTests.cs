using System.Net;
using System.Text;
using FiapCloudGames.BuildingBlocks.Authentication;
using Microsoft.Extensions.Options;

namespace FiapCloudGames.BuildingBlocks.Tests.Authentication;

public sealed class ServiceTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_WithEquivalentScopes_ReusesCachedToken()
    {
        var handler = new AuthorizationServerHandler();
        var provider = CreateProvider(handler);

        var first = await provider.GetAccessTokenAsync(
            "payments-api",
            "payments.read payments.process");
        var second = await provider.GetAccessTokenAsync(
            "payments-api",
            "payments.process payments.read");

        Assert.Equal("technical-token-1", first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.TokenRequestCount);
        Assert.Contains("audience=payments-api", handler.LastForm);
        Assert.Contains("scope=payments.process+payments.read", handler.LastForm);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenNearExpiration_RenewsToken()
    {
        var handler = new AuthorizationServerHandler();
        var clock = CreateClock();
        var provider = CreateProvider(handler, clock: clock);

        var first = await provider.GetAccessTokenAsync(
            "orders-api",
            "orders.write");
        clock.Advance(TimeSpan.FromSeconds(91));
        var second = await provider.GetAccessTokenAsync(
            "orders-api",
            "orders.write");

        Assert.Equal("technical-token-1", first);
        Assert.Equal("technical-token-2", second);
        Assert.Equal(2, handler.TokenRequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithAuthority_DiscoversAndCachesEndpoint()
    {
        var handler = new AuthorizationServerHandler();
        var provider = CreateProvider(handler, useDiscovery: true);

        await provider.GetAccessTokenAsync("catalog-api", "catalog.read");
        await provider.GetAccessTokenAsync("payments-api", "payments.process");

        Assert.Equal(1, handler.DiscoveryRequestCount);
        Assert.Equal(2, handler.TokenRequestCount);
        Assert.Equal(
            "https://identity.internal/.well-known/openid-configuration",
            handler.LastDiscoveryEndpoint?.AbsoluteUri);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithUntrustedDiscoveryHost_RejectsEndpoint()
    {
        var handler = new AuthorizationServerHandler(
            discoveryTokenEndpoint: "https://attacker.example/token");
        var provider = CreateProvider(handler, useDiscovery: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetAccessTokenAsync("catalog-api", "catalog.read").AsTask());

        Assert.Contains("não confiável", exception.Message);
        Assert.Equal(0, handler.TokenRequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithAllowedDiscoveryHost_AcceptsEndpoint()
    {
        var handler = new AuthorizationServerHandler(
            discoveryTokenEndpoint: "https://tokens.internal/oauth2/token");
        var options = CreateOptions(useDiscovery: true);
        options.AllowedTokenEndpointHosts = ["tokens.internal"];
        var provider = CreateProvider(handler, options: options);

        var token = await provider.GetAccessTokenAsync(
            "catalog-api",
            "catalog.read");

        Assert.Equal("technical-token-1", token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenConfigurationChanges_DiscardsCachedToken()
    {
        var handler = new AuthorizationServerHandler();
        var monitor = new MutableOptionsMonitor<ServiceTokenOptions>(
            CreateOptions());
        var provider = CreateProvider(handler, optionsMonitor: monitor);

        var first = await provider.GetAccessTokenAsync(
            "catalog-api",
            "catalog.read");
        var changed = CreateOptions();
        changed.ClientId = "catalog-api-v2";
        changed.ClientSecret = "rotated-secret";
        monitor.Set(changed);
        var second = await provider.GetAccessTokenAsync(
            "catalog-api",
            "catalog.read");

        Assert.Equal("technical-token-1", first);
        Assert.Equal("technical-token-2", second);
        Assert.Contains("client_id=catalog-api-v2", handler.LastForm);
        Assert.Equal(2, handler.TokenRequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithUnsafeLifetime_DoesNotCacheToken()
    {
        var handler = new AuthorizationServerHandler(expiresIn: 20);
        var provider = CreateProvider(handler);

        var first = await provider.GetAccessTokenAsync(
            "catalog-api",
            "catalog.read");
        var second = await provider.GetAccessTokenAsync(
            "catalog-api",
            "catalog.read");

        Assert.NotEqual(first, second);
        Assert.Equal(2, handler.TokenRequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithTransientFailure_RetriesRequest()
    {
        var handler = new AuthorizationServerHandler(transientTokenFailures: 1);
        var options = CreateOptions();
        options.RetryCount = 2;
        options.RetryBaseDelay = TimeSpan.Zero;
        var provider = CreateProvider(handler, options: options);

        var token = await provider.GetAccessTokenAsync(
            "catalog-api",
            "catalog.read");

        Assert.Equal("technical-token-2", token);
        Assert.Equal(2, handler.TokenRequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithClientError_DoesNotRetryRequest()
    {
        var handler = new AuthorizationServerHandler(
            tokenFailureStatus: HttpStatusCode.Unauthorized);
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.GetAccessTokenAsync("catalog-api", "catalog.read").AsTask());

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(1, handler.TokenRequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithConcurrentCalls_IssuesOneToken()
    {
        var handler = new AuthorizationServerHandler(responseDelay: TimeSpan.FromMilliseconds(20));
        var provider = CreateProvider(handler);

        var tokens = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ =>
                provider.GetAccessTokenAsync(
                    "catalog-api",
                    "catalog.read").AsTask()));

        Assert.All(tokens, token => Assert.Equal("technical-token-1", token));
        Assert.Equal(1, handler.TokenRequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenCacheLimitIsReached_EvictsOldToken()
    {
        var handler = new AuthorizationServerHandler();
        var options = CreateOptions();
        options.MaxCachedTokens = 1;
        var provider = CreateProvider(handler, options: options);

        await provider.GetAccessTokenAsync("catalog-api", "catalog.read");
        await provider.GetAccessTokenAsync("payments-api", "payments.read");
        await provider.GetAccessTokenAsync("catalog-api", "catalog.read");

        Assert.Equal(3, handler.TokenRequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithNonOAuthResponse_RejectsResponse()
    {
        var handler = new AuthorizationServerHandler(useCamelCaseToken: true);
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetAccessTokenAsync("catalog-api", "catalog.read").AsTask());
    }

    private static MutableTimeProvider CreateClock() =>
        new(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));

    private static ServiceTokenOptions CreateOptions(bool useDiscovery = false) =>
        new()
        {
            Authority = useDiscovery
                ? new Uri("https://identity.internal")
                : null,
            TokenEndpoint = useDiscovery
                ? null
                : new Uri("https://identity.internal/oauth2/token"),
            ClientId = "orders-api",
            ClientSecret = "test-secret",
            RefreshBeforeExpiration = TimeSpan.FromSeconds(30),
            RetryCount = 0
        };

    private static ServiceTokenProvider CreateProvider(
        HttpMessageHandler handler,
        TimeProvider? clock = null,
        bool useDiscovery = false,
        ServiceTokenOptions? options = null,
        MutableOptionsMonitor<ServiceTokenOptions>? optionsMonitor = null)
    {
        var monitor = optionsMonitor ??
            new MutableOptionsMonitor<ServiceTokenOptions>(
                options ?? CreateOptions(useDiscovery));

        return new ServiceTokenProvider(
            new StubHttpClientFactory(handler),
            monitor,
            clock ?? CreateClock());
    }

    private sealed class AuthorizationServerHandler(
        string discoveryTokenEndpoint =
            "https://identity.internal/oauth2/token",
        bool useCamelCaseToken = false,
        int expiresIn = 120,
        int transientTokenFailures = 0,
        HttpStatusCode? tokenFailureStatus = null,
        TimeSpan? responseDelay = null) : HttpMessageHandler
    {
        private int discoveryRequestCount;
        private int tokenRequestCount;

        public int DiscoveryRequestCount => discoveryRequestCount;

        public int TokenRequestCount => tokenRequestCount;

        public string LastForm { get; private set; } = string.Empty;

        public Uri? LastDiscoveryEndpoint { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (responseDelay is { } delay)
            {
                await Task.Delay(delay, cancellationToken);
            }

            if (request.Method == HttpMethod.Get)
            {
                Interlocked.Increment(ref discoveryRequestCount);
                LastDiscoveryEndpoint = request.RequestUri;
                return JsonResponse(
                    $$"""
                    {
                      "token_endpoint": "{{discoveryTokenEndpoint}}"
                    }
                    """);
            }

            var requestNumber = Interlocked.Increment(ref tokenRequestCount);
            LastForm = await request.Content!.ReadAsStringAsync(cancellationToken);

            if (tokenFailureStatus is { } status)
            {
                return new HttpResponseMessage(status);
            }

            if (requestNumber <= transientTokenFailures)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return JsonResponse(
                useCamelCaseToken
                    ? $$"""
                      {
                        "accessToken": "technical-token-{{requestNumber}}",
                        "expiresIn": {{expiresIn}},
                        "tokenType": "Bearer"
                      }
                      """
                    : $$"""
                      {
                        "access_token": "technical-token-{{requestNumber}}",
                        "expires_in": {{expiresIn}},
                        "token_type": "Bearer"
                      }
                      """);
        }

        private static HttpResponseMessage JsonResponse(string json) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
    }

    private sealed class StubHttpClientFactory(
        HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false);
    }

    private sealed class MutableOptionsMonitor<T>(T initial) : IOptionsMonitor<T>
    {
        private Action<T, string?>? listener;
        private T current = initial;

        public T CurrentValue => current;

        public T Get(string? name) => current;

        public IDisposable OnChange(Action<T, string?> changeListener)
        {
            listener += changeListener;
            return new CallbackDisposable(() => listener -= changeListener);
        }

        public void Set(T value)
        {
            current = value;
            listener?.Invoke(value, Options.DefaultName);
        }
    }

    private sealed class CallbackDisposable(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    private sealed class MutableTimeProvider(
        DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset current = utcNow;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan amount) => current += amount;
    }
}
