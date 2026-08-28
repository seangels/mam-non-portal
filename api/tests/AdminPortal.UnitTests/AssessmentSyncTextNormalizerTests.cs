using AdminPortal.Application.GoogleSheets;

namespace AdminPortal.UnitTests;

public sealed class AssessmentSyncTextNormalizerTests
{
    [Theory]
    [InlineData("Nội dung dòng 1\r\nNội dung dòng 2", "Nội dung dòng 1\r\nNội dung dòng 2")]
    [InlineData("Nhóm 1\nNhóm 2", "Nhóm 1\nNhóm 2")]
    [InlineData("Nhóm\t\tphát triển   ngôn ngữ", "Nhóm\t\tphát triển   ngôn ngữ")]
    [InlineData("  Phát triển\r\n\r\nnhận thức  ", "Phát triển\r\n\r\nnhận thức")]
    public void NormalizeRequiredNameKeepsInnerContentAndOnlyTrimsEnds(string value, string expected) =>
        Assert.Equal(expected, AssessmentSyncTextNormalizer.NormalizeRequiredName(value));

    [Fact]
    public void NormalizeRequiredNameReturnsEmptyForBlank() =>
        Assert.Equal(string.Empty, AssessmentSyncTextNormalizer.NormalizeRequiredName("  \r\n\t "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n\t ")]
    public void NormalizeOptionalNameReturnsNullForBlankValues(string? value) =>
        Assert.Null(AssessmentSyncTextNormalizer.NormalizeOptionalName(value));

    [Fact]
    public void NormalizeOptionalNamePreservesNewlinesAndInnerWhitespace()
    {
        const string source = "  Phát triển\r\n\r\nnhận thức  ";

        var normalized = AssessmentSyncTextNormalizer.NormalizeOptionalName(source);

        Assert.Equal("Phát triển\r\n\r\nnhận thức", normalized);
        Assert.Contains('\n', normalized!);
    }
}
