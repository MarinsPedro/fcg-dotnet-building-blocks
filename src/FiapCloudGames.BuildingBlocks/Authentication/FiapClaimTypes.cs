namespace FiapCloudGames.BuildingBlocks.Authentication;

/// <summary>
/// Claims padronizadas nos tokens do FIAP Cloud Games.
/// </summary>
public static class FiapClaimTypes
{
    /// <summary>Identificador da aplicação cliente.</summary>
    public const string ClientId = "client_id";

    /// <summary>Permissões concedidas ao token.</summary>
    public const string Scope = "scope";

    /// <summary>Identificador do sujeito do token.</summary>
    public const string Subject = "sub";

    /// <summary>Indica se o token representa um usuário ou uma aplicação.</summary>
    public const string TokenUse = "token_use";

    /// <summary>Papel do sujeito autenticado.</summary>
    public const string Role = "role";
}
