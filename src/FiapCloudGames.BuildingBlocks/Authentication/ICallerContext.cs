namespace FiapCloudGames.BuildingBlocks.Authentication;

/// <summary>
/// Expõe a identidade autenticada da aplicação ou do usuário chamador.
/// </summary>
public interface ICallerContext
{
    /// <summary>Obtém o tipo do token autenticado.</summary>
    string? TokenUse { get; }

    /// <summary>Obtém o identificador da aplicação cliente.</summary>
    string? ClientId { get; }

    /// <summary>Obtém os scopes concedidos ao chamador.</summary>
    IReadOnlySet<string> Scopes { get; }

    /// <summary>Obtém o usuário somente quando o token for do tipo <c>user</c>.</summary>
    Guid? UserId { get; }

    /// <summary>Indica se o chamador apresentou um token de usuário.</summary>
    bool IsUser { get; }

    /// <summary>Indica se o chamador apresentou um token de aplicação.</summary>
    bool IsService { get; }

    /// <summary>Indica se existe uma identidade autenticada.</summary>
    bool IsAuthenticated { get; }
}
