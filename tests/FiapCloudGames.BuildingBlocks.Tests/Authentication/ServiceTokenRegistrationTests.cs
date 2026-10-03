using FiapCloudGames.BuildingBlocks.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FiapCloudGames.BuildingBlocks.Tests.Authentication;

public sealed class ServiceTokenRegistrationTests
{
    [Fact]
    public void AddFiapServiceTokenProvider_WithAuthority_ConfiguresDiscovery()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["ServiceAuthentication:Authority"] =
                    "https://identity.internal",
                ["ServiceAuthentication:ClientId"] = "orders-api",
                ["ServiceAuthentication:ClientSecret"] = "test-secret"
            });
        var services = new ServiceCollection();

        services.AddFiapServiceTokenProvider(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptions<ServiceTokenOptions>>()
            .Value;
        Assert.Equal(
            "https://identity.internal/",
            options.Authority?.AbsoluteUri);
        Assert.Null(options.TokenEndpoint);
    }

    [Fact]
    public void AddFiapServiceTokenProvider_WithoutTokenSource_RejectsConfiguration()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["ServiceAuthentication:ClientId"] = "orders-api",
                ["ServiceAuthentication:ClientSecret"] = "test-secret"
            });
        var services = new ServiceCollection();
        services.AddFiapServiceTokenProvider(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ServiceTokenOptions>>().Value);
    }

    [Fact]
    public void AddFiapServiceTokenProvider_WithHttpAuthorityInProductionMode_RejectsConfiguration()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["ServiceAuthentication:Authority"] =
                    "http://identity.internal",
                ["ServiceAuthentication:ClientId"] = "orders-api",
                ["ServiceAuthentication:ClientSecret"] = "test-secret",
                ["ServiceAuthentication:RequireHttps"] = "true"
            });
        var services = new ServiceCollection();
        services.AddFiapServiceTokenProvider(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ServiceTokenOptions>>().Value);
    }

    private static IConfiguration CreateConfiguration(
        IReadOnlyDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
