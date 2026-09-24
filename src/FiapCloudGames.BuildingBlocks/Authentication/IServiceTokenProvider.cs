namespace FiapCloudGames.BuildingBlocks.Authentication;

/// <summary>
/// Fornece access tokens do tipo <c>service</c> para chamadas internas.
/// </summary>
public interface IServiceTokenProvider
{
    /// <summary>Obtém um token válido para a audience e os scopes solicitados.</summary>
    ValueTask<string> GetAccessTokenAsync(
        string audience,
        string scopes,
        CancellationToken cancellationToken = default);
}
