using System.Text.Json.Serialization;

namespace FiapCloudGames.BuildingBlocks.Web;

/// <summary>
/// Representa um erro retornado por uma API.
/// </summary>
public sealed class ApiError
{
    /// <summary>Obtém a mensagem segura para o cliente.</summary>
    public required string Message { get; init; }

    /// <summary>Obtém o campo relacionado ao erro, quando existente.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Field { get; init; }
}
