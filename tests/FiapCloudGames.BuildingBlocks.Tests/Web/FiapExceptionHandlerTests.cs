using System.Text.Json;
using FiapCloudGames.BuildingBlocks.Exceptions;
using FiapCloudGames.BuildingBlocks.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace FiapCloudGames.BuildingBlocks.Tests.Web;

public sealed class FiapExceptionHandlerTests
{
    [Fact]
    public async Task Handler_ShouldWriteValidationProblemDetails()
    {
        var context = CreateContext();
        var handler = new FiapExceptionHandler(
            NullLogger<FiapExceptionHandler>.Instance);
        var exception = AppException.Validation(
            [new AppError("Informe o e-mail.", "email")]);

        var handled = await handler.TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        var body = await ReadBodyAsync(context);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(
            ApiProblemTypes.Validation,
            root.GetProperty("type").GetString());
        Assert.Equal(
            "email",
            root.GetProperty("errors")[0].GetProperty("field").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            root.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Handler_ShouldHideUnexpectedExceptionDetails()
    {
        var context = CreateContext();
        var handler = new FiapExceptionHandler(
            NullLogger<FiapExceptionHandler>.Instance);

        await handler.TryHandleAsync(
            context,
            new InvalidOperationException("database-password-was-here"),
            CancellationToken.None);

        var body = await ReadBodyAsync(context);

        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            context.Response.StatusCode);
        Assert.DoesNotContain("database-password-was-here", body);
        Assert.Contains("Não foi possível concluir a operação.", body);
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }
}
