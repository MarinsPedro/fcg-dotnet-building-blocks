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
        var clock = CreateClock();
        var provider = CreateProvider(handler, clock);

        var first = await provider.GetAccessTokenAsync(
            "payments-api",
            "payments.read payments.process");
        var second = await provider.GetAccessTokenAsync(
            "payments-api",
            "payments.process payments.read");

        Assert.Equal("technical-token-1", first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.TokenRequestCount);
        Assert.Equal(
            "https://identity.internal/oauth2/token",
            handler.LastTokenEndpoint?.AbsoluteUri);
        Assert.Contains("audience=payments-api", handler.LastForm);
        Assert.Contains(
            "scope=payments.process+payments.read",
            handler.LastForm);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenNearExpiration_RenewsToken()
    {
        var handler = new AuthorizationServerHandler();
        var clock = CreateClock();
        var provider = CreateProvider(handler, clock);

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
    public async Task GetAccessTokenAsync_WithAuthority_DiscoversAndCachesTokenEndpoint()
    {
        var handler = new AuthorizationServerHandler();
        var clock = CreateClock();
        var provider = CreateProvider(handler, clock, useDiscovery: true);

        await provider.GetAccessTokenAsync(
            "catalog-api",
            "catalog.games.read");
        await provider.GetAccessTokenAsync(
            "payments-api",
            "payments.process");

        Assert.Equal(1, handler.DiscoveryRequestCount);
        Assert.Equal(2, handler.TokenRequestCount);
        Assert.Equal(
            "https://identity.internal/.well-known/openid-configuration",
            handler.LastDiscoveryEndpoint?.AbsoluteUri);
        Assert.Equal(
            "https://identity.internal/oauth2/token",
            handler.LastTokenEndpoint?.AbsoluteUri);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithInvalidDiscoveryDocument_RejectsResponse()
    {
        var handler = new AuthorizationServerHandler(
            discoveryTokenEndpoint: "not-an-absolute-uri");
        var provider = CreateProvider(
            handler,
            CreateClock(),
            useDiscovery: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetAccessTokenAsync(
                    "catalog-api",
                    "catalog.games.read")
                .AsTask());

        Assert.Equal(
            "O documento de Discovery retornou um token_endpoint inválido.",
            exception.Message);
        Assert.Equal(1, handler.DiscoveryRequestCount);
        Assert.Equal(0, handler.TokenRequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithNonOAuthResponse_RejectsResponse()
    {
        var handler = new AuthorizationServerHandler(useCamelCaseToken: true);
        var provider = CreateProvider(handler, CreateClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetAccessTokenAsync(
                    "catalog-api",
                    "catalog.games.read")
                .AsTask());
    }

    private static MutableTimeProvider CreateClock() =>
        new(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));

    private static ServiceTokenProvider CreateProvider(
        HttpMessageHandler handler,
        TimeProvider clock,
        bool useDiscovery = false)
    {
        var options = new ServiceTokenOptions
        {
            Authority = useDiscovery
                ? new Uri("https://identity.internal")
                : null,
            TokenEndpoint = useDiscovery
                ? null
                : new Uri("https://identity.internal/oauth2/token"),
            ClientId = "orders-api",
            ClientSecret = "test-secret",
            RefreshBeforeExpiration = TimeSpan.FromSeconds(30)
        };

        return new ServiceTokenProvider(
            new StubHttpClientFactory(handler),
            new StubOptionsMonitor<ServiceTokenOptions>(options),
            clock);
    }

    private sealed class AuthorizationServerHandler(
        string discoveryTokenEndpoint =
            "https://identity.internal/oauth2/token",
        bool useCamelCaseToken = false) : HttpMessageHandler
    {
        public int DiscoveryRequestCount { get; private set; }
        public int TokenRequestCount { get; private set; }
        public string LastForm { get; private set; } = string.Empty;
        public Uri? LastDiscoveryEndpoint { get; private set; }
        public Uri? LastTokenEndpoint { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                DiscoveryRequestCount++;
                LastDiscoveryEndpoint = request.RequestUri;
                return JsonResponse(
                    $$"""
                    {
                      "token_endpoint": "{{discoveryTokenEndpoint}}"
                    }
                    """);
            }

            TokenRequestCount++;
            LastTokenEndpoint = request.RequestUri;
            LastForm = await request.Content!.ReadAsStringAsync(
                cancellationToken);

            return JsonResponse(
                useCamelCaseToken
                    ? $$"""
                      {
                        "accessToken": "technical-token-{{TokenRequestCount}}",
                        "expiresIn": 120,
                        "tokenType": "Bearer"
                      }
                      """
                    : $$"""
                      {
                        "access_token": "technical-token-{{TokenRequestCount}}",
                        "expires_in": 120,
                        "token_type": "Bearer",
                        "scope": "payments.process"
                      }
                      """);
        }

        private static HttpResponseMessage JsonResponse(string json) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
            };
    }

    private sealed class StubHttpClientFactory(
        HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false);
    }

    private sealed class StubOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(
            Action<T, string?> listener) => null;
    }

    private sealed class MutableTimeProvider(
        DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset current = utcNow;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan amount) => current += amount;
    }
}
