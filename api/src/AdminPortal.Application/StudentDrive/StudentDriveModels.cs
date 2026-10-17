using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using System.ComponentModel.DataAnnotations;

namespace AdminPortal.Application.StudentDrive;

public sealed record DriveFolderInfo(
    string Id,
    string Name,
    string WebViewLink,
    IReadOnlyList<string> ParentIds);

public sealed record StudentDriveMutation(
    DriveFolderInfo Folder,
    bool Created,
    bool Reused,
    bool Moved,
    IReadOnlyList<string> PreviousParentIds);

public sealed record StudentDriveFolderActionResponse(
    Guid StudentId,
    string DriveFolderId,
    string FolderName,
    string WebViewLink);

public sealed record StudentDriveReconcileError(
    Guid StudentId,
    string StudentCode,
    string FullName,
    string Code,
    string Message);

public sealed record StudentDriveReconcileResponse(
    int Processed,
    int Created,
    int Reused,
    int Moved,
    int Unchanged,
    int Failed,
    IReadOnlyList<StudentDriveReconcileError> Errors);

public sealed record RenameStudentDriveFolderRequest(
    [param: Range(1, int.MaxValue)] int ExpectedVersion);

public static class StudentDriveFolderRules
{
    public static string BuildFolderName(string studentCode, string fullName) =>
        $"{studentCode.Trim().ToUpperInvariant()} - {fullName.Trim()}";

    public static string? NormalizeFolderId(string? value, string fieldName = "driveFolderId")
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var input = value.Trim();
        var folderId = Uri.TryCreate(input, UriKind.Absolute, out var uri)
            ? ExtractFolderId(uri)
            : input;
        if (string.IsNullOrWhiteSpace(folderId) || folderId.Length > 200 ||
            folderId.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
        {
            const string message = "Drive Folder ID không hợp lệ. Hãy nhập ID thư mục hoặc link Google Drive có dạng /folders/{id} hay ?id={id}.";
            throw new AppValidationException(message, new Dictionary<string, string[]> { [fieldName] = [message] });
        }

        return folderId;
    }

    private static string? ExtractFolderId(Uri uri)
    {
        if (uri.Scheme is not ("http" or "https") ||
            !uri.Host.Equals("drive.google.com", StringComparison.OrdinalIgnoreCase)) return null;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (segments[index].Equals("folders", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(segments[index + 1]);
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            var key = separatorIndex < 0 ? pair : pair[..separatorIndex];
            if (!Uri.UnescapeDataString(key).Equals("id", StringComparison.OrdinalIgnoreCase)) continue;
            var encodedValue = separatorIndex < 0 ? string.Empty : pair[(separatorIndex + 1)..];
            return Uri.UnescapeDataString(encodedValue.Replace('+', ' '));
        }

        return null;
    }
}
