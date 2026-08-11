using AdminPortal.Application.Common.Models;
using AdminPortal.Application.StudentGroups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminPortal.Api.Controllers;

[ApiController]
[Authorize(Policy = "PortalManagers")]
[Route("api/v1/student-groups")]
public sealed class StudentGroupsController(IStudentGroupService groupService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<StudentGroupResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<StudentGroupResponse>>> List(
        [FromQuery] StudentGroupListQuery query,
        CancellationToken cancellationToken) => Ok(await groupService.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<StudentGroupResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentGroupResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await groupService.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<StudentGroupResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<StudentGroupResponse>> Create(
        CreateStudentGroupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await groupService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<StudentGroupResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentGroupResponse>> Update(
        Guid id,
        UpdateStudentGroupRequest request,
        CancellationToken cancellationToken) => Ok(await groupService.UpdateAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/responsible-teacher")]
    [ProducesResponseType<StudentGroupResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentGroupResponse>> AssignResponsibleTeacher(
        Guid id,
        AssignResponsibleTeacherRequest request,
        CancellationToken cancellationToken) =>
        Ok(await groupService.AssignResponsibleTeacherAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await groupService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
