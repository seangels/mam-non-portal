using AdminPortal.Application.AssessmentSheets;
using AdminPortal.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminPortal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/assessment-sheets")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class AssessmentSheetsController(IAssessmentSheetService assessmentSheetService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<AssessmentSheetListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AssessmentSheetListItemResponse>>> List(
        [FromQuery] AssessmentSheetListQuery query,
        CancellationToken cancellationToken) =>
        Ok(await assessmentSheetService.ListAsync(query, cancellationToken));

    [HttpGet("plan-candidates")]
    [ProducesResponseType<PagedResponse<AssessmentPlanCandidateResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AssessmentPlanCandidateResponse>>> ListPlanCandidates(
        [FromQuery] AssessmentPlanCandidateQuery query,
        CancellationToken cancellationToken) =>
        Ok(await assessmentSheetService.ListPlanCandidatesAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AssessmentSheetDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentSheetDetailResponse>> Get(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await assessmentSheetService.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<AssessmentSheetDetailResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AssessmentSheetDetailResponse>> Create(
        CreateAssessmentSheetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await assessmentSheetService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<AssessmentSheetDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentSheetDetailResponse>> Update(
        Guid id,
        UpdateAssessmentSheetRequest request,
        CancellationToken cancellationToken) =>
        Ok(await assessmentSheetService.UpdateAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/records")]
    [ProducesResponseType<AssessmentSheetDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentSheetDetailResponse>> ReplaceRecords(
        Guid id,
        ReplaceAssessmentSheetRecordsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await assessmentSheetService.ReplaceRecordsAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/status")]
    [ProducesResponseType<AssessmentSheetDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentSheetDetailResponse>> UpdateStatus(
        Guid id,
        UpdateAssessmentSheetStatusRequest request,
        CancellationToken cancellationToken) =>
        Ok(await assessmentSheetService.UpdateStatusAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/export-to-sheet")]
    [ProducesResponseType<AssessmentSheetDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentSheetDetailResponse>> ExportToSheet(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await assessmentSheetService.ExportToSheetAsync(id, cancellationToken));

    [HttpPost("{id:guid}/sync-to-sheet")]
    [ProducesResponseType<AssessmentSheetDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentSheetDetailResponse>> SyncToSheet(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await assessmentSheetService.SyncToSheetAsync(id, cancellationToken));

    [HttpPost("{id:guid}/generate-plan-pdf")]
    [ProducesResponseType<AssessmentSheetDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentSheetDetailResponse>> GeneratePlanPdf(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await assessmentSheetService.GeneratePlanPdfAsync(id, cancellationToken));

    [HttpPost("{id:guid}/generate-result-pdf")]
    [ProducesResponseType<AssessmentSheetDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentSheetDetailResponse>> GenerateResultPdf(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await assessmentSheetService.GenerateResultPdfAsync(id, cancellationToken));

    [HttpPost("{id:guid}/submit-results")]
    [ProducesResponseType<AssessmentSheetDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentSheetDetailResponse>> SubmitResults(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await assessmentSheetService.SubmitResultsAsync(id, cancellationToken));
}
