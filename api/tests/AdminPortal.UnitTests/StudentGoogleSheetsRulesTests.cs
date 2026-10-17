using AdminPortal.Application.GoogleSheets;

namespace AdminPortal.UnitTests;

public sealed class StudentGoogleSheetsRulesTests
{
    [Fact]
    public void FindUniqueRowUsesStudentIdColumnBCaseInsensitively()
    {
        IReadOnlyList<IList<object>> rows =
        [
            new List<object> { "S10", "Bé Một" },
            new List<object>(),
            new List<object> { "s24", "Bé Hai" }
        ];

        Assert.Equal(4, StudentGoogleSheetsRules.FindUniqueRow(rows, "S24"));
        Assert.Null(StudentGoogleSheetsRules.FindUniqueRow(rows, "S99"));
    }

    [Fact]
    public void FindUniqueRowRejectsDuplicateStudentIds()
    {
        IReadOnlyList<IList<object>> rows =
        [
            new List<object> { "S10" },
            new List<object> { "s10" }
        ];

        Assert.Throws<InvalidOperationException>(() => StudentGoogleSheetsRules.FindUniqueRow(rows, "S10"));
    }

    [Fact]
    public void FindFirstAvailableRowReusesFirstBlankStudentId()
    {
        IReadOnlyList<IList<object>> rows =
        [
            new List<object> { "S10" },
            new List<object>(),
            new List<object> { "S24" }
        ];

        Assert.Equal(3, StudentGoogleSheetsRules.FindFirstAvailableRow(rows));
    }

    [Theory]
    [InlineData(1899, 12, 30, 0)]
    [InlineData(2026, 10, 17, 46312)]
    public void ToGoogleDateSerialUsesSheetsCompatibleEpoch(int year, int month, int day, double expected)
    {
        Assert.Equal(expected, StudentGoogleSheetsRules.ToGoogleDateSerial(new DateOnly(year, month, day)));
    }

    [Fact]
    public void ResolveStableStudentIdKeepsOriginalSheetKeyWhenStudentCodeChanges()
    {
        Assert.Equal("S10", StudentGoogleSheetsRules.ResolveStableStudentId("S10", "S99"));
        Assert.Equal("S99", StudentGoogleSheetsRules.ResolveStableStudentId(null, "S99"));
    }

    [Fact]
    public void WritableColumnsNeverIncludeFormulaColumns()
    {
        Assert.Equal(["B", "C", "D", "H"], StudentGoogleSheetsRules.CreateColumns);
        Assert.Equal(["C", "D", "H"], StudentGoogleSheetsRules.UpdateColumns);
        Assert.DoesNotContain("F", StudentGoogleSheetsRules.CreateColumns);
        Assert.DoesNotContain("G", StudentGoogleSheetsRules.CreateColumns);
    }
}
