namespace FiapCloudGames.BuildingBlocks.Observability;

/// <summary>
/// Constantes usadas na propagação do identificador de correlação.
/// </summary>
public static class CorrelationIdConstants
{
    /// <summary>Header HTTP de correlação.</summary>
    public const string HeaderName = "X-Correlation-ID";

    internal const string ItemName =
        "FiapCloudGames.BuildingBlocks.CorrelationId";
}
