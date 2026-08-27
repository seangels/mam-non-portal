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

public sealed record SyncAssessmentsFromGoogleSheetsRequest();

public sealed record SyncAssessmentsFromGoogleSheetsResponse(
    int SheetsTotalRows,
    int DatabaseTotalRows,
    int InsertedRows,
    int UpdatedRows,
    int DeletedRows
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
