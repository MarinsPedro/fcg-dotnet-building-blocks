namespace FiapCloudGames.BuildingBlocks;

/// <summary>
/// Permite localizar o assembly dos building blocks em testes e configurações.
/// </summary>
public static class BuildingBlocksAssemblyReference
{
    /// <summary>
    /// Obtém o assembly que contém os building blocks.
    /// </summary>
    public static readonly System.Reflection.Assembly Assembly =
        typeof(BuildingBlocksAssemblyReference).Assembly;
}
