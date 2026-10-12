using System.Net;
using FiapCloudGames.BuildingBlocks.Observability;

namespace FiapCloudGames.BuildingBlocks.Tests.Observability;

public sealed class CorrelationIdDelegatingHandlerTests
{
    [Fact]
    public async Task Handler_ShouldPropagateCurrentCorrelationId()
    {
        var inner = new RecordingHandler();
        using var handler = new CorrelationIdDelegatingHandler(
            new StubCorrelationContext("orders-123"))
        {
            InnerHandler = inner
        };
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync("https://catalog.internal");

        Assert.Equal(
            "orders-123",
            inner.CorrelationIds.Single());
    }

    [Fact]
    public async Task Handler_ShouldNotReplaceExplicitCorrelationId()
    {
        var inner = new RecordingHandler();
        using var handler = new CorrelationIdDelegatingHandler(
            new StubCorrelationContext("context-value"))
        {
            InnerHandler = inner
        };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://catalog.internal");
        request.Headers.Add(
            CorrelationIdConstants.HeaderName,
            "explicit-value");

        using var response = await client.SendAsync(request);

        Assert.Equal(
            "explicit-value",
            inner.CorrelationIds.Single());
    }

    [Fact]
    public async Task Handler_WithoutCorrelationId_ShouldNotAddHeader()
    {
        var inner = new RecordingHandler();
        using var handler = new CorrelationIdDelegatingHandler(
            new StubCorrelationContext(string.Empty))
        {
            InnerHandler = inner
        };
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync("https://catalog.internal");

        Assert.Empty(inner.CorrelationIds);
    }

    private sealed class StubCorrelationContext(
        string correlationId) : ICorrelationContext
    {
        public string CorrelationId => correlationId;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public IReadOnlyCollection<string> CorrelationIds { get; private set; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CorrelationIds = request.Headers.TryGetValues(
                    CorrelationIdConstants.HeaderName,
                    out var values)
                ? values.ToArray()
                : [];
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
