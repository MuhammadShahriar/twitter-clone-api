using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TwitterClone.Infrastructure.Authentication;

namespace TwitterClone.Api.Common;

public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Registers JWT bearer authentication whose validation parameters mirror exactly what
    /// <c>JwtTokenGenerator</c> signs with — both read the same <see cref="JwtSettings"/>. The
    /// parameters are bound from <see cref="IOptions{TOptions}"/> (resolved lazily), so the effective
    /// configuration — including values injected by integration tests — is always honoured.
    /// </summary>
    public static IServiceCollection AddJwtBearerAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((bearer, jwtSettings) =>
            {
                var settings = jwtSettings.Value;

                // Keep JWT claim names as-is (sub/handle/email) instead of remapping sub -> nameidentifier.
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorization();

        return services;
    }
}
