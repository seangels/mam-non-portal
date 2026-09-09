namespace AdminPortal.Application.GoogleSheets;

public enum DrivePdfSaveOperation
{
    Create,
    Update
}

public sealed record DrivePdfSaveTarget(
    DrivePdfSaveOperation Operation,
    string? ExistingFileId);

/// <summary>
/// Resolves whether a PDF upload should create a new Drive file or update the file referenced by the saved link.
/// Kept free of Google API calls so the write decision can be verified without live Drive credentials.
/// </summary>
public static class DrivePdfSaveRules
{
    public static DrivePdfSaveTarget Resolve(string? existingFileLink)
    {
        var existingFileId = ExtractFileId(existingFileLink);
        return existingFileId is null
            ? new DrivePdfSaveTarget(DrivePdfSaveOperation.Create, null)
            : new DrivePdfSaveTarget(DrivePdfSaveOperation.Update, existingFileId);
    }

    public static string? ExtractFileId(string? webViewLink)
    {
        if (string.IsNullOrWhiteSpace(webViewLink))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(webViewLink, "/d/([a-zA-Z0-9_-]+)");
        return match.Success ? match.Groups[1].Value : null;
    }
}
