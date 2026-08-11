using AdminPortal.Application.Attendance;
using AdminPortal.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminPortal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/attendance")]
public sealed class AttendanceController(IAttendanceService attendanceService) : ControllerBase
{
    [HttpGet("context")]
    [ProducesResponseType<AttendanceContextResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceContextResponse>> Context(
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken) => Ok(await attendanceService.GetContextAsync(date, cancellationToken));

    [HttpGet("daily")]
    [ProducesResponseType<AttendanceDailyResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceDailyResponse>> Daily(
        [FromQuery] DateOnly date,
        [FromQuery] Guid? groupId,
        CancellationToken cancellationToken) => Ok(await attendanceService.GetDailyAsync(date, groupId, cancellationToken));

    [HttpPost("sheets")]
    [ProducesResponseType<AttendanceDailyResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AttendanceDailyResponse>> Create(
        CreateAttendanceSheetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.CreateAsync(request, cancellationToken);
        return Created($"/api/v1/attendance/sheets/{result.SheetId}", result);
    }

    [HttpPut("sheets/{sheetId:guid}")]
    [ProducesResponseType<AttendanceDailyResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceDailyResponse>> Update(
        Guid sheetId,
        UpdateAttendanceSheetRequest request,
        CancellationToken cancellationToken) => Ok(await attendanceService.UpdateAsync(sheetId, request, cancellationToken));

    [HttpPost("sheets/historical-recovery")]
    [Authorize(Policy = "PortalManagers")]
    [ProducesResponseType<AttendanceDailyResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AttendanceDailyResponse>> Recover(
        HistoricalRecoveryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.RecoverAsync(request, cancellationToken);
        return Created($"/api/v1/attendance/sheets/{result.SheetId}", result);
    }

    [HttpGet("historical-recovery/group-candidates")]
    [Authorize(Policy = "PortalManagers")]
    [ProducesResponseType<PagedResponse<HistoricalGroupCandidateResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<HistoricalGroupCandidateResponse>>> GroupCandidates(
        [FromQuery] CandidateListQuery query,
        CancellationToken cancellationToken) =>
        Ok(await attendanceService.ListGroupCandidatesAsync(query, cancellationToken));

    [HttpGet("historical-recovery/student-candidates")]
    [Authorize(Policy = "PortalManagers")]
    [ProducesResponseType<PagedResponse<HistoricalStudentCandidateResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<HistoricalStudentCandidateResponse>>> StudentCandidates(
        [FromQuery] CandidateListQuery query,
        CancellationToken cancellationToken) =>
        Ok(await attendanceService.ListStudentCandidatesAsync(query, cancellationToken));

    [HttpGet("historical-recovery/teacher-candidates")]
    [Authorize(Policy = "PortalManagers")]
    [ProducesResponseType<PagedResponse<HistoricalTeacherCandidateResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<HistoricalTeacherCandidateResponse>>> TeacherCandidates(
        [FromQuery] CandidateListQuery query,
        CancellationToken cancellationToken) =>
        Ok(await attendanceService.ListTeacherCandidatesAsync(query, cancellationToken));
}
