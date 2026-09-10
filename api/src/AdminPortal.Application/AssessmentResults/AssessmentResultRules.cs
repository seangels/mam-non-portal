using System.Security.Cryptography;
using System.Text;

namespace AdminPortal.Application.AssessmentResults;

public static class AssessmentResultRules
{
    public static string? NormalizeNote(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string CreateVersion(
        Guid assessmentId,
        string gradeCell,
        string noteCell,
        string? rawGrade,
        string? rawNote)
    {
        var payload = string.Join(
            '\u001f',
            assessmentId.ToString("N"),
            gradeCell,
            noteCell,
            rawGrade ?? string.Empty,
            rawNote ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
