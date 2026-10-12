using Microsoft.Extensions.DependencyInjection;

namespace FiapCloudGames.BuildingBlocks.Observability;

internal sealed class CorrelationIdDelegatingHandler(
    ICorrelationContext correlationContext) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var correlationId = correlationContext.CorrelationId;
        if (!string.IsNullOrWhiteSpace(correlationId) &&
            !request.Headers.Contains(CorrelationIdConstants.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(
                CorrelationIdConstants.HeaderName,
                correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Extensões para propagar correlação somente em clientes HTTP internos.
/// </summary>
public static class CorrelationIdHttpClientBuilderExtensions
{
    /// <summary>
    /// Propaga o correlation ID atual nas requisições deste cliente HTTP.
    /// Use somente em integrações internas e confiáveis.
    /// </summary>
    public static IHttpClientBuilder AddFiapCorrelationPropagation(
        this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddHttpMessageHandler(provider =>
            new CorrelationIdDelegatingHandler(
                provider.GetRequiredService<ICorrelationContext>()));
    }
}
