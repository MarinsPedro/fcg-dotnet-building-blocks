namespace FiapCloudGames.BuildingBlocks.Authentication;

/// <summary>
/// Fornece access tokens de aplicação para chamadas entre serviços.
/// </summary>
public interface IServiceTokenProvider
{
    /// <summary>Obtém um token válido para a audience e os scopes solicitados.</summary>
    ValueTask<string> GetAccessTokenAsync(
        string audience,
        string scopes,
        CancellationToken cancellationToken = default);
}
