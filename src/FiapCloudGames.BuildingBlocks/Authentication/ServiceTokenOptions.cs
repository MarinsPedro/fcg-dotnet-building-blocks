namespace FiapCloudGames.BuildingBlocks.Authentication;

/// <summary>
/// Configura a obtenção de tokens técnicos pelo fluxo Client Credentials.
/// </summary>
public sealed class ServiceTokenOptions
{
    /// <summary>Nome padrão da seção de configuração.</summary>
    public const string DefaultSectionName = "ServiceAuthentication";

    /// <summary>Obtém ou define o endpoint de emissão de tokens.</summary>
    public Uri? TokenEndpoint { get; set; }

    /// <summary>Obtém ou define o identificador desta aplicação.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Obtém ou define o segredo desta aplicação.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Indica se o endpoint de token deve usar HTTPS.</summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>Obtém ou define a antecedência usada para renovar o token.</summary>
    public TimeSpan RefreshBeforeExpiration { get; set; } =
        TimeSpan.FromSeconds(30);
}
