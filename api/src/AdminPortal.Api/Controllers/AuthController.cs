using AdminPortal.Api.Authentication;
using AdminPortal.Api.Configuration;
using AdminPortal.Application.Auth;
using AdminPortal.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AdminPortal.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IAuthService authService,
    CsrfTokenValidator csrfTokenValidator,
    IOptions<SecurityOptions> securityOptions) : ControllerBase
{
    private readonly SecurityOptions _security = securityOptions.Value;

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType<AccessTokenResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccessTokenResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, GetIpAddress(), cancellationToken);
        WriteAuthCookies(result);
        return Ok(result.Response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-refresh")]
    [ProducesResponseType<AccessTokenResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccessTokenResponse>> Refresh(CancellationToken cancellationToken)
    {
        csrfTokenValidator.Validate(Request);
        var refreshToken = Request.Cookies[_security.RefreshCookieName]
            ?? throw new UnauthorizedException("Không tìm thấy refresh token.");
        var result = await authService.RefreshAsync(refreshToken, GetIpAddress(), cancellationToken);
        WriteAuthCookies(result);
        return Ok(result.Response);
    }

    [HttpGet("csrf")]
    [AllowAnonymous]
    [ProducesResponseType<CsrfTokenResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CsrfTokenResponse>> GetCsrfToken(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[_security.RefreshCookieName]
            ?? throw new UnauthorizedException("Không tìm thấy refresh token.");
        var csrfToken = Request.Cookies[_security.CsrfCookieName]
            ?? throw new UnauthorizedException("Không tìm thấy CSRF token.");
        await authService.ValidateRefreshTokenAsync(refreshToken, cancellationToken);
        return Ok(new CsrfTokenResponse(csrfToken));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[_security.RefreshCookieName];
        if (refreshToken is not null)
        {
            csrfTokenValidator.Validate(Request);
            await authService.LogoutAsync(refreshToken, GetIpAddress(), cancellationToken);
        }

        DeleteAuthCookies();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<AuthenticatedUser>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthenticatedUser>> Me(CancellationToken cancellationToken) =>
        Ok(await authService.GetMeAsync(cancellationToken));

    private void WriteAuthCookies(AuthResult result)
    {
        Response.Cookies.Append(_security.RefreshCookieName, result.RefreshToken, CreateCookieOptions(true, result.RefreshTokenExpiresAt));
        Response.Cookies.Append(_security.CsrfCookieName, result.CsrfToken, CreateCookieOptions(false, result.RefreshTokenExpiresAt));
    }

    private void DeleteAuthCookies()
    {
        var options = CreateCookieOptions(false, DateTimeOffset.UnixEpoch);
        Response.Cookies.Delete(_security.RefreshCookieName, options);
        Response.Cookies.Delete(_security.CsrfCookieName, options);
    }

    private static CookieOptions CreateCookieOptions(bool httpOnly, DateTimeOffset expiresAt) => new()
    {
        HttpOnly = httpOnly,
        Secure = true,
        SameSite = SameSiteMode.None,
        Expires = expiresAt,
        Path = "/api/v1/auth",
        IsEssential = true
    };

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
