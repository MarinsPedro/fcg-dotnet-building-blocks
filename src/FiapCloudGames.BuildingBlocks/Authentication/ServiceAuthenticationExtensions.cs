using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FiapCloudGames.BuildingBlocks.Authentication;

/// <summary>
/// Registra a autenticação técnica comum das APIs.
/// </summary>
public static class ServiceAuthenticationExtensions
{
    /// <summary>
    /// Adiciona validação JWT Bearer, contexto do chamador e policies técnicas.
    /// </summary>
    public static IServiceCollection AddFiapServiceAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = ServiceAuthenticationOptions.DefaultSectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        var section = configuration.GetSection(sectionName);

        services.AddOptions<ServiceAuthenticationOptions>()
            .Bind(section)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Issuer),
                $"Configure '{sectionName}:Issuer'.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Audience),
                $"Configure '{sectionName}:Audience'.")
            .Validate(
                options => options.AllowedTokenUses is { Length: > 0 } &&
                    options.AllowedTokenUses.All(FiapTokenUses.IsSupported),
                $"Configure '{sectionName}:AllowedTokenUses' somente com " +
                $"'{FiapTokenUses.User}' ou '{FiapTokenUses.Service}'.")
            .Validate(
                options =>
                    IsValidMetadataEndpoint(
                        options.Authority,
                        options.RequireHttpsMetadata) ||
                    IsValidMetadataEndpoint(
                        options.MetadataAddress,
                        options.RequireHttpsMetadata),
                $"Configure '{sectionName}:Authority' ou " +
                $"'{sectionName}:MetadataAddress' com uma URI absoluta e segura.")
            .Validate(
                options => options.ClockSkew >= TimeSpan.Zero &&
                    options.ClockSkew <= TimeSpan.FromMinutes(5),
                $"'{sectionName}:ClockSkew' deve estar entre zero e cinco minutos.")
            .ValidateOnStart();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<ServiceAuthenticationOptions>>(
                (jwt, configured) =>
                {
                    var options = configured.Value;

                    if (!string.IsNullOrWhiteSpace(options.Authority))
                    {
                        jwt.Authority = options.Authority;
                    }

                    if (!string.IsNullOrWhiteSpace(options.MetadataAddress))
                    {
                        jwt.MetadataAddress = options.MetadataAddress;
                    }
                    jwt.Audience = options.Audience;
                    jwt.RequireHttpsMetadata = options.RequireHttpsMetadata;
                    jwt.MapInboundClaims = false;
                    jwt.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = options.Issuer,
                            ValidateAudience = true,
                            ValidAudience = options.Audience,
                            ValidateIssuerSigningKey = true,
                            ValidateLifetime = true,
                            ClockSkew = options.ClockSkew,
                            NameClaimType = FiapClaimTypes.Subject,
                            RoleClaimType = FiapClaimTypes.Role
                        };

                    jwt.Events.OnTokenValidated = context =>
                    {
                        var failure = TokenUseValidator.GetFailure(
                            context.Principal!,
                            options.AllowedTokenUses);

                        if (failure is not null)
                        {
                            context.Fail(failure);
                        }

                        return Task.CompletedTask;
                    };
                });

        services.AddAuthorization(options =>
        {
            var authenticatedByDefault = new AuthorizationPolicyBuilder(
                    JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();

            options.DefaultPolicy = authenticatedByDefault;
            options.FallbackPolicy = authenticatedByDefault;
        });
        services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICallerContext, HttpCallerContext>();

        return services;
    }

    private static bool IsValidMetadataEndpoint(
        string? value,
        bool requireHttps) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (!requireHttps || uri.Scheme == Uri.UriSchemeHttps);
}
