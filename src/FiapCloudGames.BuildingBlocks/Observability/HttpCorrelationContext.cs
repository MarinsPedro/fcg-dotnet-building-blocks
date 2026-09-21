using Microsoft.AspNetCore.Http;

namespace FiapCloudGames.BuildingBlocks.Observability;

internal sealed class HttpCorrelationContext(
    IHttpContextAccessor httpContextAccessor) : ICorrelationContext
{
    public string CorrelationId
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context?.Items.TryGetValue(
                    CorrelationIdConstants.ItemName,
                    out var value) == true &&
                value is string correlationId)
            {
                return correlationId;
            }

            return context?.TraceIdentifier ?? string.Empty;
        }
    }
}
