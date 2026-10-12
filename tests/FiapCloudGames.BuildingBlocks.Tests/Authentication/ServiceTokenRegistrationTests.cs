using FiapCloudGames.BuildingBlocks.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
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

    [Fact]
    public void AddFiapServiceTokenProvider_ConfiguresTimeoutAndDisablesRedirects()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["ServiceAuthentication:TokenEndpoint"] =
                    "https://identity.internal/connect/token",
                ["ServiceAuthentication:ClientId"] = "orders-api",
                ["ServiceAuthentication:ClientSecret"] = "test-secret",
                ["ServiceAuthentication:HttpTimeout"] = "00:00:07"
            });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFiapServiceTokenProvider(configuration);

        using var provider = services.BuildServiceProvider();
        var client = provider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(ServiceTokenProvider.HttpClientName);
        var handler = provider
            .GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler(ServiceTokenProvider.HttpClientName);

        Assert.Equal(TimeSpan.FromSeconds(7), client.Timeout);
        Assert.False(GetPrimaryHandler(handler).AllowAutoRedirect);
    }

    [Fact]
    public void AddFiapServiceTokenProvider_WithInvalidAllowedHost_RejectsConfiguration()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["ServiceAuthentication:Authority"] =
                    "https://identity.internal",
                ["ServiceAuthentication:ClientId"] = "orders-api",
                ["ServiceAuthentication:ClientSecret"] = "test-secret",
                ["ServiceAuthentication:AllowedTokenEndpointHosts:0"] =
                    "https://not-a-host.example"
            });
        var services = new ServiceCollection();
        services.AddFiapServiceTokenProvider(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ServiceTokenOptions>>().Value);
    }

    private static HttpClientHandler GetPrimaryHandler(HttpMessageHandler handler)
    {
        while (handler is DelegatingHandler delegatingHandler)
        {
            handler = delegatingHandler.InnerHandler
                ?? throw new InvalidOperationException(
                    "A cadeia do HttpClient contém um handler sem InnerHandler.");
        }

        return Assert.IsType<HttpClientHandler>(handler);
    }

    private static IConfiguration CreateConfiguration(
        IReadOnlyDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
