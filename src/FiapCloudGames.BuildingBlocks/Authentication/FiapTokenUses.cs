namespace FiapCloudGames.BuildingBlocks.Authentication;

/// <summary>
/// Tipos de access token aceitos pelas aplicações do FIAP Cloud Games.
/// </summary>
public static class FiapTokenUses
{
    /// <summary>Token de usuário aceito somente por BFFs e API Gateways.</summary>
    public const string User = "user";

    /// <summary>Token de aplicação usado em chamadas internas.</summary>
    public const string Service = "service";

    internal static bool IsSupported(string tokenUse) =>
        tokenUse is User or Service;
}
