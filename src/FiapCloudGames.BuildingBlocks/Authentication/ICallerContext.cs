namespace FiapCloudGames.BuildingBlocks.Authentication;

/// <summary>
/// Expõe a identidade autenticada da aplicação ou do usuário chamador.
/// </summary>
public interface ICallerContext
{
    /// <summary>Obtém o identificador da aplicação cliente.</summary>
    string? ClientId { get; }

    /// <summary>Obtém os scopes concedidos ao chamador.</summary>
    IReadOnlySet<string> Scopes { get; }

    /// <summary>Obtém o usuário quando o sujeito do token for um identificador válido.</summary>
    Guid? UserId { get; }

    /// <summary>Indica se existe uma identidade autenticada.</summary>
    bool IsAuthenticated { get; }
}
