using System.ComponentModel.DataAnnotations;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.Students;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminPortal.Api.Controllers;

[ApiController]
[Authorize(Policy = "PortalManagers")]
[Route("api/v1/students")]
public sealed class StudentsController(IStudentService studentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<StudentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<StudentResponse>>> List(
        [FromQuery] StudentListQuery query,
        CancellationToken cancellationToken) =>
        Ok(await studentService.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<StudentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await studentService.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<StudentResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<StudentResponse>> Create(
        CreateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var created = await studentService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<StudentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentResponse>> Update(
        Guid id,
        UpdateStudentRequest request,
        CancellationToken cancellationToken) =>
        Ok(await studentService.UpdateAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/group")]
    [ProducesResponseType<StudentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentResponse>> AssignGroup(
        Guid id,
        AssignStudentGroupRequest request,
        CancellationToken cancellationToken) =>
        Ok(await studentService.AssignGroupAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery, Range(1, int.MaxValue)] int expectedVersion,
        CancellationToken cancellationToken)
    {
        await studentService.DeleteAsync(id, expectedVersion, cancellationToken);
        return NoContent();
    }
}
