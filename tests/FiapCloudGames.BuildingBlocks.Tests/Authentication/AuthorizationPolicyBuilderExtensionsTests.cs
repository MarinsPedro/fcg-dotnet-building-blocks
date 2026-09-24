using FiapCloudGames.BuildingBlocks.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace FiapCloudGames.BuildingBlocks.Tests.Authentication;

public sealed class AuthorizationPolicyBuilderExtensionsTests
{
    [Fact]
    public void RequireServiceToken_ShouldAddExpectedClaimRequirement()
    {
        var policy = new AuthorizationPolicyBuilder()
            .RequireServiceToken()
            .Build();

        var requirement = Assert.Single(
            policy.Requirements.OfType<ClaimsAuthorizationRequirement>());
        Assert.Equal(FiapClaimTypes.TokenUse, requirement.ClaimType);
        Assert.Equal(
            [FiapTokenUses.Service],
            requirement.AllowedValues);
    }

    [Fact]
    public void RequireTokenUses_ShouldRejectUnsupportedType()
    {
        var builder = new AuthorizationPolicyBuilder();

        var exception = Assert.Throws<ArgumentException>(
            () => builder.RequireTokenUses("delegated"));

        Assert.Equal("tokenUses", exception.ParamName);
    }
}
