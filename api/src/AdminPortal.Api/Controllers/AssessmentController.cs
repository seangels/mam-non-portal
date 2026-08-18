using System.ComponentModel.DataAnnotations;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.Assessments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminPortal.Api.Controllers;

[ApiController]
[Route("api/v1/assessments")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class AssessmentController(IAssessmentService assessmentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<AssessmentListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AssessmentListItemResponse>>> List(
        [FromQuery] AssessmentListQuery query,
        CancellationToken cancellationToken) => Ok(await assessmentService.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AssessmentDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentDetailResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await assessmentService.GetAsync(id, cancellationToken));
}
