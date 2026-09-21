namespace FiapCloudGames.BuildingBlocks.Tests;

public sealed class BuildingBlocksAssemblyReferenceTests
{
    [Fact]
    public void AssemblyReference_ShouldPointToBuildingBlocksAssembly()
    {
        var assembly = BuildingBlocksAssemblyReference.Assembly;

        Assert.Equal(
            "FiapCloudGames.BuildingBlocks",
            assembly.GetName().Name);
    }
}
