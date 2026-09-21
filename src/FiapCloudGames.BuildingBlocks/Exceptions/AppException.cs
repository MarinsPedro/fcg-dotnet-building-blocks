namespace FiapCloudGames.BuildingBlocks.Exceptions;

/// <summary>
/// Exceção de aplicação com categoria e erros seguros para exposição na API.
/// </summary>
public sealed class AppException : Exception
{
    private AppException(
        AppErrorCategory category,
        string message,
        IReadOnlyCollection<AppError>? errors = null)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Category = category;
        Errors = errors?.ToArray() ?? [];

        if (Errors.Any(error => string.IsNullOrWhiteSpace(error.Message)))
        {
            throw new ArgumentException(
                "Todos os erros devem possuir uma mensagem.",
                nameof(errors));
        }
    }

    /// <summary>Obtém a categoria do erro.</summary>
    public AppErrorCategory Category { get; }

    /// <summary>Obtém os erros detalhados e seguros para o cliente.</summary>
    public IReadOnlyCollection<AppError> Errors { get; }

    /// <summary>Indica se existem erros detalhados.</summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>Cria uma falha de validação.</summary>
    public static AppException Validation(string message) =>
        new(AppErrorCategory.Validation, message);

    /// <summary>Cria uma falha de validação com detalhes por campo.</summary>
    public static AppException Validation(
        IReadOnlyCollection<AppError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException(
                "Informe pelo menos um erro de validação.",
                nameof(errors));
        }

        return new AppException(
            AppErrorCategory.Validation,
            "Um ou mais dados informados são inválidos.",
            errors);
    }

    /// <summary>Cria uma falha de autenticação.</summary>
    public static AppException Authentication(string message) =>
        new(AppErrorCategory.Authentication, message);

    /// <summary>Cria uma falha de autorização.</summary>
    public static AppException Forbidden(string message) =>
        new(AppErrorCategory.Forbidden, message);

    /// <summary>Cria uma falha de recurso não encontrado.</summary>
    public static AppException NotFound(string message) =>
        new(AppErrorCategory.NotFound, message);

    /// <summary>Cria uma falha de conflito.</summary>
    public static AppException Conflict(string message) =>
        new(AppErrorCategory.Conflict, message);

    /// <summary>Cria uma falha de regra de negócio.</summary>
    public static AppException BusinessRule(string message) =>
        new(AppErrorCategory.BusinessRule, message);
}
