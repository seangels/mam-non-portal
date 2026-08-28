namespace AdminPortal.Application.GoogleSheets;

using AdminPortal.Domain.Enums;

public sealed record AssessmentGoogleSheetResponse(
    string? ItemId,
    string? Item,
    string? NhomTuoi,
    string? GroupLv2,
    string? GroupLv3,
    string? RowIndex
);
public sealed record AssessmentLastResultGoogleSheetResponse(
    string? ItemId,
    string? MaHs,
    string? KetQua,
    string? NhomTuoi,
    string? GroupLv2,
    string? GroupLv3,
    string? Item,
    string? RowIndex,
    string? TenHs,
    string? GhiChu
);

public sealed record SyncAssessmentsFromGoogleSheetsRequest(
    AssessmentRecordSnapshotReplacement? ReplaceRecordSnapshots = null);

/// <summary>
/// Tùy chọn 2 của popup đồng bộ: sau khi rebuild catalog, ghi đè các trường đã chọn từ
/// <see cref="AdminPortal.Domain.Entities.Assessment"/> mới vào snapshot đông cứng trên
/// <see cref="AdminPortal.Domain.Entities.AssessmentRecord"/>, khớp theo mã và giới hạn theo trạng thái bảng.
/// Null = giữ hành vi mặc định (tùy chọn 1), không đụng snapshot bản ghi.
/// </summary>
public sealed record AssessmentRecordSnapshotReplacement(
    bool Name = false,
    bool GroupLv1Name = false,
    bool GroupLv2Name = false,
    bool GroupLv3Name = false,
    bool RowIndex = false,
    IReadOnlyList<AssessmentSheetStatus>? SheetStatuses = null)
{
    public bool HasAnyField => Name || GroupLv1Name || GroupLv2Name || GroupLv3Name || RowIndex;
}

public sealed record SyncAssessmentsFromGoogleSheetsResponse(
    int SheetsTotalRows,
    int DatabaseTotalRows,
    int InsertedRows,
    int UpdatedRows,
    int DeletedRows,
    int ReplacedRecordSnapshots
    );

public sealed record GoogleSheetsCredentialSmokeResponse(
    bool Success,
    bool IsConfigured,
    string? SpreadsheetId,
    string? SpreadsheetTitle,
    string? FirstSheetTitle,
    string? ReadRange,
    int? ReadRowCount,
    string? ErrorCode
);

public sealed record ResultSourceCellUpdate(
    string SpreadsheetId,
    string SheetName,
    string Cell,
    int Row,
    string Column,
    string Kind,
    string? CurrentValue,
    string NewValue,
    string StudentCode,
    string AssessmentCode,
    string AssessmentName,
    AssessmentGrade? FinalGrade,
    string? FinalGradeLabel,
    string? FinalNote
);
