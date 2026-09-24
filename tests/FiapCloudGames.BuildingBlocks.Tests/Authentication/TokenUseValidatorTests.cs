using System.Security.Claims;
using FiapCloudGames.BuildingBlocks.Authentication;

namespace FiapCloudGames.BuildingBlocks.Tests.Authentication;

public sealed class TokenUseValidatorTests
{
    [Fact]
    public void Validator_ShouldAcceptUserTokenAtTheEdge()
    {
        var principal = CreatePrincipal(
            FiapTokenUses.User,
            Guid.NewGuid().ToString(),
            "web-bff");

        var failure = TokenUseValidator.GetFailure(
            principal,
            [FiapTokenUses.User]);

        Assert.Null(failure);
    }

    [Fact]
    public void Validator_ShouldAcceptServiceTokenInternally()
    {
        var principal = CreatePrincipal(
            FiapTokenUses.Service,
            "orders-api",
            "orders-api");

        var failure = TokenUseValidator.GetFailure(
            principal,
            [FiapTokenUses.Service]);

        Assert.Null(failure);
    }

    [Fact]
    public void Validator_ShouldRejectUserTokenInInternalApi()
    {
        var principal = CreatePrincipal(
            FiapTokenUses.User,
            Guid.NewGuid().ToString(),
            "web-bff");

        var failure = TokenUseValidator.GetFailure(
            principal,
            [FiapTokenUses.Service]);

        Assert.Equal(
            "O tipo de token não é aceito por esta aplicação.",
            failure);
    }

    [Fact]
    public void Validator_ShouldRejectServiceTokenWithDifferentSubject()
    {
        var principal = CreatePrincipal(
            FiapTokenUses.Service,
            "another-service",
            "orders-api");

        var failure = TokenUseValidator.GetFailure(
            principal,
            [FiapTokenUses.Service]);

        Assert.Equal(
            "O sub do token de serviço deve corresponder ao client_id.",
            failure);
    }

    [Fact]
    public void Validator_ShouldRejectMissingTokenUse()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(FiapClaimTypes.Subject, "orders-api"),
                new Claim(FiapClaimTypes.ClientId, "orders-api")
            ],
            "test"));

        var failure = TokenUseValidator.GetFailure(
            principal,
            [FiapTokenUses.Service]);

        Assert.Equal(
            "O token_use está ausente ou não é suportado.",
            failure);
    }

    private static ClaimsPrincipal CreatePrincipal(
        string tokenUse,
        string subject,
        string clientId) =>
        new(
            new ClaimsIdentity(
            [
                new Claim(FiapClaimTypes.TokenUse, tokenUse),
                new Claim(FiapClaimTypes.Subject, subject),
                new Claim(FiapClaimTypes.ClientId, clientId)
            ],
            "test"));
}
