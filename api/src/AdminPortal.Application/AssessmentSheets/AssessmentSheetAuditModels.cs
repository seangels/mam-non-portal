namespace AdminPortal.Application.AssessmentSheets;

public sealed record AssessmentSheetDeletionAuditSnapshot(
    string Status,
    Guid StudentId,
    int RecordCount,
    bool HasPlanPdf,
    bool HasResultPdf);
