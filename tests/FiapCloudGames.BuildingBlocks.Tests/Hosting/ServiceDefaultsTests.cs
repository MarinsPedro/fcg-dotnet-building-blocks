using FiapCloudGames.BuildingBlocks.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace FiapCloudGames.BuildingBlocks.Tests.Hosting;

public sealed class ServiceDefaultsTests
{
    [Fact]
    public void Registration_ShouldProvideTechnicalDefaults()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddFiapServiceDefaults();

        using var host = builder.Build();
        Assert.NotNull(host.Services.GetRequiredService<HealthCheckService>());
        Assert.NotNull(host.Services.GetRequiredService<IHttpClientFactory>());
        Assert.Same(
            TimeProvider.System,
            host.Services.GetRequiredService<TimeProvider>());
    }
}
