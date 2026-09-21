using FiapCloudGames.BuildingBlocks.Exceptions;

namespace FiapCloudGames.BuildingBlocks.Authentication;

/// <summary>
/// Operações auxiliares para o contexto do chamador.
/// </summary>
public static class CallerContextExtensions
{
    /// <summary>Obtém o cliente autenticado ou lança uma falha de autenticação.</summary>
    public static string GetRequiredClientId(this ICallerContext caller)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return string.IsNullOrWhiteSpace(caller.ClientId)
            ? throw AppException.Authentication(
                "O identificador da aplicação autenticada é inválido.")
            : caller.ClientId;
    }

    /// <summary>Obtém o usuário autenticado ou lança uma falha de autenticação.</summary>
    public static Guid GetRequiredUserId(this ICallerContext caller)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return caller.UserId ??
            throw AppException.Authentication(
                "O identificador do usuário autenticado é inválido.");
    }
}
