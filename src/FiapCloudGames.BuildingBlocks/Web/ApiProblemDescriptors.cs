using FiapCloudGames.BuildingBlocks.Exceptions;
using Microsoft.AspNetCore.Http;

namespace FiapCloudGames.BuildingBlocks.Web;

internal static class ApiProblemDescriptors
{
    public static ApiProblemDescriptor Validation { get; } = new(
        StatusCodes.Status400BadRequest,
        ApiProblemTypes.Validation,
        "Um ou mais dados são inválidos",
        "Verifique os dados informados.");

    public static ApiProblemDescriptor BadRequest { get; } = new(
        StatusCodes.Status400BadRequest,
        ApiProblemTypes.BadRequest,
        "Requisição inválida",
        "Verifique os dados enviados na requisição.");

    public static ApiProblemDescriptor Unauthorized { get; } = new(
        StatusCodes.Status401Unauthorized,
        ApiProblemTypes.Unauthorized,
        "Não autenticado",
        "Token ausente, inválido ou expirado.");

    public static ApiProblemDescriptor Forbidden { get; } = new(
        StatusCodes.Status403Forbidden,
        ApiProblemTypes.Forbidden,
        "Acesso negado",
        "O chamador autenticado não possui a permissão necessária.");

    public static ApiProblemDescriptor NotFound { get; } = new(
        StatusCodes.Status404NotFound,
        ApiProblemTypes.NotFound,
        "Recurso não encontrado",
        "O recurso informado não foi encontrado.");

    public static ApiProblemDescriptor Conflict { get; } = new(
        StatusCodes.Status409Conflict,
        ApiProblemTypes.Conflict,
        "Conflito",
        "Não foi possível concluir a operação devido a um conflito.");

    public static ApiProblemDescriptor BusinessRule { get; } = new(
        StatusCodes.Status422UnprocessableEntity,
        ApiProblemTypes.BusinessRule,
        "Regra de negócio não atendida",
        "A regra de negócio necessária para a operação não foi atendida.");

    public static ApiProblemDescriptor InternalServerError { get; } = new(
        StatusCodes.Status500InternalServerError,
        ApiProblemTypes.InternalServerError,
        "Erro interno",
        "Não foi possível concluir a operação.");

    public static ApiProblemDescriptor FromCategory(
        AppErrorCategory category) =>
        category switch
        {
            AppErrorCategory.Validation => Validation,
            AppErrorCategory.Authentication => Unauthorized,
            AppErrorCategory.Forbidden => Forbidden,
            AppErrorCategory.NotFound => NotFound,
            AppErrorCategory.Conflict => Conflict,
            AppErrorCategory.BusinessRule => BusinessRule,
            _ => InternalServerError
        };

    public static ApiProblemDescriptor FromStatusCode(int status) =>
        status switch
        {
            StatusCodes.Status400BadRequest => BadRequest,
            StatusCodes.Status401Unauthorized => Unauthorized,
            StatusCodes.Status403Forbidden => Forbidden,
            StatusCodes.Status404NotFound => NotFound,
            StatusCodes.Status409Conflict => Conflict,
            StatusCodes.Status422UnprocessableEntity => BusinessRule,
            StatusCodes.Status500InternalServerError => InternalServerError,
            _ => new ApiProblemDescriptor(
                status,
                ApiProblemTypes.HttpError,
                "Erro HTTP",
                "Ocorreu um erro ao processar a requisição.")
        };
}

internal sealed record ApiProblemDescriptor(
    int Status,
    string Type,
    string Title,
    string DefaultDetail);
