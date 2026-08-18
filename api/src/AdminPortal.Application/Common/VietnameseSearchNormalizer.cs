using System.Globalization;
using System.Text;
namespace AdminPortal.Application.Common;
public static class VietnameseSearchNormalizer
{
    public static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasWhitespace = true;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            var normalized = character is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(character);
            if (char.IsWhiteSpace(normalized))
            {
                if (!previousWasWhitespace) builder.Append(' ');
                previousWasWhitespace = true;
                continue;
            }

            builder.Append(normalized);
            previousWasWhitespace = false;
        }

        return builder.ToString().TrimEnd();
    }

    public static string Digits(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsDigit(character)) builder.Append(character);
        }

        return builder.ToString();
    }
}
