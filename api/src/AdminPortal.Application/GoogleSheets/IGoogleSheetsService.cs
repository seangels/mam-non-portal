namespace AdminPortal.Application.GoogleSheets;

public interface IGoogleSheetsSettings
{
    public string CredentialFilePath { get; }
    public string SpreadsheetId { get; }
}
public interface IGoogleSheetsService
{
    Task<SyncAssessmentsFromGoogleSheetsResponse> SyncAssessmentsAsync(SyncAssessmentsFromGoogleSheetsRequest request, CancellationToken cancellationToken);
}