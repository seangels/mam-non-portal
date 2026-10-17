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

    public static IReadOnlyList<string> FindAvailableStudentCodes(IReadOnlyList<IList<object>> rows)
    {
        EnsureUniqueStudentIds(rows);
        var available = new List<string>();
        foreach (var row in rows)
        {
            var studentCode = CellText(row, 0);
            if (string.IsNullOrWhiteSpace(studentCode)) continue;
            if (string.IsNullOrWhiteSpace(CellText(row, 1))) available.Add(studentCode);
        }

        return available;
    }

    public static bool HasBlankFullName(IReadOnlyList<IList<object>> rows, int rowNumber)
    {
        var index = rowNumber - FirstDataRow;
        return index >= 0 && index < rows.Count && string.IsNullOrWhiteSpace(CellText(rows[index], 1));
    }

    public static StudentSheetCreateTarget ResolveCreateTarget(
        IReadOnlyList<IList<object>> rows,
        string studentCode)
    {
        EnsureUniqueStudentIds(rows);
        var existingRow = FindUniqueRow(rows, studentCode);
        if (existingRow is null)
            return new StudentSheetCreateTarget(FindFirstAvailableRow(rows), StudentSheetCreateDisposition.NewRow);
        return new StudentSheetCreateTarget(
            existingRow.Value,
            HasBlankFullName(rows, existingRow.Value)
                ? StudentSheetCreateDisposition.AvailableExistingRow
                : StudentSheetCreateDisposition.UnavailableExistingRow);
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

    private static string? CellText(IList<object> row, int index) =>
        row.Count > index ? row[index]?.ToString()?.Trim() : null;

    private static void EnsureUniqueStudentIds(IReadOnlyList<IList<object>> rows)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var studentCode = CellText(row, 0);
            if (string.IsNullOrWhiteSpace(studentCode)) continue;
            if (!seen.Add(studentCode))
                throw new InvalidOperationException($"StudentID '{studentCode}' xuất hiện nhiều hơn một lần trong Google Sheets.");
        }
    }
}

public enum StudentSheetCreateDisposition
{
    NewRow,
    AvailableExistingRow,
    UnavailableExistingRow
}

public sealed record StudentSheetCreateTarget(int RowNumber, StudentSheetCreateDisposition Disposition);
