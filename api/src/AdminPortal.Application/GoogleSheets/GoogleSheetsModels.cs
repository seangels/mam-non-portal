namespace AdminPortal.Application.GoogleSheets;

public sealed record AssessmentGoogleSheetResponse(
    string ItemId,
    string Item,
    string NhomTuoi,
    string GroupLv2,
    string GroupLv3,
    string RowIndex
);
public sealed record AssessmentLastResultGoogleSheetResponse(
    string ItemId,
    string MaHs,
    string KetQua,
    string NhomTuoi,
    string GroupLv2,
    string GroupLv3,
    string Item,
    string RowIndex,
    string TenHs,
    string GhiChu
);

public sealed record SyncAssessmentsFromGoogleSheetsRequest();

public sealed record SyncAssessmentsFromGoogleSheetsResponse(
    int SheetsTotalRows,
    int DatabaseTotalRows,
    int InsertedRows,
    int UpdatedRows,
    int DeletedRows
    );