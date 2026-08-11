using AdminPortal.Application.Auth;
using AdminPortal.Domain.Entities;

namespace AdminPortal.Application.Common.Interfaces;

public interface ITokenService
{
    AccessTokenIssue CreateAccessToken(User user, Guid sessionId);
    RefreshTokenIssue CreateRefreshToken();
    string CreateCsrfToken();
    string HashOpaqueToken(string token);
}
