namespace AdminPortal.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken);
    Task<AuthResult> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);
    Task ValidateRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);
    Task<AuthenticatedUser> GetMeAsync(CancellationToken cancellationToken);
}
