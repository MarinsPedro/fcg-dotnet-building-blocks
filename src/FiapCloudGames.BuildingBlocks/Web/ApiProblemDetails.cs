using System.Text.Json.Serialization;

namespace FiapCloudGames.BuildingBlocks.Web;

/// <summary>
/// Contrato padronizado de erro HTTP baseado em Problem Details.
/// </summary>
public sealed class ApiProblemDetails
{
    /// <summary>Obtém o identificador estável do tipo do problema.</summary>
    public required string Type { get; init; }

    /// <summary>Obtém o título resumido.</summary>
    public required string Title { get; init; }

    /// <summary>Obtém o status HTTP.</summary>
    public required int Status { get; init; }

    /// <summary>Obtém o detalhe seguro para o cliente.</summary>
    public required string Detail { get; init; }

    /// <summary>Obtém o identificador usado para localizar a requisição.</summary>
    public required string TraceId { get; init; }

    /// <summary>Obtém erros adicionais, quando existentes.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyCollection<ApiError>? Errors { get; init; }
}
