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

    Task<ImportAssessmentSheetsPreviewResponse> PreviewExcelImportAsync(
        string fileName,
        byte[] content,
        CancellationToken cancellationToken);

    Task<ImportAssessmentSheetsResponse> ImportExcelAsync(
        string fileName,
        byte[] content,
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

    Task<AssessmentSheetDetailResponse> UploadPlanPdfAsync(
        Guid id, string fileName, byte[] content, CancellationToken cancellationToken);

    Task<AssessmentSheetDetailResponse> UploadResultPdfAsync(
        Guid id, string fileName, byte[] content, CancellationToken cancellationToken);

    Task<AssessmentSheetDetailResponse> SubmitResultsAsync(Guid id, CancellationToken cancellationToken);

    Task<SubmitResultsPreviewResponse> PreviewSubmitResultsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Bulk Action: tải PDF kế hoạch/kết quả của nhiều bảng đánh giá từ Google Drive rồi gộp thành một
    /// file zip. <see cref="AssessmentSheetPdfArchiveFormat.Images"/> render từng trang PDF thành PNG.
    /// Dòng không có link hoặc tải lỗi bị bỏ qua và ghi vào <c>_bo-qua.txt</c> trong zip.
    /// </summary>
    Task<AssessmentSheetPdfArchiveResult> BuildPdfArchiveAsync(
        AssessmentSheetPdfArchiveRequest request, CancellationToken cancellationToken);
}
