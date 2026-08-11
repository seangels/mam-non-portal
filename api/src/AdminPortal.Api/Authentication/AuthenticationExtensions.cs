using System.IdentityModel.Tokens.Jwt;
using System.Text;
using AdminPortal.Domain.Enums;
using AdminPortal.Infrastructure.Options;
using AdminPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace AdminPortal.Api.Authentication;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddAdminPortalAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    RoleClaimType = "role",
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateSessionAsync
                };
            });
        services.AddAuthorization(options => options.AddPolicy("PortalManagers", policy =>
            policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.Admin))));
        return services;
    }

    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var subject = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var sessionClaim = context.Principal?.FindFirst("sid")?.Value;
        if (!Guid.TryParse(subject, out var userId) || !Guid.TryParse(sessionClaim, out var sessionId))
        {
            context.Fail("Missing subject or session claim.");
            return;
        }

        var dbContext = context.HttpContext.RequestServices.GetRequiredService<AdminPortalDbContext>();
        var timeProvider = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
        var now = timeProvider.GetUtcNow();
        var isActive = await dbContext.AuthSessions.AsNoTracking()
            .AnyAsync(session =>
                session.Id == sessionId &&
                session.UserId == userId &&
                session.RevokedAt == null &&
                session.RefreshTokenExpiresAt > now &&
                session.User.Status == UserStatus.Active,
                context.HttpContext.RequestAborted);

        if (!isActive)
        {
            context.Fail("Authentication session is no longer active.");
        }
    }
}
