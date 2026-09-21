namespace FiapCloudGames.BuildingBlocks.Exceptions;

/// <summary>
/// Descreve um erro de aplicação opcionalmente associado a um campo.
/// </summary>
/// <param name="Message">Mensagem segura para o cliente.</param>
/// <param name="Field">Campo relacionado ao erro.</param>
public sealed record AppError(
    string Message,
    string? Field = null);
