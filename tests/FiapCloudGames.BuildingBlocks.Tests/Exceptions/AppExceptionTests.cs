using FiapCloudGames.BuildingBlocks.Exceptions;

namespace FiapCloudGames.BuildingBlocks.Tests.Exceptions;

public sealed class AppExceptionTests
{
    [Fact]
    public void Validation_WithErrors_ShouldPreserveSafeDetails()
    {
        var errors = new[]
        {
            new AppError("Informe o nome.", "name")
        };

        var exception = AppException.Validation(errors);

        Assert.Equal(AppErrorCategory.Validation, exception.Category);
        Assert.True(exception.HasErrors);
        Assert.Equal(errors, exception.Errors);
    }

    [Fact]
    public void Validation_WithNoErrors_ShouldRejectConfiguration()
    {
        Assert.Throws<ArgumentException>(() =>
            AppException.Validation([]));
    }
}
