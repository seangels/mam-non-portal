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

    [Fact]
    public void FindAvailableStudentCodesUsesBlankFullNameAndPreservesSheetOrder()
    {
        IReadOnlyList<IList<object>> rows =
        [
            new List<object> { "S10", "" },
            new List<object> { "S11", "Đã có tên" },
            new List<object> { "S12", "   " },
            new List<object> { "", "Không có mã" }
        ];

        Assert.Equal(["S10", "S12"], StudentGoogleSheetsRules.FindAvailableStudentCodes(rows));
        Assert.True(StudentGoogleSheetsRules.HasBlankFullName(rows, 2));
        Assert.False(StudentGoogleSheetsRules.HasBlankFullName(rows, 3));
    }

    [Fact]
    public void FindAvailableStudentCodesRejectsDuplicateStudentIdsEvenWhenOneIsUnavailable()
    {
        IReadOnlyList<IList<object>> rows =
        [
            new List<object> { "S10", "" },
            new List<object> { "s10", "Đã có tên" }
        ];

        Assert.Throws<InvalidOperationException>(() => StudentGoogleSheetsRules.FindAvailableStudentCodes(rows));
    }

    [Fact]
    public void ResolveCreateTargetDistinguishesAvailableUnavailableAndNewRows()
    {
        IReadOnlyList<IList<object>> rows =
        [
            new List<object> { "S10", "" },
            new List<object> { "S11", "Đã có tên" },
            new List<object>()
        ];

        Assert.Equal(
            new StudentSheetCreateTarget(2, StudentSheetCreateDisposition.AvailableExistingRow),
            StudentGoogleSheetsRules.ResolveCreateTarget(rows, "S10"));
        Assert.Equal(
            new StudentSheetCreateTarget(3, StudentSheetCreateDisposition.UnavailableExistingRow),
            StudentGoogleSheetsRules.ResolveCreateTarget(rows, "S11"));
        Assert.Equal(
            new StudentSheetCreateTarget(4, StudentSheetCreateDisposition.NewRow),
            StudentGoogleSheetsRules.ResolveCreateTarget(rows, "S12"));
    }

    [Fact]
    public void ResolveCreateTargetRejectsUnrelatedDuplicateStudentIdsAsStructuralError()
    {
        IReadOnlyList<IList<object>> rows =
        [
            new List<object> { "S10", "" },
            new List<object> { "S10", "Đã có tên" }
        ];

        Assert.Throws<InvalidOperationException>(() => StudentGoogleSheetsRules.ResolveCreateTarget(rows, "S99"));
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
