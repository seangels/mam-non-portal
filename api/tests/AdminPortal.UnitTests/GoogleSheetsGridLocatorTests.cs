using AdminPortal.Application.GoogleSheets;

namespace AdminPortal.UnitTests;

public sealed class GoogleSheetsGridLocatorTests
{
    [Theory]
    [InlineData(0, "A")]
    [InlineData(7, "H")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(27, "AB")]
    [InlineData(51, "AZ")]
    [InlineData(52, "BA")]
    [InlineData(701, "ZZ")]
    [InlineData(702, "AAA")]
    public void ColumnIndexToLetterMatchesSpreadsheetColumnNaming(int index, string expected) =>
        Assert.Equal(expected, GoogleSheetsGridLocator.ColumnIndexToLetter(index));

    [Fact]
    public void ColumnIndexToLetterRejectsNegativeIndex() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => GoogleSheetsGridLocator.ColumnIndexToLetter(-1));

    [Fact]
    public void FindAbsoluteRowMatchesCodeAndAddsFirstRowOffset()
    {
        var columnValues = new List<string?> { "A01", "A02", "A03" };
        Assert.Equal(16, GoogleSheetsGridLocator.FindAbsoluteRow(columnValues, "A01", firstRowNumber: 16));
        Assert.Equal(18, GoogleSheetsGridLocator.FindAbsoluteRow(columnValues, "A03", firstRowNumber: 16));
    }

    [Fact]
    public void FindAbsoluteRowReturnsNullWhenCodeMissing()
    {
        var columnValues = new List<string?> { "A01", "A02" };
        Assert.Null(GoogleSheetsGridLocator.FindAbsoluteRow(columnValues, "A99", firstRowNumber: 16));
    }

    [Fact]
    public void FindAbsoluteRowTrimsWhitespaceBeforeComparing()
    {
        var columnValues = new List<string?> { " A01 " };
        Assert.Equal(16, GoogleSheetsGridLocator.FindAbsoluteRow(columnValues, "A01", firstRowNumber: 16));
    }

    [Fact]
    public void FindAbsoluteColumnIndexMatchesCodeAndAddsFirstColumnOffset()
    {
        var rowValues = new List<string?> { "HS-001", "HS-002", "HS-003" };
        Assert.Equal(7, GoogleSheetsGridLocator.FindAbsoluteColumnIndex(rowValues, "HS-001", firstColumnIndex: 7));
        Assert.Equal(9, GoogleSheetsGridLocator.FindAbsoluteColumnIndex(rowValues, "HS-003", firstColumnIndex: 7));
    }

    [Fact]
    public void FindAbsoluteColumnIndexReturnsNullWhenCodeMissing()
    {
        var rowValues = new List<string?> { "HS-001", "HS-002" };
        Assert.Null(GoogleSheetsGridLocator.FindAbsoluteColumnIndex(rowValues, "HS-999", firstColumnIndex: 7));
    }
}
