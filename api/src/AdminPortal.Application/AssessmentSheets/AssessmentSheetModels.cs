using System.ComponentModel.DataAnnotations;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.AssessmentSheets;

public sealed class AssessmentSheetListQuery
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(200)] public string? Search { get; init; }
    public Guid? StudentId { get; init; }
    public Guid? ResponsibleTeacherId { get; init; }
    public DateTimeOffset? DateFrom {get; init; }
    public DateTimeOffset? DateTo {get; init; }
    public AssessmentSheetStatus? Status { get; init; }
    [MaxLength(40)] public string SortBy { get; init; } = "updatedAt";
    [RegularExpression("(?i)^(asc|desc)$")] public string SortOrder { get; init; } = "desc";
}


public sealed record AssessmentSheetListItemResponse(
    Guid Id,
    AssessmentSheetStatus Status,
    Guid StudentId,
    string? StudentCode,
    string? StudentFullName,
    Guid? ResponsibleTeacherId,
    string? ResponsibleTeacherFullName,
    DateTimeOffset? StartDate,
    DateTimeOffset? DueDate,
    DateTimeOffset? DoneDate,
    DateTimeOffset? SubmissionDate,
    string? PlanFileLinkPdf,
    string? ResultFileLinkPdf,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AssessmentSheetDetailResponse(
    Guid Id,
    AssessmentSheetStatus Status,
    Guid StudentId,
    AssessmentSheetStudentSnapshotResponse StudentSnapshot,
    Guid? ResponsibleTeacherId,
    string? ResponsibleTeacherFullName,
    string? Note,
    DateTimeOffset? StartDate,
    DateTimeOffset? DueDate,
    DateTimeOffset? DoneDate,
    DateTimeOffset? SubmissionDate,
    string? Feedback,
    string? PlanFileLinkPdf,
    string? ResultFileLinkPdf,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AssessmentSheetRecordResponse> Records);

public sealed record AssessmentSheetStudentSnapshotResponse(
    string? StudentCode,
    string? FullName,
    string? NickName,
    DateOnly? DateOfBirth,
    Gender? Gender);

public sealed record AssessmentSheetRecordResponse(
    Guid Id,
    Guid AssessmentSheetId,
    int? AssessmentRowIndex,
    int? DisplayOrder,
    AssessmentSnapshotResponse Assessment,
    AssessmentGrade? PlanGrade,
    string? PlanNote,
    AssessmentGrade? FinalGrade,
    string? FinalNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AssessmentSnapshotResponse(
    string Code,
    string Name,
    string? GroupLv1Name,
    string? GroupLv2Name,
    string? GroupLv3Name,
    int? RowIndex);

public sealed record AssessmentPlanCandidateResponse(
    Guid Id,
    string Code,
    string Name,
    string? GroupLv1Name,
    string? GroupLv2Name,
    string? GroupLv3Name,
    int? RowIndex,
    AssessmentGrade? LatestGrade);

public sealed record CreateAssessmentSheetRequest(
    Guid StudentId,
    Guid? ResponsibleTeacherId,
    [param: MaxLength(2000)] string? Note,
    DateTimeOffset? StartDate,
    DateTimeOffset? DueDate,
    // Cho phép rỗng (`[]`) — tạo bảng đánh giá không có mục nào, thêm sau ở màn edit (ASH-FB-W1 / G1).
    [param: Required, MaxLength(5000)] IReadOnlyList<CreateAssessmentSheetRecordRequest> Records);

public sealed record CreateAssessmentSheetRecordRequest(
    Guid AssessmentId,
    AssessmentGrade? LatestGrade,
    [param: MaxLength(2000)] string? Note);

public sealed record UpdateAssessmentSheetRequest(
    Guid? ResponsibleTeacherId,
    [param: MaxLength(2000)] string? Note,
    DateTimeOffset? StartDate,
    DateTimeOffset? DueDate,
    [param: MaxLength(2000)] string? Feedback,
    [param: MaxLength(2000)] string? PlanFileLinkPdf,
    [param: MaxLength(2000)] string? ResultFileLinkPdf);

public sealed record ReplaceAssessmentSheetRecordsRequest(
    // Cho phép rỗng (`[]`) — xóa mục cuối cùng, bảng đánh giá được phép rỗng (ASH-FB-W1 / G1+G2).
    [param: Required, MaxLength(5000)] IReadOnlyList<AssessmentSheetRecordRequest> Records);

public sealed record AssessmentSheetRecordRequest(
    Guid AssessmentId,
    AssessmentGrade? PlanGrade,
    [param: MaxLength(2000)] string? PlanNote,
    AssessmentGrade? FinalGrade,
    [param: MaxLength(2000)] string? FinalNote,
    [param: Range(0, int.MaxValue)] int? DisplayOrder = null,
    [param: MaxLength(500)] string? GroupLv2Name = null,
    [param: MaxLength(500)] string? GroupLv3Name = null);

public sealed record UpdateAssessmentSheetStatusRequest(AssessmentSheetStatus Status);

public sealed record ImportAssessmentSheetsPreviewResponse(
    ImportAssessmentSheetsPreviewSummaryResponse Summary,
    IReadOnlyList<ImportAssessmentSheetsPreviewRowResponse> Rows);

public sealed record ImportAssessmentSheetsPreviewSummaryResponse(
    bool CanImport,
    int ValidRowCount,
    int ErrorCount,
    int WarningCount,
    int SkippedDuplicateRowCount,
    int Groups);

public sealed record ImportAssessmentSheetsPreviewRowResponse(
    int RowNumber,
    string? AssessmentCode,
    string? StudentCode,
    string? StudentName,
    DateTimeOffset? StartDate,
    DateTimeOffset? DueDate,
    string? PlanGrade,
    string? PlanNote,
    int? Stt,
    string? GroupLv2Name,
    string? GroupLv3Name,
    string? NormalizedAssessmentCode,
    string? NormalizedStudentCode,
    string? DisplayStartDate,
    string? DisplayDueDate,
    string Action,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public sealed record ImportAssessmentSheetsResponse(
    int CreatedSheetCount,
    int UpdatedSheetCount,
    int ImportedRecordCount,
    int SkippedDuplicateRowCount,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ImportedAssessmentSheetResponse> Sheets);

public sealed record ImportedAssessmentSheetResponse(
    Guid Id,
    string StudentCode,
    string StudentName,
    DateTimeOffset StartDate,
    DateTimeOffset DueDate,
    string Action,
    int RecordCount);

// Kết quả dry-run cho popup xác nhận trước khi submit kết quả vào [F0.ĐG].
// GradeSummary luôn xếp: Đạt +, Hỗ trợ +, Hỗ trợ -, Chưa đạt -, rồi "Chưa có kết quả" (Grade = null).
public sealed record SubmitResultsPreviewResponse(
    IReadOnlyList<SubmitResultsGradeStat> GradeSummary,
    int TotalRecords,
    int TotalChangedCells,
    IReadOnlyList<SubmitResultsCellChange> Changes);

public sealed record SubmitResultsGradeStat(
    AssessmentGrade? Grade,
    string Label,
    int Count);

public sealed record SubmitResultsCellChange(
    string Cell,
    string Kind,
    string AssessmentCode,
    string AssessmentName,
    string? CurrentValue,
    string NewValue);

// Bulk Action "Tải PDF/ảnh" trên màn danh sách: chọn nhiều dòng rồi tải/gộp hoàn toàn ở backend.
public enum AssessmentSheetPdfKind
{
    Plan,
    Result
}

public enum AssessmentSheetPdfArchiveFormat
{
    // Zip các file PDF gốc, mỗi bảng đánh giá một file.
    Pdf,
    // Zip ảnh PNG: mỗi bảng đánh giá một thư mục, mỗi trang PDF một file PNG.
    Images
}

public sealed record AssessmentSheetPdfArchiveRequest(
    [param: Required, MinLength(1), MaxLength(500)] IReadOnlyList<Guid> Ids,
    AssessmentSheetPdfKind Kind,
    AssessmentSheetPdfArchiveFormat Format);

public sealed record AssessmentSheetPdfArchiveResult(byte[] Content, string FileName);
