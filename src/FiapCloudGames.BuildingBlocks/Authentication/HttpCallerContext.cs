using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace FiapCloudGames.BuildingBlocks.Authentication;

internal sealed class HttpCallerContext : ICallerContext
{
    private readonly ClaimsPrincipal principal;
    private readonly Lazy<IReadOnlySet<string>> scopes;

    public HttpCallerContext(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);

        principal = httpContextAccessor.HttpContext?.User ??
            new ClaimsPrincipal();
        scopes = new Lazy<IReadOnlySet<string>>(ResolveScopes);
    }

    public string? ClientId =>
        principal.FindFirstValue(FiapClaimTypes.ClientId);

    public string? TokenUse =>
        principal.FindFirstValue(FiapClaimTypes.TokenUse);

    public IReadOnlySet<string> Scopes => scopes.Value;

    public Guid? UserId
    {
        get
        {
            if (!IsUser)
            {
                return null;
            }

            var subject =
                principal.FindFirstValue(FiapClaimTypes.Subject) ??
                principal.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(subject, out var userId)
                ? userId
                : null;
        }
    }

    public bool IsUser =>
        string.Equals(TokenUse, FiapTokenUses.User, StringComparison.Ordinal);

    public bool IsService =>
        string.Equals(TokenUse, FiapTokenUses.Service, StringComparison.Ordinal);

    public bool IsAuthenticated =>
        principal.Identity?.IsAuthenticated == true;

    private IReadOnlySet<string> ResolveScopes() =>
        principal.FindAll(FiapClaimTypes.Scope)
            .SelectMany(claim => claim.Value.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);
}
