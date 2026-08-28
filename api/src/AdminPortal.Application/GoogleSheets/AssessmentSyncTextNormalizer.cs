using System.Text;

namespace AdminPortal.Application.GoogleSheets;

public static class AssessmentSyncTextNormalizer
{
    public static string NormalizeRequiredName(string? value) =>
        NormalizeOptionalName(value) ?? string.Empty;

    public static string? NormalizeOptionalName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = new StringBuilder(value.Length);
        var hasPendingSeparator = false;

        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                hasPendingSeparator = normalized.Length > 0;
                continue;
            }

            if (hasPendingSeparator)
            {
                normalized.Append(' ');
                hasPendingSeparator = false;
            }

            normalized.Append(character);
        }

        return normalized.ToString();
    }
}
