using Microsoft.AspNetCore.Authorization;

namespace FiapCloudGames.BuildingBlocks.Authentication;

internal sealed record ScopeRequirement(
    IReadOnlySet<string> RequiredScopes) : IAuthorizationRequirement;

internal sealed class ScopeAuthorizationHandler :
    AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopeRequirement requirement)
    {
        var grantedScopes = context.User
            .FindAll(FiapClaimTypes.Scope)
            .SelectMany(claim => claim.Value.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);

        if (requirement.RequiredScopes.All(grantedScopes.Contains))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
