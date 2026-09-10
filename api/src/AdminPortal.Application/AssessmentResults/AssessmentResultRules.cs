using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;

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

    public static string CreateDatabaseVersion(
        Assessment assessment,
        Guid? recordId,
        AssessmentGrade? grade,
        string? note,
        DateTimeOffset? recordUpdatedAt)
    {
        var payload = string.Join(
            '\u001f',
            assessment.Id.ToString("N"),
            assessment.Code,
            assessment.Name,
            assessment.GroupLv1Name ?? string.Empty,
            assessment.GroupLv2Name ?? string.Empty,
            assessment.GroupLv3Name ?? string.Empty,
            assessment.RowIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            assessment.UpdatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            recordId?.ToString("N") ?? string.Empty,
            recordUpdatedAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            grade?.ToString() ?? string.Empty,
            note ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
