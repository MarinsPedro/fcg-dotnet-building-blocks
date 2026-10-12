using FiapCloudGames.BuildingBlocks.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace FiapCloudGames.BuildingBlocks.Tests.Observability;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Middleware_ShouldPropagateValidCorrelationId()
    {
        const string expected = "checkout-123";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdConstants.HeaderName] = expected;
        context.Response.Body = new MemoryStream();
        var middleware = new CorrelationIdMiddleware(
            async httpContext =>
                await httpContext.Response.WriteAsync("ok"),
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(
            expected,
            context.Items[CorrelationIdConstants.ItemName]);
        Assert.Equal(
            expected,
            context.Response.Headers[CorrelationIdConstants.HeaderName]);
    }

    [Fact]
    public async Task Middleware_ShouldReplaceUnsafeCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdConstants.HeaderName] =
            "invalid\r\nheader";
        context.Response.Body = new MemoryStream();
        var middleware = new CorrelationIdMiddleware(
            async httpContext =>
                await httpContext.Response.WriteAsync("ok"),
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        var generated = Assert.IsType<string>(
            context.Items[CorrelationIdConstants.ItemName]);
        Assert.NotEqual("invalid\r\nheader", generated);
        Assert.NotEmpty(generated);
    }

    [Fact]
    public async Task Middleware_ShouldReplaceOversizedCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdConstants.HeaderName] =
            new string('a', 129);
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        var generated = Assert.IsType<string>(
            context.Items[CorrelationIdConstants.ItemName]);
        Assert.NotEqual(new string('a', 129), generated);
    }
}
