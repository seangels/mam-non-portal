using System.ComponentModel.DataAnnotations;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.Teachers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminPortal.Api.Controllers;

[ApiController]
[Authorize(Policy = "PortalManagers")]
[Route("api/v1/teachers")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class TeachersController(ITeacherService teacherService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<TeacherListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<TeacherListItemResponse>>> List(
        [FromQuery] TeacherListQuery query,
        CancellationToken cancellationToken) => Ok(await teacherService.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TeacherDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TeacherDetailResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await teacherService.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<TeacherDetailResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TeacherDetailResponse>> Create(
        CreateTeacherRequest request,
        CancellationToken cancellationToken)
    {
        var created = await teacherService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<TeacherDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TeacherDetailResponse>> Update(
        Guid id,
        UpdateTeacherRequest request,
        CancellationToken cancellationToken) =>
        Ok(await teacherService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery, Range(1, int.MaxValue)] int expectedVersion,
        CancellationToken cancellationToken)
    {
        await teacherService.DeleteAsync(id, expectedVersion, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/attendance-policy")]
    [ProducesResponseType<TeacherDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TeacherDetailResponse>> UpdateAttendancePolicy(
        Guid id,
        UpdateAttendancePolicyRequest request,
        CancellationToken cancellationToken) =>
        Ok(await teacherService.UpdateAttendancePolicyAsync(id, request, cancellationToken));
}
