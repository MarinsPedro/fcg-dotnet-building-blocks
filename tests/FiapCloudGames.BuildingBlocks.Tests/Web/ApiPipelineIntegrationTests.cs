using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FiapCloudGames.BuildingBlocks.Exceptions;
using FiapCloudGames.BuildingBlocks.Hosting;
using FiapCloudGames.BuildingBlocks.Observability;
using FiapCloudGames.BuildingBlocks.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FiapCloudGames.BuildingBlocks.Tests.Web;

public sealed class ApiPipelineIntegrationTests
{
    [Theory]
    [InlineData("not-found", HttpStatusCode.NotFound, ApiProblemTypes.NotFound)]
    [InlineData("conflict", HttpStatusCode.Conflict, ApiProblemTypes.Conflict)]
    [InlineData(
        "business-rule",
        HttpStatusCode.UnprocessableEntity,
        ApiProblemTypes.BusinessRule)]
    public async Task AppException_ShouldProduceStandardProblemDetails(
        string path,
        HttpStatusCode expectedStatus,
        string expectedType)
    {
        await using var application = await TestApplication.CreateAsync();
        using var request = CreateAuthenticatedRequest($"/pipeline/{path}");

        using var response = await application.Client.SendAsync(request);
        using var document = await ReadProblemAsync(response);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(expectedType, document.RootElement.GetProperty("type").GetString());
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task InvalidModel_ShouldProduceValidationProblemDetails400()
    {
        await using var application = await TestApplication.CreateAsync();
        using var request = CreateAuthenticatedRequest(
            "/pipeline/model",
            HttpMethod.Post);
        request.Content = JsonContent.Create(new { });

        using var response = await application.Client.SendAsync(request);
        using var document = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            ApiProblemTypes.Validation,
            document.RootElement.GetProperty("type").GetString());
        Assert.Equal(
            "name",
            document.RootElement
                .GetProperty("errors")[0]
                .GetProperty("field")
                .GetString());
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutIdentity_ShouldProduce401()
    {
        await using var application = await TestApplication.CreateAsync();

        using var response = await application.Client.GetAsync(
            "/pipeline/not-found");
        using var document = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            ApiProblemTypes.Unauthorized,
            document.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task PolicyFailure_ShouldProduce403()
    {
        await using var application = await TestApplication.CreateAsync();
        using var request = CreateAuthenticatedRequest("/pipeline/forbidden");

        using var response = await application.Client.SendAsync(request);
        using var document = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            ApiProblemTypes.Forbidden,
            document.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task UnexpectedException_ShouldProduceSafe500()
    {
        await using var application = await TestApplication.CreateAsync();
        using var request = CreateAuthenticatedRequest("/pipeline/unexpected");

        using var response = await application.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Não foi possível concluir a operação.", body);
        Assert.DoesNotContain("database-password", body);
    }

    [Fact]
    public async Task ProblemDetails_ShouldSeparateTraceAndCorrelationIds()
    {
        await using var application = await TestApplication.CreateAsync();
        using var request = CreateAuthenticatedRequest("/pipeline/not-found");
        request.Headers.Add(
            CorrelationIdConstants.HeaderName,
            "integration-correlation-123");

        using var response = await application.Client.SendAsync(request);
        using var document = await ReadProblemAsync(response);
        var root = document.RootElement;

        Assert.Equal(
            "integration-correlation-123",
            root.GetProperty("correlationId").GetString());
        Assert.NotEqual(
            root.GetProperty("correlationId").GetString(),
            root.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task AppException_ShouldProduceOneStructuredClientErrorLog()
    {
        await using var application = await TestApplication.CreateAsync();
        using var request = CreateAuthenticatedRequest("/pipeline/not-found");

        using var response = await application.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var matchingLogs = application.Logs.Entries
            .Where(entry => entry.Message.StartsWith(
                "Requisição rejeitada com",
                StringComparison.Ordinal))
            .ToArray();
        Assert.Single(matchingLogs);
        Assert.Equal(LogLevel.Information, matchingLogs[0].Level);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        string path,
        HttpMethod? method = null)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        request.Headers.Add("X-Test-Identity", "test-client");
        return request;
    }

    private static async Task<JsonDocument> ReadProblemAsync(
        HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private sealed class TestApplication : IAsyncDisposable
    {
        private readonly WebApplication application;

        private TestApplication(
            WebApplication application,
            HttpClient client,
            ListLoggerProvider logs)
        {
            this.application = application;
            Client = client;
            Logs = logs;
        }

        public HttpClient Client { get; }

        public ListLoggerProvider Logs { get; }

        public static async Task<TestApplication> CreateAsync()
        {
            var logs = new ListLoggerProvider();
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(logs);

            builder.AddFiapServiceDefaults();
            builder.Services.AddFiapApiDefaults();
            builder.Services
                .AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                options.AddPolicy(
                    "permission-required",
                    policy => policy.RequireClaim("permission", "granted"));
            });
            builder.Services
                .AddControllers()
                .AddApplicationPart(typeof(PipelineTestController).Assembly);

            var application = builder.Build();
            application.UseFiapApiDefaults();
            application.UseAuthentication();
            application.UseAuthorization();
            application.MapControllers();
            await application.StartAsync();

            return new TestApplication(
                application,
                application.GetTestClient(),
                logs);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await application.DisposeAsync();
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(
            options,
            logger,
            encoder)
    {
        public const string SchemeName = "PipelineTests";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Identity", out var identity))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, identity.ToString())
            };
            if (Request.Headers.ContainsKey("X-Test-Permission"))
            {
                claims.Add(new Claim("permission", "granted"));
            }

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, SchemeName));
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

[ApiController]
[Route("pipeline")]
public sealed class PipelineTestController : ControllerBase
{
    [HttpPost("model")]
    public IActionResult ValidateModel(PipelineTestRequest request) =>
        Ok(request);

    [HttpGet("forbidden")]
    [Authorize(Policy = "permission-required")]
    public IActionResult ForbiddenByPolicy() => Ok();

    [HttpGet("not-found")]
    public IActionResult NotFoundError() =>
        throw AppException.NotFound("Recurso de teste não encontrado.");

    [HttpGet("conflict")]
    public IActionResult ConflictError() =>
        throw AppException.Conflict("Conflito de teste.");

    [HttpGet("business-rule")]
    public IActionResult BusinessRuleError() =>
        throw AppException.BusinessRule("Regra de teste não atendida.");

    [HttpGet("unexpected")]
    public IActionResult UnexpectedError() =>
        throw new InvalidOperationException("database-password");
}

public sealed class PipelineTestRequest
{
    [Required]
    public string? Name { get; init; }
}

public sealed class ListLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> entries = new();

    public IReadOnlyCollection<LogEntry> Entries => entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new ListLogger(entries);

    public void Dispose()
    {
    }

    private sealed class ListLogger(ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(logLevel, formatter(state, exception)));
    }
}

public sealed record LogEntry(LogLevel Level, string Message);
