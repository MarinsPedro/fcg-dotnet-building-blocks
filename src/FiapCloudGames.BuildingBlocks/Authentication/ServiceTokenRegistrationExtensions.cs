using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
                options => options.TokenEndpoint is { IsAbsoluteUri: true },
                $"Configure '{sectionName}:TokenEndpoint' com uma URI absoluta.")
            .Validate(
                options =>
                    !options.RequireHttps ||
                    options.TokenEndpoint?.Scheme == Uri.UriSchemeHttps,
                $"'{sectionName}:TokenEndpoint' deve usar HTTPS.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ClientId),
                $"Configure '{sectionName}:ClientId'.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ClientSecret),
                $"Configure '{sectionName}:ClientSecret'.")
            .Validate(
                options => options.RefreshBeforeExpiration >= TimeSpan.Zero,
                $"'{sectionName}:RefreshBeforeExpiration' não pode ser negativo.")
            .ValidateOnStart();

        services.AddHttpClient(ServiceTokenProvider.HttpClientName);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IServiceTokenProvider, ServiceTokenProvider>();

        return services;
    }
}
