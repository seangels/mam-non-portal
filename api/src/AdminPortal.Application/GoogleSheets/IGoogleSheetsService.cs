namespace AdminPortal.Application.GoogleSheets;

public interface IGoogleSheetsSettings
{
    public string CredentialFilePath { get; }
    public string SpreadsheetId { get; }
    public string AssessmentSheetTemplateFileId { get; }
}
public interface IGoogleSheetsService
{
    Task<SyncAssessmentsFromGoogleSheetsResponse> SyncAssessmentsAsync(SyncAssessmentsFromGoogleSheetsRequest request, CancellationToken cancellationToken);
    Task<AssessmentSheetGoogleActionResponse> ExportAssessmentSheetToSheetAsync(Guid assessmentSheetId, CancellationToken cancellationToken);
    Task<AssessmentSheetGoogleActionResponse> SyncAssessmentSheetToSheetAsync(Guid assessmentSheetId, CancellationToken cancellationToken);
    Task<AssessmentSheetGoogleActionResponse> SubmitAssessmentSheetResultsAsync(Guid assessmentSheetId, CancellationToken cancellationToken);
    Task<AssessmentSheetGoogleActionResponse> GenerateAssessmentSheetPlanPdfAsync(Guid assessmentSheetId, CancellationToken cancellationToken);
    Task<AssessmentSheetGoogleActionResponse> GenerateAssessmentSheetResultPdfAsync(Guid assessmentSheetId, CancellationToken cancellationToken);
}
