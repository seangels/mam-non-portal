using AdminPortal.Application.Common;
using AdminPortal.Application.Teachers;

namespace AdminPortal.UnitTests;

public sealed class VietnameseSearchNormalizerTests
{
    [Theory]
    [InlineData("Nguyễn", "nguyen")]
    [InlineData("Hoàng", "hoang")]
    [InlineData("Đặng", "dang")]
    [InlineData("  NGUYỄN \t  THỊ  ", "nguyen thi")]
    [InlineData("hoa\u0300ng", "hoang")]
    [InlineData("%_", "%_")]
    public void FoldNormalizesVietnameseCaseWhitespaceAndKeepsLiteralCharacters(
        string input,
        string expected)
    {
        Assert.Equal(expected, VietnameseSearchNormalizer.Fold(input));
    }

    [Theory]
    [InlineData("090 123-4567", "0901234567")]
    [InlineData("không có số", "")]
    [InlineData(null, "")]
    public void DigitsReturnsOnlyDigits(string? input, string expected)
    {
        Assert.Equal(expected, VietnameseSearchNormalizer.Digits(input));
    }
}
