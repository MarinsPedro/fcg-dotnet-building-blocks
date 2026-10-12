using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FiapCloudGames.BuildingBlocks.Authentication;

/// <summary>
/// Registra o cliente de tokens <c>service</c>.
/// </summary>
public static class ServiceTokenRegistrationExtensions
{
    /// <summary>
    /// Adiciona o provider Client Credentials com cache em memória.
    /// </summary>
    public static IServiceCollection AddFiapServiceTokenProvider(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = ServiceTokenOptions.DefaultSectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        services.AddOptions<ServiceTokenOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Validate(
                options => HasValidTokenSource(options),
                $"Configure '{sectionName}:Authority', " +
                $"'{sectionName}:MetadataAddress' ou " +
                $"'{sectionName}:TokenEndpoint' com uma URI absoluta.")
            .Validate(
                options => HasSecureTokenSource(options),
                $"A fonte de tokens de '{sectionName}' deve usar HTTPS.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ClientId),
                $"Configure '{sectionName}:ClientId'.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ClientSecret),
                $"Configure '{sectionName}:ClientSecret'.")
            .Validate(
                options => options.RefreshBeforeExpiration >= TimeSpan.Zero,
                $"'{sectionName}:RefreshBeforeExpiration' não pode ser negativo.")
            .Validate(
                options => options.DiscoveryRefreshInterval > TimeSpan.Zero &&
                    options.DiscoveryRefreshInterval <= TimeSpan.FromDays(1),
                $"'{sectionName}:DiscoveryRefreshInterval' deve ser maior que " +
                "zero e menor ou igual a um dia.")
            .Validate(
                options => options.HttpTimeout > TimeSpan.Zero &&
                    options.HttpTimeout <= TimeSpan.FromMinutes(2),
                $"'{sectionName}:HttpTimeout' deve estar entre zero e dois minutos.")
            .Validate(
                options => options.RetryCount is >= 0 and <= 3,
                $"'{sectionName}:RetryCount' deve estar entre zero e três.")
            .Validate(
                options => options.RetryBaseDelay >= TimeSpan.Zero &&
                    options.RetryBaseDelay <= TimeSpan.FromSeconds(5),
                $"'{sectionName}:RetryBaseDelay' deve estar entre zero e cinco segundos.")
            .Validate(
                options => options.MaxCachedTokens is >= 1 and <= 1024,
                $"'{sectionName}:MaxCachedTokens' deve estar entre um e 1024.")
            .Validate(
                options => options.AllowedTokenEndpointHosts is not null &&
                    options.AllowedTokenEndpointHosts.All(IsValidHost),
                $"'{sectionName}:AllowedTokenEndpointHosts' contém um host inválido.")
            .ValidateOnStart();

        services.AddHttpClient(ServiceTokenProvider.HttpClientName)
            .ConfigureHttpClient((provider, client) =>
            {
                client.Timeout = provider
                    .GetRequiredService<IOptionsMonitor<ServiceTokenOptions>>()
                    .CurrentValue
                    .HttpTimeout;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IServiceTokenProvider, ServiceTokenProvider>();

        return services;
    }

    private static bool HasValidTokenSource(ServiceTokenOptions options) =>
        options.TokenEndpoint is { IsAbsoluteUri: true } ||
        options.MetadataAddress is { IsAbsoluteUri: true } ||
        options.Authority is { IsAbsoluteUri: true };

    private static bool HasSecureTokenSource(ServiceTokenOptions options)
    {
        if (!options.RequireHttps)
        {
            return true;
        }

        return GetConfiguredTokenSource(options)?.Scheme == Uri.UriSchemeHttps;
    }

    private static Uri? GetConfiguredTokenSource(ServiceTokenOptions options) =>
        options.TokenEndpoint ?? options.MetadataAddress ?? options.Authority;

    private static bool IsValidHost(string host) =>
        !string.IsNullOrWhiteSpace(host) &&
        Uri.CheckHostName(host) != UriHostNameType.Unknown;
}
