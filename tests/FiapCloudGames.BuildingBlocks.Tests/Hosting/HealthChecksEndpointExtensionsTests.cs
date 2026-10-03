using FiapCloudGames.BuildingBlocks.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FiapCloudGames.BuildingBlocks.Tests.Hosting;

public sealed class HealthChecksEndpointExtensionsTests
{
    [Fact]
    public void MapFiapHealthEndpoints_WithDefaultPath_MapsAnonymousHealthRoutes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHealthChecks();
        var app = builder.Build();

        app.MapFiapHealthEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToDictionary(
                endpoint => endpoint.RoutePattern.RawText!,
                StringComparer.Ordinal);
        Assert.Equal(
            ["/health", "/health/live", "/health/ready"],
            endpoints.Keys.Order(StringComparer.Ordinal));
        Assert.All(
            endpoints.Values,
            endpoint => Assert.NotNull(
                endpoint.Metadata.GetMetadata<IAllowAnonymous>()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("health")]
    [InlineData("/")]
    public void MapFiapHealthEndpoints_WithInvalidPath_RejectsConfiguration(
        string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHealthChecks();
        var app = builder.Build();

        Assert.Throws<ArgumentException>(() =>
            app.MapFiapHealthEndpoints(path));
    }
}
