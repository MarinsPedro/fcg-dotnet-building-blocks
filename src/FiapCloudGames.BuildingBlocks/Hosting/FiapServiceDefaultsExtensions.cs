using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FiapCloudGames.BuildingBlocks.Hosting;

/// <summary>
/// Configura os serviços técnicos básicos de uma aplicação.
/// </summary>
public static class FiapServiceDefaultsExtensions
{
    /// <summary>
    /// Adiciona health checks, HttpClient, contexto HTTP, relógio e correlação
    /// de trace nos logs.
    /// </summary>
    public static IHostApplicationBuilder AddFiapServiceDefaults(
        this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Logging.Configure(options =>
            options.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId |
                ActivityTrackingOptions.SpanId |
                ActivityTrackingOptions.ParentId);

        builder.Services.AddHealthChecks();
        builder.Services.AddHttpClient();
        builder.Services.AddHttpContextAccessor();
        builder.Services.TryAddSingleton(TimeProvider.System);

        return builder;
    }
}
