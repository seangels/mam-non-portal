using AdminPortal.Application.AssessmentSheets;
using AdminPortal.Application.Common.Exceptions;
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
    private const long MaxPdfUploadBytes = 10 * 1024 * 1024;
    private const long MaxExcelImportBytes = 10 * 1024 * 1024;

    [HttpGet]
    [ProducesResponseType<PagedResponse<AssessmentSheetListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AssessmentSheetListItemResponse>>> List(
        [FromQuery] AssessmentSheetListQuery query,
        CancellationToken cancellationToken) =>
        Ok(await assessmentSheetService.ListAsync(query, cancellationToken));

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

    [HttpPost("import-excel/preview")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxExcelImportBytes)]
    [ProducesResponseType<ImportAssessmentSheetsPreviewResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ImportAssessmentSheetsPreviewResponse>> PreviewExcelImport(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        ValidateExcelImportFile(file);
        using var stream = new MemoryStream();
        await file!.CopyToAsync(stream, cancellationToken);
        return Ok(await assessmentSheetService.PreviewExcelImportAsync(file.FileName, stream.ToArray(), cancellationToken));
    }

    [HttpPost("import-excel")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxExcelImportBytes)]
    [ProducesResponseType<ImportAssessmentSheetsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ImportAssessmentSheetsResponse>> ImportExcel(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        ValidateExcelImportFile(file);
        using var stream = new MemoryStream();
        await file!.CopyToAsync(stream, cancellationToken);
        return Ok(await assessmentSheetService.ImportExcelAsync(file.FileName, stream.ToArray(), cancellationToken));
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

    [HttpPost("{id:guid}/upload-plan-pdf")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxPdfUploadBytes)]
    [ProducesResponseType<AssessmentSheetDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentSheetDetailResponse>> UploadPlanPdf(
        Guid id,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new AppValidationException("File PDF kế hoạch không hợp lệ.", new Dictionary<string, string[]>
            {
                ["file"] = ["Vui lòng chọn file PDF có nội dung."]
            });
        }
        if (file.Length > MaxPdfUploadBytes)
        {
            throw new AppValidationException("File PDF kế hoạch vượt quá dung lượng cho phép.", new Dictionary<string, string[]>
            {
                ["file"] = ["Dung lượng tối đa là 10MB."]
            });
        }
        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppValidationException("File PDF kế hoạch không hợp lệ.", new Dictionary<string, string[]>
            {
                ["file"] = ["Vui lòng chọn file PDF."]
            });
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return Ok(await assessmentSheetService.UploadPlanPdfAsync(id, file.FileName, stream.ToArray(), cancellationToken));
    }

    [HttpPost("{id:guid}/upload-result-pdf")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxPdfUploadBytes)]
    [ProducesResponseType<AssessmentSheetDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentSheetDetailResponse>> UploadResultPdf(
        Guid id,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new AppValidationException("File PDF kết quả không hợp lệ.", new Dictionary<string, string[]>
            {
                ["file"] = ["Vui lòng chọn file PDF có nội dung."]
            });
        }
        if (file.Length > MaxPdfUploadBytes)
        {
            throw new AppValidationException("File PDF kết quả vượt quá dung lượng cho phép.", new Dictionary<string, string[]>
            {
                ["file"] = ["Dung lượng tối đa là 10MB."]
            });
        }
        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppValidationException("File PDF kết quả không hợp lệ.", new Dictionary<string, string[]>
            {
                ["file"] = ["Vui lòng chọn file PDF."]
            });
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return Ok(await assessmentSheetService.UploadResultPdfAsync(id, file.FileName, stream.ToArray(), cancellationToken));
    }

    [HttpPost("{id:guid}/submit-results")]
    [ProducesResponseType<AssessmentSheetDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentSheetDetailResponse>> SubmitResults(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await assessmentSheetService.SubmitResultsAsync(id, cancellationToken));

    // Dry-run: đọc/đối chiếu [F0.ĐG] và trả về đúng các ô sẽ thay đổi + thống kê kết quả, KHÔNG ghi.
    [HttpPost("{id:guid}/submit-results/preview")]
    [ProducesResponseType<SubmitResultsPreviewResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SubmitResultsPreviewResponse>> PreviewSubmitResults(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await assessmentSheetService.PreviewSubmitResultsAsync(id, cancellationToken));

    private static void ValidateExcelImportFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            throw new AppValidationException("File Excel import không hợp lệ.", new Dictionary<string, string[]>
            {
                ["file"] = ["Vui lòng chọn file .xlsx có nội dung."]
            });
        }

        if (file.Length > MaxExcelImportBytes)
        {
            throw new AppValidationException("File Excel import vượt quá dung lượng cho phép.", new Dictionary<string, string[]>
            {
                ["file"] = ["Dung lượng tối đa là 10MB."]
            });
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppValidationException("File Excel import không hợp lệ.", new Dictionary<string, string[]>
            {
                ["file"] = ["Vui lòng chọn file .xlsx."]
            });
        }
    }
}
