using AdminPortal.Application.AssessmentResults;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;

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

    [Fact]
    public void CreateDatabaseVersionIsStableAndTracksCatalogAndLatestRecordState()
    {
        var now = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            Code = "A-001",
            Name = "Assessment",
            RowIndex = 10,
            UpdatedByUserId = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now
        };
        var recordId = Guid.NewGuid();
        var baseline = AssessmentResultRules.CreateDatabaseVersion(
            assessment, recordId, AssessmentGrade.A, "note", now);

        Assert.Equal(
            baseline,
            AssessmentResultRules.CreateDatabaseVersion(assessment, recordId, AssessmentGrade.A, "note", now));
        Assert.NotEqual(
            baseline,
            AssessmentResultRules.CreateDatabaseVersion(assessment, recordId, AssessmentGrade.B, "note", now));
        Assert.NotEqual(
            baseline,
            AssessmentResultRules.CreateDatabaseVersion(assessment, recordId, AssessmentGrade.A, "new note", now));
        Assert.NotEqual(
            baseline,
            AssessmentResultRules.CreateDatabaseVersion(assessment, null, null, null, null));

        assessment.Name = "Updated assessment";
        Assert.NotEqual(
            baseline,
            AssessmentResultRules.CreateDatabaseVersion(assessment, recordId, AssessmentGrade.A, "note", now));
    }
}
