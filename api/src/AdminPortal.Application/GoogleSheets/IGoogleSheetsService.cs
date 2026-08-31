using AdminPortal.Domain.Entities;

namespace AdminPortal.Application.GoogleSheets;

public interface IGoogleSheetsSettings
{
    public string CredentialFilePath { get; }
    public string TokenStorePath { get; }
    public string AuthUser { get; }
    public string SpreadsheetId { get; }
}

public interface IGoogleSheetsService
{
    Task<GoogleSheetsCredentialSmokeResponse> SmokeTestCredentialAsync(CancellationToken cancellationToken);

    Task<SyncAssessmentsFromGoogleSheetsResponse> SyncAssessmentsAsync(SyncAssessmentsFromGoogleSheetsRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Lưu PDF kế hoạch đã render từ UI vào Google Drive folder của học sinh và trả về webViewLink.
    /// Flow này yêu cầu Student.DriveFolderId để tránh tạo file ngoài folder học viên.
    /// </summary>
    Task<string> UploadAssessmentSheetPlanPdfAsync(
        Guid assessmentSheetId, Guid studentId, string? existingFileLink, string fileName,
        byte[] content, CancellationToken cancellationToken);

    /// <summary>
    /// Lưu PDF kết quả đã render từ UI vào Google Drive folder của học sinh và trả về webViewLink.
    /// Flow này yêu cầu Student.DriveFolderId để tránh tạo file ngoài folder học viên.
    /// </summary>
    Task<string> UploadAssessmentSheetResultPdfAsync(
        Guid assessmentSheetId, Guid studentId, string? existingFileLink, string fileName,
        byte[] content, CancellationToken cancellationToken);

    /// <summary>
    /// Đồng bộ FinalGrade và FinalNote vào [F0.ĐG] theo đúng học sinh. Cột kết quả được dò bằng
    /// studentCode ở hàng định vị mã học sinh; cột FinalNote là cột ngay bên phải và phải để trống
    /// ở hàng định vị. Chỉ ghi những cell khác giá trị hiện tại và trả về danh sách cell thật sự được ghi.
    /// </summary>
    Task<IReadOnlyList<ResultSourceCellUpdate>> WriteFinalGradesToSourceSheetAsync(
        string studentCode,
        IReadOnlyList<AssessmentRecord> records,
        CancellationToken cancellationToken);

    /// <summary>
    /// Bản dry-run của <see cref="WriteFinalGradesToSourceSheetAsync"/>: đọc và đối chiếu [F0.ĐG] để
    /// lấy đúng tập cell sẽ thay đổi (kèm giá trị hiện tại) nhưng KHÔNG ghi. Dùng cho popup xác nhận.
    /// </summary>
    Task<IReadOnlyList<ResultSourceCellUpdate>> PreviewFinalGradesToSourceSheetAsync(
        string studentCode,
        IReadOnlyList<AssessmentRecord> records,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tải nội dung + tên gốc của một file PDF trên Google Drive theo webViewLink đã lưu (ví dụ
    /// <see cref="AssessmentSheet.PlanFileLinkPdf"/>). Dùng cho endpoint proxy tải/gộp PDF hàng loạt —
    /// link Drive không tải trực tiếp từ trình duyệt được (CORS).
    /// </summary>
    Task<DriveFileContent> DownloadAssessmentSheetPdfAsync(string fileLink, CancellationToken cancellationToken);
}

/// <summary>Nội dung một file tải từ Google Drive kèm tên hiển thị gốc.</summary>
public sealed record DriveFileContent(byte[] Content, string Name);
