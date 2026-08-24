namespace AdminPortal.Application.GoogleSheets;

public static class GoogleSheetsGridLocator
{
    public static string ColumnIndexToLetter(int zeroBasedIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(zeroBasedIndex);

        var index = zeroBasedIndex;
        var letters = string.Empty;
        do
        {
            letters = (char)('A' + index % 26) + letters;
            index = index / 26 - 1;
        } while (index >= 0);

        return letters;
    }

    public static int? FindAbsoluteRow(IReadOnlyList<string?> columnValuesFromFirstRow, string code, int firstRowNumber)
    {
        for (var i = 0; i < columnValuesFromFirstRow.Count; i++)
        {
            if (string.Equals(columnValuesFromFirstRow[i]?.Trim(), code, StringComparison.Ordinal))
                return firstRowNumber + i;
        }

        return null;
    }

    public static int? FindAbsoluteColumnIndex(IReadOnlyList<string?> rowValuesFromFirstColumn, string code, int firstColumnIndex)
    {
        for (var i = 0; i < rowValuesFromFirstColumn.Count; i++)
        {
            if (string.Equals(rowValuesFromFirstColumn[i]?.Trim(), code, StringComparison.Ordinal))
                return firstColumnIndex + i;
        }

        return null;
    }
}
