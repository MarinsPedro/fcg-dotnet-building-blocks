using System.Security.Claims;
using FiapCloudGames.BuildingBlocks.Authentication;
using Microsoft.AspNetCore.Http;

namespace FiapCloudGames.BuildingBlocks.Tests.Authentication;

public sealed class HttpCallerContextTests
{
    [Fact]
    public void Context_ShouldReadTechnicalCallerAndSpaceSeparatedScopes()
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
        Assert.Equal("orders-service", caller.ClientId);
        Assert.Equal(userId, caller.UserId);
        Assert.Equal(
            new[] { "catalog.read", "orders.read", "orders.write" },
            caller.Scopes.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Context_ShouldNotTreatServiceSubjectAsUserId()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                [new Claim(FiapClaimTypes.Subject, "orders-service")],
                "test"))
        };

        var caller = new HttpCallerContext(
            new HttpContextAccessor { HttpContext = context });

        Assert.Null(caller.UserId);
    }
}
