using AdminPortal.Application.Common.Models;

namespace AdminPortal.Application.AssessmentSheets;

public interface IAssessmentSheetService
{
    Task<PagedResponse<AssessmentSheetListItemResponse>> ListAsync(
        AssessmentSheetListQuery query,
        CancellationToken cancellationToken);

    Task<AssessmentSheetDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<AssessmentSheetDetailResponse> CreateAsync(
        CreateAssessmentSheetRequest request,
        CancellationToken cancellationToken);

    Task<AssessmentSheetDetailResponse> UpdateAsync(
        Guid id,
        UpdateAssessmentSheetRequest request,
        CancellationToken cancellationToken);

    Task<AssessmentSheetDetailResponse> ReplaceRecordsAsync(
        Guid id,
        ReplaceAssessmentSheetRecordsRequest request,
        CancellationToken cancellationToken);

    Task<AssessmentSheetDetailResponse> UpdateStatusAsync(
        Guid id,
        UpdateAssessmentSheetStatusRequest request,
        CancellationToken cancellationToken);

    Task<AssessmentSheetDetailResponse> ExportToSheetAsync(Guid id, CancellationToken cancellationToken);

    Task<AssessmentSheetDetailResponse> SyncToSheetAsync(Guid id, CancellationToken cancellationToken);

    Task<AssessmentSheetDetailResponse> GeneratePlanPdfAsync(Guid id, CancellationToken cancellationToken);

    Task<AssessmentSheetDetailResponse> UploadPlanPdfAsync(
        Guid id, string fileName, byte[] content, CancellationToken cancellationToken);

    Task<AssessmentSheetDetailResponse> GenerateResultPdfAsync(Guid id, CancellationToken cancellationToken);

    Task<AssessmentSheetDetailResponse> SubmitResultsAsync(Guid id, CancellationToken cancellationToken);
}
