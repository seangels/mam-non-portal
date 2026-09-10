using AdminPortal.Application.AssessmentResults;

namespace AdminPortal.UnitTests;

public sealed class AssessmentResultRulesTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  ghi chú  ", "ghi chú")]
    public void NormalizeNoteReturnsCanonicalValue(string? value, string? expected)
    {
        Assert.Equal(expected, AssessmentResultRules.NormalizeNote(value));
    }

    [Fact]
    public void CreateVersionIsStableAndChangesForEitherSourceCell()
    {
        var assessmentId = Guid.NewGuid();
        var baseline = AssessmentResultRules.CreateVersion(assessmentId, "H12", "I12", "Đạt +", "ghi chú");

        Assert.Equal(
            baseline,
            AssessmentResultRules.CreateVersion(assessmentId, "H12", "I12", "Đạt +", "ghi chú"));
        Assert.NotEqual(
            baseline,
            AssessmentResultRules.CreateVersion(assessmentId, "H12", "I12", "Hỗ trợ +", "ghi chú"));
        Assert.NotEqual(
            baseline,
            AssessmentResultRules.CreateVersion(assessmentId, "H12", "I12", "Đạt +", "ghi chú mới"));
        Assert.NotEqual(
            baseline,
            AssessmentResultRules.CreateVersion(assessmentId, "H13", "I13", "Đạt +", "ghi chú"));
    }
}
