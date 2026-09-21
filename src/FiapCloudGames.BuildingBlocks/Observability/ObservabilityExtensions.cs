using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FiapCloudGames.BuildingBlocks.Observability;

/// <summary>
/// Registra e habilita a correlação de requisições.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>Adiciona o contexto de correlação aos serviços.</summary>
    public static IServiceCollection AddFiapCorrelation(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.TryAddScoped<ICorrelationContext, HttpCorrelationContext>();
        return services;
    }

    /// <summary>Habilita a leitura, geração e propagação do correlation ID.</summary>
    public static IApplicationBuilder UseFiapCorrelationId(
        this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
