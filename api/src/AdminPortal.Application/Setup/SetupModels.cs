using System.ComponentModel.DataAnnotations;

namespace AdminPortal.Application.Setup;

public sealed record SetupStatusResponse(bool RequiresInitialization);

public sealed record CreateSuperAdminRequest(
    [param: Required, EmailAddress, MaxLength(255)] string Email,
    [param: Required, MaxLength(200)] string FullName,
    [param: Required, MaxLength(128)] string Password);

public sealed record SetupSuperAdminResponse(
    Guid Id,
    string Email,
    string FullName);
