namespace FiapCloudGames.BuildingBlocks.Observability;

/// <summary>
/// Fornece o identificador de correlação da requisição atual.
/// </summary>
public interface ICorrelationContext
{
    /// <summary>Obtém o identificador de correlação atual.</summary>
    string CorrelationId { get; }
}
