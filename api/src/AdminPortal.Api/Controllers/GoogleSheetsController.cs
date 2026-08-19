using System.ComponentModel.DataAnnotations;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.Assessments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AdminPortal.Application.GoogleSheets;

namespace AdminPortal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/google-sheets")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class GoogleSheetsController(IGoogleSheetsService googleSheetsService) : ControllerBase
{
    [HttpPost("sync-assessments")]
    [Authorize(Policy = "PortalManagers")]
    [ProducesResponseType<SyncAssessmentsFromGoogleSheetsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncAssessmentsFromGoogleSheetsResponse>> SyncAssessmentsFromGoogleSheets(
        SyncAssessmentsFromGoogleSheetsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await googleSheetsService.SyncAssessmentsAsync(request, cancellationToken));
}
