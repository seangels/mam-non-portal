using System.ComponentModel.DataAnnotations;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.AssessmentGroups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminPortal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/assessment-groups")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class AssessmentGroupController(IAssessmentGroupService assessmentGroupService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<AssessmentGroupListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AssessmentGroupListItemResponse>>> List(
        [FromQuery] AssessmentGroupListQuery query,
        CancellationToken cancellationToken) => Ok(await assessmentGroupService.ListAsync(query, cancellationToken));

    [HttpGet("{name}")]
    [ProducesResponseType<AssessmentGroupListItemResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentGroupListItemResponse>> Get(string name, CancellationToken cancellationToken) =>
        Ok(await assessmentGroupService.GetAsync(name, cancellationToken));
}
