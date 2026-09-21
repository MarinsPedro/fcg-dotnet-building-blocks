using FiapCloudGames.BuildingBlocks.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FiapCloudGames.BuildingBlocks.Tests.Authentication;

public sealed class ServiceAuthenticationRegistrationTests
{
    [Fact]
    public void Registration_ShouldConfigureStrictJwtValidation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Authentication:Authority"] = "https://identity.internal",
                    ["Authentication:Issuer"] = "fiap-cloud-games-identity",
                    ["Authentication:Audience"] = "orders-api",
                    ["Authentication:RequireHttpsMetadata"] = "true",
                    ["Authentication:ClockSkew"] = "00:00:20"
                })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddFiapServiceAuthentication(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal("https://identity.internal", options.Authority);
        Assert.Equal("orders-api", options.Audience);
        Assert.False(options.MapInboundClaims);
        Assert.True(options.TokenValidationParameters.ValidateIssuer);
        Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
        Assert.True(options.TokenValidationParameters.ValidateLifetime);
        Assert.Equal(
            "fiap-cloud-games-identity",
            options.TokenValidationParameters.ValidIssuer);
        Assert.Equal(
            TimeSpan.FromSeconds(20),
            options.TokenValidationParameters.ClockSkew);
    }
}
