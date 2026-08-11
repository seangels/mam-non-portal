using AdminPortal.Application.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AdminPortal.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/setup")]
public sealed class SetupController(ISetupService setupService) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType<SetupStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SetupStatusResponse>> Status(CancellationToken cancellationToken) =>
        Ok(await setupService.GetStatusAsync(cancellationToken));

    [HttpPost("super-admin")]
    [EnableRateLimiting("setup")]
    [ProducesResponseType<SetupSuperAdminResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SetupSuperAdminResponse>> CreateSuperAdmin(
        CreateSuperAdminRequest request,
        CancellationToken cancellationToken)
    {
        var created = await setupService.CreateSuperAdminAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }
}
