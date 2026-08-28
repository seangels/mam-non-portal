using AdminPortal.Application.GoogleSheets;

namespace AdminPortal.UnitTests;

public sealed class AssessmentSyncTextNormalizerTests
{
    [Theory]
    [InlineData("Nội dung dòng 1\r\nNội dung dòng 2", "Nội dung dòng 1 Nội dung dòng 2")]
    [InlineData("Nhóm 1\nNhóm 2", "Nhóm 1 Nhóm 2")]
    [InlineData("Nhóm 1\rNhóm 2", "Nhóm 1 Nhóm 2")]
    [InlineData("  Nhóm\t\tphát triển   ngôn ngữ  ", "Nhóm phát triển ngôn ngữ")]
    public void NormalizeRequiredNameCollapsesNewlinesAndWhitespace(string value, string expected) =>
        Assert.Equal(expected, AssessmentSyncTextNormalizer.NormalizeRequiredName(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n\t ")]
    public void NormalizeOptionalNameReturnsNullForBlankValues(string? value) =>
        Assert.Null(AssessmentSyncTextNormalizer.NormalizeOptionalName(value));

    [Fact]
    public void NormalizeOptionalNamePreservesTextWhileMakingItSingleLine()
    {
        const string source = "  Phát triển\r\n\r\nnhận thức  ";

        var normalized = AssessmentSyncTextNormalizer.NormalizeOptionalName(source);

        Assert.Equal("Phát triển nhận thức", normalized);
        Assert.NotNull(normalized);
        Assert.DoesNotContain('\r', normalized);
        Assert.DoesNotContain('\n', normalized);
    }
}
