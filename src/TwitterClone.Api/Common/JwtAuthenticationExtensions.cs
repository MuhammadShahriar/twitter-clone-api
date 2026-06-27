using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TwitterClone.Api.Hubs;
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

                // Browsers can't set the Authorization header on a WebSocket handshake, so SignalR clients
                // pass the access token in the query string instead. Accept it ONLY for the hub paths (REST
                // endpoints still require the Authorization header — a query-string token there would be a
                // leak risk, e.g. via logs/referrers). Both hubs share this one rule — adding the chat hub
                // here rather than forking a second token-extraction mechanism.
                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = messageContext =>
                    {
                        var accessToken = messageContext.Request.Query["access_token"];
                        var path = messageContext.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken)
                            && (path.StartsWithSegments(NotificationHub.Path)
                                || path.StartsWithSegments(ChatHub.Path)))
                        {
                            messageContext.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization();

        return services;
    }
}
