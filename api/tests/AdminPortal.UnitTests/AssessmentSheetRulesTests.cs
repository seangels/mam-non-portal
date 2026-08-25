using AdminPortal.Application.AssessmentSheets;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;

namespace AdminPortal.UnitTests;

public sealed class AssessmentSheetRulesTests
{
    private static ActorContext Actor(UserRole role) => new(Guid.NewGuid(), Guid.NewGuid(), role, null);

    [Theory]
    [InlineData(UserRole.SuperAdmin)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Teacher)]
    public void EveryStaffRoleCanAccessAssessmentSheets(UserRole role) =>
        AssessmentSheetRules.EnsureAssessmentSheetRole(Actor(role));

    [Fact]
    public void UnknownOrUnassignedRoleIsForbidden() =>
        Assert.Throws<ForbiddenException>(() => AssessmentSheetRules.EnsureAssessmentSheetRole(Actor((UserRole)999)));

    [Fact]
    public void OpenSheetPassesEnsureOpen()
    {
        var sheet = new AssessmentSheet
        {
            AssessmentSheetStatus = AssessmentSheetStatus.Open,
            StudentSnapshot = new StudentSnapshot()
        };
        AssessmentSheetRules.EnsureOpen(sheet);
    }

    [Fact]
    public void DoneSheetRejectsEnsureOpen()
    {
        var sheet = new AssessmentSheet
        {
            AssessmentSheetStatus = AssessmentSheetStatus.Done,
            StudentSnapshot = new StudentSnapshot()
        };
        var exception = Assert.Throws<ConflictException>(() => AssessmentSheetRules.EnsureOpen(sheet));
        Assert.Equal(ProblemCodes.AssessmentSheetDone, exception.Code);
    }

    [Fact]
    public void EmptyIdListIsRejected() =>
        Assert.Throws<AppValidationException>(() => AssessmentSheetRules.EnsureDistinctIds([], "records"));

    [Fact]
    public void EmptyGuidInListIsRejected() =>
        Assert.Throws<AppValidationException>(() =>
            AssessmentSheetRules.EnsureDistinctIds([Guid.NewGuid(), Guid.Empty], "records"));

    [Fact]
    public void DuplicateIdsAreRejected()
    {
        var id = Guid.NewGuid();
        Assert.Throws<AppValidationException>(() => AssessmentSheetRules.EnsureDistinctIds([id, id], "records"));
    }

    [Fact]
    public void DistinctNonEmptyIdsPass() =>
        AssessmentSheetRules.EnsureDistinctIds([Guid.NewGuid(), Guid.NewGuid()], "assessmentIds");

    [Theory]
    [InlineData(AssessmentGrade.A, 4)]
    [InlineData(AssessmentGrade.B, 3)]
    [InlineData(AssessmentGrade.C, 2)]
    [InlineData(AssessmentGrade.D, 1)]
    public void GradeRankOrdersAFirst(AssessmentGrade grade, int expectedRank) =>
        Assert.Equal(expectedRank, AssessmentSheetRules.GradeRank(grade));

    [Theory]
    [InlineData(AssessmentGrade.A, "Đạt +")]
    [InlineData(AssessmentGrade.B, "Chưa đạt -")]
    [InlineData(AssessmentGrade.C, "Hỗ trợ +")]
    [InlineData(AssessmentGrade.D, "Hỗ trợ -")]
    public void GradeLabelMatchesConfirmedMappingInRequirements(AssessmentGrade grade, string expectedLabel) =>
        Assert.Equal(expectedLabel, AssessmentSheetRules.GradeLabel(grade));

    [Theory]
    [InlineData("Đạt +", AssessmentGrade.A)]
    [InlineData("Chưa đạt -", AssessmentGrade.B)]
    [InlineData("Hỗ trợ +", AssessmentGrade.C)]
    [InlineData("Hỗ trợ -", AssessmentGrade.D)]
    public void TryParseGradeLabelIsExactInverseOfGradeLabel(string label, AssessmentGrade expected)
    {
        Assert.True(AssessmentSheetRules.TryParseGradeLabel(label, out var grade));
        Assert.Equal(expected, grade);
    }

    [Fact]
    public void TryParseGradeLabelFailsOnUnknownOrMistypedLabel() =>
        Assert.False(AssessmentSheetRules.TryParseGradeLabel("A", out _));

    [Fact]
    public void GradeRankIsStrictlyDescendingFromAToD()
    {
        Assert.True(AssessmentSheetRules.GradeRank(AssessmentGrade.A) > AssessmentSheetRules.GradeRank(AssessmentGrade.B));
        Assert.True(AssessmentSheetRules.GradeRank(AssessmentGrade.B) > AssessmentSheetRules.GradeRank(AssessmentGrade.C));
        Assert.True(AssessmentSheetRules.GradeRank(AssessmentGrade.C) > AssessmentSheetRules.GradeRank(AssessmentGrade.D));
    }

    private static Assessment CreateAssessment(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = "Mục " + code,
        UpdatedByUser = null!
    };

    [Fact]
    public void BuildRecordsPrefillsPlanGradeAndPlanNoteFromCreateRequest()
    {
        var a = CreateAssessment("A01");
        var b = CreateAssessment("A02");
        var sheetId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);

        var records = AssessmentSheetRules.BuildRecords(
            sheetId,
            [a, b],
            [
                new CreateAssessmentSheetRecordRequest(a.Id, AssessmentGrade.B, "  cần quan sát thêm  "),
                new CreateAssessmentSheetRecordRequest(b.Id, null, "   ")
            ],
            now,
            actorId);

        var recordA = records.Single(x => x.AssessmentSnapshot.Code == "A01");
        var recordB = records.Single(x => x.AssessmentSnapshot.Code == "A02");
        Assert.Equal(AssessmentGrade.B, recordA.PlanGrade);
        Assert.Equal("cần quan sát thêm", recordA.PlanNote);
        Assert.Null(recordB.PlanGrade);
        Assert.Null(recordB.PlanNote);
    }

    [Fact]
    public void BuildRecordsAlwaysLeavesFinalGradeAndFinalNoteEmpty()
    {
        var a = CreateAssessment("A01");

        var records = AssessmentSheetRules.BuildRecords(
            Guid.NewGuid(),
            [a],
            [new CreateAssessmentSheetRecordRequest(a.Id, AssessmentGrade.A, "ghi chú gần nhất")],
            DateTimeOffset.UtcNow,
            Guid.NewGuid());

        var record = Assert.Single(records);
        Assert.Null(record.FinalGrade);
        Assert.Null(record.FinalNote);
        Assert.Equal(AssessmentGrade.A, record.PlanGrade);
        Assert.Equal("ghi chú gần nhất", record.PlanNote);
    }

    [Fact]
    public void BuildReplacementRecordKeepsPlanAndFinalGradeIndependent()
    {
        var sheet = new AssessmentSheet { AssessmentSheetStatus = AssessmentSheetStatus.Open, StudentSnapshot = new StudentSnapshot() };
        var assessment = CreateAssessment("A01");
        var now = DateTimeOffset.UtcNow;
        var actorId = Guid.NewGuid();

        var changedFinalOnly = AssessmentSheetRules.BuildReplacementRecord(
            sheet, assessment,
            new AssessmentSheetRecordRequest(assessment.Id, AssessmentGrade.A, "plan note", AssessmentGrade.C, "final note"),
            now, actorId);
        Assert.Equal(AssessmentGrade.A, changedFinalOnly.PlanGrade);
        Assert.Equal("plan note", changedFinalOnly.PlanNote);
        Assert.Equal(AssessmentGrade.C, changedFinalOnly.FinalGrade);
        Assert.Equal("final note", changedFinalOnly.FinalNote);

        var changedPlanOnly = AssessmentSheetRules.BuildReplacementRecord(
            sheet, assessment,
            new AssessmentSheetRecordRequest(assessment.Id, AssessmentGrade.D, "plan note", AssessmentGrade.C, "final note"),
            now, actorId);
        Assert.Equal(AssessmentGrade.D, changedPlanOnly.PlanGrade);
        Assert.Equal(AssessmentGrade.C, changedPlanOnly.FinalGrade);
    }

    [Fact]
    public void BuildReplacementRecordTrimsNotesAndTreatsBlankAsNull()
    {
        var sheet = new AssessmentSheet { AssessmentSheetStatus = AssessmentSheetStatus.Open, StudentSnapshot = new StudentSnapshot() };
        var assessment = CreateAssessment("A01");

        var record = AssessmentSheetRules.BuildReplacementRecord(
            sheet, assessment,
            new AssessmentSheetRecordRequest(assessment.Id, null, "  ghi chú  ", null, "   "),
            DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Equal("ghi chú", record.PlanNote);
        Assert.Null(record.FinalNote);
    }
}
