using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FiapCloudGames.BuildingBlocks.Hosting;

/// <summary>
/// Mapeia os endpoints de saúde padronizados.
/// </summary>
public static class HealthChecksEndpointExtensions
{
    /// <summary>
    /// Mapeia os endpoints padrão de saúde, liveness e readiness.
    /// </summary>
    public static IEndpointRouteBuilder MapFiapHealthEndpoints(
        this IEndpointRouteBuilder endpoints,
        string path = "/health")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ValidatePath(path);

        var normalizedPath = path.TrimEnd('/');
        endpoints.MapHealthChecks(
                normalizedPath,
                CreateOptions())
            .AllowAnonymous();
        endpoints.MapHealthChecks(
                $"{normalizedPath}/live",
                CreateOptions(_ => false))
            .AllowAnonymous();
        endpoints.MapHealthChecks(
                $"{normalizedPath}/ready",
                CreateOptions(registration =>
                    registration.Tags.Contains("ready")))
            .AllowAnonymous();

        return endpoints;
    }

    /// <summary>Mapeia o health check no caminho informado.</summary>
    public static IEndpointConventionBuilder MapFiapHealthChecks(
        this IEndpointRouteBuilder endpoints,
        string path = "/health")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ValidatePath(path);

        return endpoints.MapHealthChecks(
            path,
            CreateOptions());
    }

    private static HealthCheckOptions CreateOptions(
        Func<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration, bool>?
            predicate = null) =>
        new()
        {
            Predicate = predicate,
            AllowCachingResponses = false
        };

    private static void ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!path.StartsWith("/", StringComparison.Ordinal) || path == "/")
        {
            throw new ArgumentException(
                "O caminho base deve começar com '/' e não pode ser a raiz.",
                nameof(path));
        }
    }
}
