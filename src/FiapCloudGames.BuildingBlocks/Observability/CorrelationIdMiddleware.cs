using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FiapCloudGames.BuildingBlocks.Observability;

internal sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    private const int MaximumLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[CorrelationIdConstants.ItemName] = correlationId;
        context.Response.Headers[CorrelationIdConstants.HeaderName] =
            correlationId;

        using (logger.BeginScope(
            new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            }))
        {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var candidate = context.Request.Headers[
            CorrelationIdConstants.HeaderName].FirstOrDefault();

        if (IsValid(candidate))
        {
            return candidate!;
        }

        return Activity.Current?.TraceId.ToString() ??
            Guid.NewGuid().ToString("N");
    }

    private static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaximumLength &&
        value.All(character =>
            char.IsLetterOrDigit(character) ||
            character is '-' or '_' or '.');
}
