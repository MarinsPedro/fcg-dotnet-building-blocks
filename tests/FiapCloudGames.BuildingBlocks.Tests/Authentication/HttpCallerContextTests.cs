using System.Security.Claims;
using FiapCloudGames.BuildingBlocks.Authentication;
using Microsoft.AspNetCore.Http;

namespace FiapCloudGames.BuildingBlocks.Tests.Authentication;

public sealed class HttpCallerContextTests
{
    [Fact]
    public void Context_ShouldReadUserCallerAndSpaceSeparatedScopes()
    {
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                [
                    new Claim(FiapClaimTypes.ClientId, "orders-service"),
                    new Claim(FiapClaimTypes.Scope, "orders.read orders.write"),
                    new Claim(FiapClaimTypes.Scope, "catalog.read"),
                    new Claim(FiapClaimTypes.TokenUse, FiapTokenUses.User),
                    new Claim(FiapClaimTypes.Subject, userId.ToString())
                ],
                "test"))
        };
        var accessor = new HttpContextAccessor
        {
            HttpContext = context
        };

        var caller = new HttpCallerContext(accessor);

        Assert.True(caller.IsAuthenticated);
        Assert.True(caller.IsUser);
        Assert.False(caller.IsService);
        Assert.Equal(FiapTokenUses.User, caller.TokenUse);
        Assert.Equal("orders-service", caller.ClientId);
        Assert.Equal(userId, caller.UserId);
        Assert.Equal(
            new[] { "catalog.read", "orders.read", "orders.write" },
            caller.Scopes.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Context_ShouldNotTreatServiceSubjectAsUserId()
    {
        var serviceId = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                [
                    new Claim(FiapClaimTypes.Subject, serviceId),
                    new Claim(FiapClaimTypes.ClientId, serviceId),
                    new Claim(FiapClaimTypes.TokenUse, FiapTokenUses.Service)
                ],
                "test"))
        };

        var caller = new HttpCallerContext(
            new HttpContextAccessor { HttpContext = context });

        Assert.Null(caller.UserId);
        Assert.False(caller.IsUser);
        Assert.True(caller.IsService);
    }
}
