using System.Net;
using System.Text;
using FiapCloudGames.BuildingBlocks.Authentication;
using Microsoft.Extensions.Options;

namespace FiapCloudGames.BuildingBlocks.Tests.Authentication;

public sealed class ServiceTokenProviderTests
{
    [Fact]
    public async Task Provider_ShouldCacheTokenByAudienceAndNormalizedScopes()
    {
        var handler = new TokenEndpointHandler();
        var clock = new MutableTimeProvider(
            new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(handler, clock);

        var first = await provider.GetAccessTokenAsync(
            "payments-api",
            "payments.read payments.process");
        var second = await provider.GetAccessTokenAsync(
            "payments-api",
            "payments.process payments.read");

        Assert.Equal("technical-token-1", first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.RequestCount);
        Assert.Contains("audience=payments-api", handler.LastForm);
        Assert.Contains(
            "scope=payments.process+payments.read",
            handler.LastForm);
    }

    [Fact]
    public async Task Provider_ShouldRenewTokenBeforeExpiration()
    {
        var handler = new TokenEndpointHandler();
        var clock = new MutableTimeProvider(
            new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
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
        Assert.Equal(2, handler.RequestCount);
    }

    private static ServiceTokenProvider CreateProvider(
        HttpMessageHandler handler,
        TimeProvider clock)
    {
        var options = new ServiceTokenOptions
        {
            TokenEndpoint = new Uri("https://identity.internal/connect/token"),
            ClientId = "orders-service",
            ClientSecret = "test-secret",
            RefreshBeforeExpiration = TimeSpan.FromSeconds(30)
        };

        return new ServiceTokenProvider(
            new StubHttpClientFactory(handler),
            new StubOptionsMonitor<ServiceTokenOptions>(options),
            clock);
    }

    private sealed class TokenEndpointHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public string LastForm { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastForm = await request.Content!.ReadAsStringAsync(
                cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                    {
                      "access_token": "technical-token-{{RequestCount}}",
                      "expires_in": 120
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }
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
