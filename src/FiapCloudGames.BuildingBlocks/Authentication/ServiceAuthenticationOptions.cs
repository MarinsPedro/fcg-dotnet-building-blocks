namespace FiapCloudGames.BuildingBlocks.Authentication;

/// <summary>
/// Configura a validação de tokens recebidos por uma API.
/// </summary>
public sealed class ServiceAuthenticationOptions
{
    /// <summary>Nome padrão da seção de configuração.</summary>
    public const string DefaultSectionName = "Authentication";

    /// <summary>Obtém ou define a autoridade que publica os metadados OIDC.</summary>
    public string? Authority { get; set; }

    /// <summary>Obtém ou define um endereço de metadados OIDC não convencional.</summary>
    public string? MetadataAddress { get; set; }

    /// <summary>Obtém ou define o emissor aceito.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Obtém ou define a audience desta API.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Obtém ou define os tipos de token aceitos pela aplicação.
    /// APIs internas aceitam somente <c>service</c> por padrão.
    /// </summary>
    public string[] AllowedTokenUses { get; set; } = [FiapTokenUses.Service];

    /// <summary>Indica se os metadados exigem HTTPS.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>Obtém ou define a tolerância máxima para diferenças de relógio.</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);
}
