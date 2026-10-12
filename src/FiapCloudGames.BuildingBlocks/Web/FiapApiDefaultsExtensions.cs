using System.Text.Json.Serialization;
using FiapCloudGames.BuildingBlocks.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FiapCloudGames.BuildingBlocks.Web;

/// <summary>
/// Registra e habilita as convenções comuns das APIs.
/// </summary>
public static class FiapApiDefaultsExtensions
{
    /// <summary>Content type usado nas respostas Problem Details.</summary>
    public const string ProblemJsonContentType = "application/problem+json";

    /// <summary>
    /// Adiciona tratamento de erros, validação MVC, JSON e correlação.
    /// </summary>
    public static IServiceCollection AddFiapApiDefaults(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails();
        services.AddExceptionHandler<FiapExceptionHandler>();
        services.PostConfigure<ApiBehaviorOptions>(
            ApiBehaviorOptionsConfigurator.Configure);
        services.Configure<JsonOptions>(options =>
        {
            if (!options.JsonSerializerOptions.Converters
                .OfType<JsonStringEnumConverter>()
                .Any())
            {
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter());
            }
        });
        services.AddFiapCorrelation();

        return services;
    }

    /// <summary>
    /// Habilita correlação, tratamento de exceções, status codes e logs 4xx.
    /// Deve ser chamado antes de autenticação, autorização e endpoints.
    /// </summary>
    public static IApplicationBuilder UseFiapApiDefaults(
        this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseFiapCorrelationId();
        app.UseMiddleware<ClientErrorLoggingMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var context = statusCodeContext.HttpContext;
            var problemDetails = ApiProblemDetailsFactory.CreateStatusCode(
                context,
                context.Response.StatusCode);

            await context.Response.WriteAsJsonAsync(
                problemDetails,
                options: null,
                contentType: ProblemJsonContentType,
                cancellationToken: context.RequestAborted);
        });

        return app;
    }
}
