using System.Diagnostics;
using FiapCloudGames.BuildingBlocks.Observability;
using Microsoft.AspNetCore.Http;

namespace FiapCloudGames.BuildingBlocks.Web;

internal static class ApiProblemDetailsFactory
{
    private static readonly object CurrentProblemDetailsKey = new();

    public static ApiProblemDetails CreateValidation(
        HttpContext context,
        IReadOnlyCollection<ApiError> errors) =>
        Create(
            context,
            ApiProblemDescriptors.Validation,
            errors: errors);

    public static ApiProblemDetails Create(
        HttpContext context,
        ApiProblemDescriptor descriptor,
        string? detail = null,
        IReadOnlyCollection<ApiError>? errors = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(descriptor);

        var resolvedDetail = detail ?? descriptor.DefaultDetail;
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedDetail);

        if (errors is { Count: 0 })
        {
            throw new ArgumentException(
                "Quando informada, a coleção deve possuir pelo menos um erro.",
                nameof(errors));
        }

        if (errors?.Any(error => string.IsNullOrWhiteSpace(error.Message)) ==
            true)
        {
            throw new ArgumentException(
                "Todos os erros devem possuir uma mensagem.",
                nameof(errors));
        }

        var problemDetails = new ApiProblemDetails
        {
            Type = descriptor.Type,
            Title = descriptor.Title,
            Status = descriptor.Status,
            Detail = resolvedDetail,
            TraceId = GetTraceId(context),
            CorrelationId = GetCorrelationId(context),
            Errors = errors?.ToArray()
        };

        context.Items[CurrentProblemDetailsKey] = problemDetails;
        return problemDetails;
    }

    public static ApiProblemDetails CreateStatusCode(
        HttpContext context,
        int status) =>
        Create(context, ApiProblemDescriptors.FromStatusCode(status));

    public static ApiProblemDetails? GetCurrent(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items.TryGetValue(
            CurrentProblemDetailsKey,
            out var problemDetails)
                ? problemDetails as ApiProblemDetails
                : null;
    }

    private static string GetTraceId(HttpContext context)
        => Activity.Current?.TraceId.ToString() ??
            context.TraceIdentifier;

    private static string? GetCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue(
                CorrelationIdConstants.ItemName,
                out var correlationId) &&
            correlationId is string value)
        {
            return value;
        }

        return null;
    }
}
