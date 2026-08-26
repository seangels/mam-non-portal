using AdminPortal.Domain.Entities;

namespace AdminPortal.Application.GoogleSheets;

public interface IGoogleSheetsSettings
{
    public string CredentialFilePath { get; }
    public string TokenStorePath { get; }
    public string AuthUser { get; }
    public string SpreadsheetId { get; }
    public string AssessmentSheetTemplateFileId { get; }

    // 3 sheet cố định trong file mẫu gen_assessment_sheet / mọi file [F01] copy ra (requirements 09 mục 3).
    public string DataSheetName { get; }
    public string PlanTemplateSheetName { get; }
    public long PlanTemplateSheetGid { get; }
    public string ResultTemplateSheetName { get; }
    public long ResultTemplateSheetGid { get; }

}

public interface IGoogleSheetsService
{
    Task<SyncAssessmentsFromGoogleSheetsResponse> SyncAssessmentsAsync(SyncAssessmentsFromGoogleSheetsRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Lazy Drive file-copy của file mẫu gen_assessment_sheet thành file [F01] riêng cho AssessmentSheet
    /// này nếu chưa có. Set và lưu AssessmentSheetSpreadsheetId ngay trong hàm này (SaveChangesAsync) để
    /// tránh copy trùng nếu request khác chen vào. Trả về id file [F01] (mới hoặc đã có sẵn).
    /// </summary>
    Task<string> EnsureAssessmentSheetSpreadsheetAsync(AssessmentSheet sheet, CancellationToken cancellationToken);

    /// <summary>Ghi đè toàn bộ dữ liệu Plan*/Final* hiện tại vào sheet data (gid=0) của [F01].</summary>
    Task WriteAssessmentSheetDataAsync(string spreadsheetId, IReadOnlyList<AssessmentRecord> records, CancellationToken cancellationToken);

    /// <summary>
    /// Ghi Plan*/PlanNote vào sheet khcn_template rồi export sang PDF, lưu vào thư mục Drive riêng của học sinh
    /// (lazy tạo nếu chưa có). Nếu existingFileLink đã trỏ tới một file PDF cũ, ghi đè lên đúng file đó thay vì
    /// tạo file mới. Trả về link file PDF đã lưu.
    /// </summary>
    Task<string> GenerateAssessmentSheetPlanPdfAsync(
        string spreadsheetId, Guid assessmentSheetId, Guid studentId, string? existingFileLink,
        IReadOnlyList<AssessmentRecord> records, CancellationToken cancellationToken);

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

    /// <summary>Như trên nhưng ghi Final*/FinalNote vào sheet KQ_template.</summary>
    Task<string> GenerateAssessmentSheetResultPdfAsync(
        string spreadsheetId, Guid assessmentSheetId, Guid studentId, string? existingFileLink,
        IReadOnlyList<AssessmentRecord> records, CancellationToken cancellationToken);

    /// <summary>
    /// Ghi nhãn của FinalGrade (bỏ qua record có FinalGrade null) vào [F0.ĐG] theo đúng học sinh,
    /// dò vị trí ô qua cột E16:E (mã mục) và hàng H16:16 (mã học sinh).
    /// </summary>
    Task WriteFinalGradesToSourceSheetAsync(string studentCode, IReadOnlyList<AssessmentRecord> records, CancellationToken cancellationToken);
}
