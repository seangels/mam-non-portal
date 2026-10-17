using System.ComponentModel.DataAnnotations;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.Students;
using AdminPortal.Application.StudentDrive;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminPortal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/students")]
public sealed class StudentsController(IStudentService studentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<StudentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<StudentResponse>>> List(
        [FromQuery] StudentListQuery query,
        CancellationToken cancellationToken) =>
        Ok(await studentService.ListAsync(query, cancellationToken));

    [HttpGet("student-code-suggestions")]
    [Authorize(Policy = "PortalManagers")]
    [ProducesResponseType<StudentCodeSuggestionsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentCodeSuggestionsResponse>> GetStudentCodeSuggestions(
        CancellationToken cancellationToken) =>
        Ok(await studentService.GetStudentCodeSuggestionsAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<StudentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await studentService.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = "PortalManagers")]
    [ProducesResponseType<StudentResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<StudentResponse>> Create(
        CreateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var created = await studentService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "PortalManagers")]
    [ProducesResponseType<StudentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentResponse>> Update(
        Guid id,
        UpdateStudentRequest request,
        CancellationToken cancellationToken) =>
        Ok(await studentService.UpdateAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/group")]
    [Authorize(Policy = "PortalManagers")]
    [ProducesResponseType<StudentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentResponse>> AssignGroup(
        Guid id,
        AssignStudentGroupRequest request,
        CancellationToken cancellationToken) =>
        Ok(await studentService.AssignGroupAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "PortalManagers")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery, Range(1, int.MaxValue)] int expectedVersion,
        CancellationToken cancellationToken)
    {
        await studentService.DeleteAsync(id, expectedVersion, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/drive-folder/rename")]
    [Authorize(Policy = "PortalManagers")]
    [ProducesResponseType<StudentDriveFolderActionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentDriveFolderActionResponse>> RenameDriveFolder(
        Guid id,
        RenameStudentDriveFolderRequest request,
        CancellationToken cancellationToken) =>
        Ok(await studentService.RenameDriveFolderAsync(id, request, cancellationToken));

    [HttpPost("drive-folders/reconcile")]
    [Authorize(Policy = "PortalManagers")]
    [ProducesResponseType<StudentDriveReconcileResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentDriveReconcileResponse>> ReconcileDriveFolders(
        CancellationToken cancellationToken) =>
        Ok(await studentService.ReconcileDriveFoldersAsync(cancellationToken));
}
