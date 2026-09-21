using FiapCloudGames.BuildingBlocks.Web;

namespace FiapCloudGames.BuildingBlocks.Tests.Web;

public sealed class ApiBehaviorOptionsConfiguratorTests
{
    [Theory]
    [InlineData("$.User.Email", "user.email")]
    [InlineData("Items.0.GameId", "items.0.gameId")]
    [InlineData("$", null)]
    [InlineData("", null)]
    public void NormalizeFieldName_ShouldProduceCamelCasePath(
        string field,
        string? expected)
    {
        Assert.Equal(
            expected,
            ApiBehaviorOptionsConfigurator.NormalizeFieldName(field));
    }
}
