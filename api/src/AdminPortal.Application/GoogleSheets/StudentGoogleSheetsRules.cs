namespace AdminPortal.Application.GoogleSheets;

public static class StudentGoogleSheetsRules
{
    public const int FirstDataRow = 2;
    public static readonly IReadOnlyList<string> CreateColumns = ["B", "C", "D", "H"];
    public static readonly IReadOnlyList<string> UpdateColumns = ["C", "D", "H"];

    public static int? FindUniqueRow(IReadOnlyList<IList<object>> rows, string studentCode)
    {
        var matches = rows
            .Select((row, index) => new { Row = row, RowNumber = FirstDataRow + index })
            .Where(x => x.Row.Count > 0 &&
                string.Equals(x.Row[0]?.ToString()?.Trim(), studentCode, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.RowNumber)
            .ToList();

        return matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException($"StudentID '{studentCode}' xuất hiện nhiều hơn một lần trong Google Sheets.")
        };
    }

    public static int FindFirstAvailableRow(IReadOnlyList<IList<object>> rows)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].Count == 0 || string.IsNullOrWhiteSpace(rows[index][0]?.ToString()))
                return FirstDataRow + index;
        }

        return FirstDataRow + rows.Count;
    }

    public static double ToGoogleDateSerial(DateOnly value) =>
        value.ToDateTime(TimeOnly.MinValue).ToOADate();

    public static string ResolveStableStudentId(string? persistedSheetStudentId, string currentStudentCode) =>
        string.IsNullOrWhiteSpace(persistedSheetStudentId)
            ? currentStudentCode
            : persistedSheetStudentId;
}
