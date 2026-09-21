using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FiapCloudGames.BuildingBlocks.Web;

internal sealed class ClientErrorLoggingMiddleware(
    RequestDelegate next,
    ILogger<ClientErrorLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        await next(context);

        var status = context.Response.StatusCode;
        if (status is < StatusCodes.Status400BadRequest or
            >= StatusCodes.Status500InternalServerError)
        {
            return;
        }

        var problemDetails = ApiProblemDetailsFactory.GetCurrent(context);
        var type = problemDetails?.Type ??
            ApiProblemDescriptors.FromStatusCode(status).Type;
        var durationMs = Math.Round(
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            2);
        var fields = problemDetails?.Errors?
            .Select(error => error.Field)
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        var validationErrorCount = problemDetails?.Errors?.Count ?? 0;
        var level = status is StatusCodes.Status403Forbidden or
            StatusCodes.Status429TooManyRequests
                ? LogLevel.Warning
                : LogLevel.Information;

        logger.Log(
            level,
            "Requisição rejeitada com {Type} ({Status}) em {Method} {Path}. " +
            "Campos: {ValidationFields}. Erros: {ValidationErrorCount}. " +
            "Duração: {DurationMs} ms.",
            type,
            status,
            context.Request.Method,
            context.Request.Path,
            fields.Length > 0 ? string.Join(',', fields) : "(sem campo)",
            validationErrorCount,
            durationMs);
    }
}
