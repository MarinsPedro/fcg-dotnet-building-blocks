namespace FiapCloudGames.BuildingBlocks.Web;

/// <summary>
/// Identificadores estáveis dos problemas retornados pelas APIs.
/// </summary>
public static class ApiProblemTypes
{
    private const string Prefix = "urn:fiap-cloud-games:problem:";

    /// <summary>Problema de validação.</summary>
    public const string Validation = Prefix + "validation";

    /// <summary>Requisição inválida.</summary>
    public const string BadRequest = Prefix + "bad-request";

    /// <summary>Falha de autenticação.</summary>
    public const string Unauthorized = Prefix + "unauthorized";

    /// <summary>Falha de autorização.</summary>
    public const string Forbidden = Prefix + "forbidden";

    /// <summary>Recurso não encontrado.</summary>
    public const string NotFound = Prefix + "not-found";

    /// <summary>Conflito com o estado atual.</summary>
    public const string Conflict = Prefix + "conflict";

    /// <summary>Regra de negócio não atendida.</summary>
    public const string BusinessRule = Prefix + "business-rule";

    /// <summary>Erro interno inesperado.</summary>
    public const string InternalServerError = Prefix + "internal-server-error";

    /// <summary>Erro HTTP não especializado.</summary>
    public const string HttpError = Prefix + "http-error";
}
