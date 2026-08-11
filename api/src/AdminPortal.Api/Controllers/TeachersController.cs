using AdminPortal.Application.Common.Models;
using AdminPortal.Application.Teachers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminPortal.Api.Controllers;

[ApiController]
[Authorize(Policy = "PortalManagers")]
[Route("api/v1/teachers")]
public sealed class TeachersController(ITeacherService teacherService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<TeacherResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<TeacherResponse>>> List(
        [FromQuery] TeacherListQuery query,
        CancellationToken cancellationToken) => Ok(await teacherService.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TeacherResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TeacherResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await teacherService.GetAsync(id, cancellationToken));

    [HttpPut("{id:guid}/attendance-policy")]
    [ProducesResponseType<TeacherResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TeacherResponse>> UpdateAttendancePolicy(
        Guid id,
        UpdateAttendancePolicyRequest request,
        CancellationToken cancellationToken) =>
        Ok(await teacherService.UpdateAttendancePolicyAsync(id, request, cancellationToken));
}
