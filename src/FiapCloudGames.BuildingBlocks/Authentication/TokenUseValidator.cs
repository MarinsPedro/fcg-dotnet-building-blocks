using System.Security.Claims;

namespace FiapCloudGames.BuildingBlocks.Authentication;

internal static class TokenUseValidator
{
    internal static string? GetFailure(
        ClaimsPrincipal principal,
        IEnumerable<string> allowedTokenUses)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(allowedTokenUses);

        var tokenUse = GetSingleClaim(principal, FiapClaimTypes.TokenUse);
        if (tokenUse is null || !FiapTokenUses.IsSupported(tokenUse))
        {
            return "O token_use está ausente ou não é suportado.";
        }

        if (!allowedTokenUses.Contains(tokenUse, StringComparer.Ordinal))
        {
            return "O tipo de token não é aceito por esta aplicação.";
        }

        var clientId = GetSingleClaim(principal, FiapClaimTypes.ClientId);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return "O client_id está ausente ou é inválido.";
        }

        var subject = GetSingleClaim(principal, FiapClaimTypes.Subject);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return "O sub está ausente ou é inválido.";
        }

        if (tokenUse == FiapTokenUses.User && !Guid.TryParse(subject, out _))
        {
            return "O sub do token de usuário deve ser um identificador válido.";
        }

        if (tokenUse == FiapTokenUses.Service &&
            !string.Equals(subject, clientId, StringComparison.Ordinal))
        {
            return "O sub do token de serviço deve corresponder ao client_id.";
        }

        return null;
    }

    private static string? GetSingleClaim(
        ClaimsPrincipal principal,
        string claimType)
    {
        var values = principal.FindAll(claimType)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();

        return values.Length == 1 ? values[0] : null;
    }
}
