namespace FiapCloudGames.BuildingBlocks.Exceptions;

/// <summary>
/// Categorias técnicas usadas para converter falhas em respostas HTTP.
/// </summary>
public enum AppErrorCategory
{
    /// <summary>Falha de validação da entrada.</summary>
    Validation = 1,

    /// <summary>Chamador não autenticado.</summary>
    Authentication,

    /// <summary>Chamador autenticado sem permissão.</summary>
    Forbidden,

    /// <summary>Recurso não encontrado.</summary>
    NotFound,

    /// <summary>Conflito com o estado atual.</summary>
    Conflict,

    /// <summary>Regra de negócio não atendida.</summary>
    BusinessRule
}
