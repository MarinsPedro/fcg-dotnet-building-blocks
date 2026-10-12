namespace FiapCloudGames.BuildingBlocks.Authentication;

/// <summary>
/// Configura a obtenção de tokens <c>service</c> por Client Credentials.
/// </summary>
public sealed class ServiceTokenOptions
{
    /// <summary>Nome padrão da seção de configuração.</summary>
    public const string DefaultSectionName = "ServiceAuthentication";

    /// <summary>
    /// Obtém ou define a autoridade usada para descobrir o endpoint de token.
    /// </summary>
    public Uri? Authority { get; set; }

    /// <summary>
    /// Obtém ou define um documento de Discovery em endereço não convencional.
    /// </summary>
    public Uri? MetadataAddress { get; set; }

    /// <summary>
    /// Obtém ou define diretamente o endpoint de emissão de tokens.
    /// Quando informado, tem precedência sobre Discovery.
    /// </summary>
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

    /// <summary>
    /// Obtém ou define por quanto tempo o endpoint descoberto fica em cache.
    /// </summary>
    public TimeSpan DiscoveryRefreshInterval { get; set; } =
        TimeSpan.FromHours(1);

    /// <summary>Obtém ou define o timeout de Discovery e emissão de token.</summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Obtém ou define a quantidade máxima de novas tentativas.</summary>
    public int RetryCount { get; set; } = 2;

    /// <summary>Obtém ou define o atraso base do retry exponencial.</summary>
    public TimeSpan RetryBaseDelay { get; set; } =
        TimeSpan.FromMilliseconds(200);

    /// <summary>Obtém ou define a quantidade máxima de tokens em cache.</summary>
    public int MaxCachedTokens { get; set; } = 128;

    /// <summary>
    /// Indica se um endpoint descoberto deve usar o mesmo host dos metadados,
    /// salvo quando estiver explicitamente permitido.
    /// </summary>
    public bool RequireSameHostForDiscoveredTokenEndpoint { get; set; } = true;

    /// <summary>
    /// Obtém ou define hosts adicionais permitidos para endpoints descobertos.
    /// </summary>
    public string[] AllowedTokenEndpointHosts { get; set; } = [];
}
