using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AdminPortal.Application.Auth;

public sealed class AuthService(
    IApplicationDbContext dbContext,
    IPasswordService passwordService,
    ITokenService tokenService,
    ICurrentActor currentActor,
    TimeProvider timeProvider) : IAuthService
{
    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<AuthResult> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (user is null)
        {
            AddAudit(null, "Auth.LoginFailed", "User", null, ipAddress, null);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("Email hoặc mật khẩu không đúng.");
        }

        if (user.Status != UserStatus.Active || user.LockoutEnd > now)
        {
            AddAudit(user.Id, "Auth.LoginFailed", "User", user.Id, ipAddress, new { reason = "account_not_active" });
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("Email hoặc mật khẩu không đúng.");
        }

        if (!passwordService.Verify(user, user.PasswordHash, request.Password))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedLogins)
            {
                user.LockoutEnd = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
            }

            user.UpdatedAt = now;
            AddAudit(user.Id, "Auth.LoginFailed", "User", user.Id, ipAddress, new { reason = "invalid_credentials" });
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("Email hoặc mật khẩu không đúng.");
        }

        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        user.UpdatedAt = now;

        var result = CreateSession(user, now, ipAddress);
        AddAudit(user.Id, "Auth.LoginSucceeded", "AuthSession", result.Session.Id, ipAddress, null);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result.Result;
    }

    public async Task<AuthResult> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var hash = tokenService.HashOpaqueToken(refreshToken);
        var previousSession = await dbContext.AuthSessions
            .Include(session => session.User)
            .SingleOrDefaultAsync(session => session.RefreshTokenHash == hash, cancellationToken);

        if (previousSession is null ||
            previousSession.RevokedAt is not null ||
            previousSession.RefreshTokenExpiresAt <= now ||
            previousSession.User.Status != UserStatus.Active ||
            previousSession.User.DeletedAt is not null)
        {
            throw new UnauthorizedException("Refresh token không hợp lệ hoặc đã hết hạn.");
        }

        previousSession.RevokedAt = now;
        previousSession.LastRefreshedAt = now;
        var result = CreateSession(previousSession.User, now, ipAddress);
        AddAudit(previousSession.UserId, "Auth.Refreshed", "AuthSession", result.Session.Id, ipAddress, null);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result.Result;
    }

    public async Task LogoutAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var hash = tokenService.HashOpaqueToken(refreshToken);
        var session = await dbContext.AuthSessions.SingleOrDefaultAsync(
            candidate => candidate.RefreshTokenHash == hash,
            cancellationToken);

        if (session is not null && session.RevokedAt is null)
        {
            session.RevokedAt = now;
            AddAudit(session.UserId, "Auth.Logout", "AuthSession", session.Id, ipAddress, null);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ValidateRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var hash = tokenService.HashOpaqueToken(refreshToken);
        var isActive = await dbContext.AuthSessions.AsNoTracking()
            .AnyAsync(session =>
                session.RefreshTokenHash == hash &&
                session.RevokedAt == null &&
                session.RefreshTokenExpiresAt > now &&
                session.User.Status == UserStatus.Active,
                cancellationToken);
        if (!isActive)
        {
            throw new UnauthorizedException("Refresh token không hợp lệ hoặc đã hết hạn.");
        }
    }

    public async Task<AuthenticatedUser> GetMeAsync(CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == actor.UserId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        return MapUser(user);
    }

    private (AuthSession Session, AuthResult Result) CreateSession(User user, DateTimeOffset now, string? ipAddress)
    {
        var refreshToken = tokenService.CreateRefreshToken();
        var session = new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RefreshTokenHash = refreshToken.Hash,
            RefreshTokenExpiresAt = refreshToken.ExpiresAt,
            CreatedAt = now,
            CreatedByIp = ipAddress
        };
        dbContext.AuthSessions.Add(session);

        var accessToken = tokenService.CreateAccessToken(user, session.Id);
        var csrfToken = tokenService.CreateCsrfToken();
        var response = new AccessTokenResponse(accessToken.Token, accessToken.ExpiresIn, csrfToken, MapUser(user));
        return (session, new AuthResult(response, refreshToken.Token, csrfToken, refreshToken.ExpiresAt));
    }

    private void AddAudit(
        Guid? actorUserId,
        string action,
        string entityType,
        Guid? entityId,
        string? ipAddress,
        object? newValues)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            NewValues = newValues is null ? null : System.Text.Json.JsonSerializer.Serialize(newValues),
            IpAddress = ipAddress,
            CreatedAt = timeProvider.GetUtcNow()
        });
    }

    private static AuthenticatedUser MapUser(User user) =>
        new(user.Id, user.Email, user.FullName, user.PhoneNumber, user.Role, user.Status);
}
