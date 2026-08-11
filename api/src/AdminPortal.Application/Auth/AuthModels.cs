using System.ComponentModel.DataAnnotations;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.Auth;

public sealed record LoginRequest(
    [param: Required, EmailAddress, MaxLength(255)] string Email,
    [param: Required, MaxLength(128)] string Password);

public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    string FullName,
    string? PhoneNumber,
    UserRole Role,
    UserStatus Status);

public sealed record AccessTokenResponse(
    string AccessToken,
    int ExpiresIn,
    string CsrfToken,
    AuthenticatedUser User);

public sealed record CsrfTokenResponse(string CsrfToken);

public sealed record AuthResult(
    AccessTokenResponse Response,
    string RefreshToken,
    string CsrfToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record AccessTokenIssue(string Token, int ExpiresIn);

public sealed record RefreshTokenIssue(string Token, string Hash, DateTimeOffset ExpiresAt);
