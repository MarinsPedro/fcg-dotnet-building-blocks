using System.Security.Claims;
using FiapCloudGames.BuildingBlocks.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace FiapCloudGames.BuildingBlocks.Tests.Authentication;

public sealed class ScopeAuthorizationHandlerTests
{
    [Fact]
    public async Task Handler_ShouldSucceedWhenAllScopesAreGranted()
    {
        var requirement = new ScopeRequirement(
            new HashSet<string>(["payments.process", "payments.read"]));
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(
                    FiapClaimTypes.Scope,
                    "payments.read payments.process")
            ],
            "test"));
        var context = new AuthorizationHandlerContext(
            [requirement],
            principal,
            resource: null);

        await new ScopeAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Handler_ShouldFailWhenOneScopeIsMissing()
    {
        var requirement = new ScopeRequirement(
            new HashSet<string>(["payments.process", "payments.refund"]));
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
            [new Claim(FiapClaimTypes.Scope, "payments.process")],
            "test"));
        var context = new AuthorizationHandlerContext(
            [requirement],
            principal,
            resource: null);

        await new ScopeAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
