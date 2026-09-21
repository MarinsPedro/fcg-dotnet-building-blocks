using FiapCloudGames.BuildingBlocks.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FiapCloudGames.BuildingBlocks.Web;

internal sealed class FiapExceptionHandler(
    ILogger<FiapExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException &&
            httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(
                "Requisição cancelada pelo cliente durante {Method} {Path}.",
                httpContext.Request.Method,
                httpContext.Request.Path);
            return true;
        }

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var resolved = Resolve(exception);

        if (resolved.Descriptor.Status >=
            StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Erro não tratado ao processar {Method} {Path}.",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        httpContext.Response.Clear();
        httpContext.Response.StatusCode = resolved.Descriptor.Status;

        var problemDetails = ApiProblemDetailsFactory.Create(
            httpContext,
            resolved.Descriptor,
            resolved.Detail,
            resolved.Errors);

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: FiapApiDefaultsExtensions.ProblemJsonContentType,
            cancellationToken: cancellationToken);

        return true;
    }

    private static ResolvedError Resolve(Exception exception)
    {
        if (exception is not AppException appException)
        {
            return new ResolvedError(
                ApiProblemDescriptors.InternalServerError,
                ApiProblemDescriptors.InternalServerError.DefaultDetail);
        }

        var descriptor = ApiProblemDescriptors.FromCategory(
            appException.Category);
        var errors = appException.HasErrors
            ? appException.Errors
                .Select(error => new ApiError
                {
                    Message = error.Message,
                    Field = error.Field
                })
                .ToArray()
            : null;
        var detail = appException.HasErrors
            ? "Verifique os dados informados."
            : appException.Message;

        return new ResolvedError(descriptor, detail, errors);
    }

    private sealed record ResolvedError(
        ApiProblemDescriptor Descriptor,
        string Detail,
        IReadOnlyCollection<ApiError>? Errors = null);
}
