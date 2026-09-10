using AdminPortal.Application.AssessmentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminPortal.Api.Controllers;

[ApiController]
[Authorize(Policy = "PortalManagers")]
[Route("api/v1/students/{studentId:guid}/assessment-results")]
public sealed class AssessmentResultsController(IAssessmentResultsService assessmentResultsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AssessmentResultsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentResultsResponse>> Get(
        Guid studentId,
        CancellationToken cancellationToken) =>
        Ok(await assessmentResultsService.GetAsync(studentId, cancellationToken));

    [HttpPatch]
    [ProducesResponseType<AssessmentResultsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentResultsResponse>> Update(
        Guid studentId,
        UpdateAssessmentResultsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await assessmentResultsService.UpdateAsync(studentId, request, cancellationToken));
}
