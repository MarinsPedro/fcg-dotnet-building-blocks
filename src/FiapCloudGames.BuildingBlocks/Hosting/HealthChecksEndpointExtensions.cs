using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;

namespace FiapCloudGames.BuildingBlocks.Hosting;

/// <summary>
/// Mapeia os endpoints de saúde padronizados.
/// </summary>
public static class HealthChecksEndpointExtensions
{
    /// <summary>Mapeia o health check no caminho informado.</summary>
    public static IEndpointConventionBuilder MapFiapHealthChecks(
        this IEndpointRouteBuilder endpoints,
        string path = "/health")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "O caminho do health check deve começar com '/'.",
                nameof(path));
        }

        return endpoints.MapHealthChecks(
            path,
            new HealthCheckOptions
            {
                AllowCachingResponses = false
            });
    }
}
