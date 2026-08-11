namespace AdminPortal.Application.Setup;

public interface ISetupService
{
    Task<SetupStatusResponse> GetStatusAsync(CancellationToken cancellationToken);

    Task<SetupSuperAdminResponse> CreateSuperAdminAsync(
        CreateSuperAdminRequest request,
        string? ipAddress,
        CancellationToken cancellationToken);
}
