using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AdminPortal.Application.Auth;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Domain.Entities;
using AdminPortal.Infrastructure.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AdminPortal.Infrastructure.Security;

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessTokenIssue CreateAccessToken(User user, Guid sessionId)
    {
        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(_options.AccessTokenMinutes);
        var claims = new Claim[]
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new("role", user.Role.ToString()),
            new("sid", sessionId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            now.UtcDateTime,
            expires.UtcDateTime,
            credentials);

        return new AccessTokenIssue(
            new JwtSecurityTokenHandler().WriteToken(token),
            checked((int)TimeSpan.FromMinutes(_options.AccessTokenMinutes).TotalSeconds));
    }

    public RefreshTokenIssue CreateRefreshToken()
    {
        var token = CreateRandomToken(64);
        return new(token, HashOpaqueToken(token), timeProvider.GetUtcNow().AddDays(_options.RefreshTokenDays));
    }

    public string CreateCsrfToken() => CreateRandomToken(32);

    public string HashOpaqueToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string CreateRandomToken(int byteCount) =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(byteCount));
}
