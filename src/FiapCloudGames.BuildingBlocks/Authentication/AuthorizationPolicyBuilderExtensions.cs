using Microsoft.AspNetCore.Authorization;

namespace FiapCloudGames.BuildingBlocks.Authentication;

/// <summary>
/// Extensões para policies de autorização técnica.
/// </summary>
public static class AuthorizationPolicyBuilderExtensions
{
    /// <summary>Exige todos os scopes informados.</summary>
    public static AuthorizationPolicyBuilder RequireScopes(
        this AuthorizationPolicyBuilder builder,
        params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(scopes);

        var requiredScopes = scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .ToHashSet(StringComparer.Ordinal);

        if (requiredScopes.Count == 0)
        {
            throw new ArgumentException(
                "Informe pelo menos um scope.",
                nameof(scopes));
        }

        return builder.AddRequirements(
            new ScopeRequirement(requiredScopes));
    }

    /// <summary>Permite somente os clientes técnicos informados.</summary>
    public static AuthorizationPolicyBuilder RequireClients(
        this AuthorizationPolicyBuilder builder,
        params string[] clientIds)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(clientIds);

        var allowedClients = clientIds
            .Where(clientId => !string.IsNullOrWhiteSpace(clientId))
            .ToHashSet(StringComparer.Ordinal);

        if (allowedClients.Count == 0)
        {
            throw new ArgumentException(
                "Informe pelo menos um client_id.",
                nameof(clientIds));
        }

        return builder.RequireAssertion(context =>
        {
            var clientId = context.User.FindFirst(FiapClaimTypes.ClientId)?.Value;
            return clientId is not null && allowedClients.Contains(clientId);
        });
    }
}
