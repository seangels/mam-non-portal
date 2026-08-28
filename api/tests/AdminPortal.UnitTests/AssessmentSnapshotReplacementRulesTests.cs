using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.GoogleSheets;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;

namespace AdminPortal.UnitTests;

public sealed class AssessmentSnapshotReplacementRulesTests
{
    private static AssessmentRecord Record(string code, string name, string? lv1, string? lv2, string? lv3, int? rowIndex) =>
        new()
        {
            AssessmentSheetId = Guid.NewGuid(),
            AssessmentSheet = null!,
            AssessmentSnapshot = new AssessmentSnapshot
            {
                Code = code,
                Name = name,
                GroupLv1Name = lv1,
                GroupLv2Name = lv2,
                GroupLv3Name = lv3,
                RowIndex = rowIndex
            }
        };

    private static Assessment Catalog(string code, string name, string? lv1, string? lv2, string? lv3, int? rowIndex) =>
        new() { Code = code, Name = name, GroupLv1Name = lv1, GroupLv2Name = lv2, GroupLv3Name = lv3, RowIndex = rowIndex };

    [Fact]
    public void ValidateRejectsNoFieldSelected()
    {
        var spec = new AssessmentRecordSnapshotReplacement(SheetStatuses: [AssessmentSheetStatus.Open]);

        Assert.Throws<AppValidationException>(() => AssessmentSnapshotReplacementRules.Validate(spec));
    }

    [Fact]
    public void ValidateRejectsNoSheetStatusSelected()
    {
        var spec = new AssessmentRecordSnapshotReplacement(Name: true, SheetStatuses: []);

        Assert.Throws<AppValidationException>(() => AssessmentSnapshotReplacementRules.Validate(spec));
    }

    [Fact]
    public void ValidatePassesWhenAtLeastOneFieldAndStatus() =>
        AssessmentSnapshotReplacementRules.Validate(
            new AssessmentRecordSnapshotReplacement(RowIndex: true, SheetStatuses: [AssessmentSheetStatus.Done]));

    [Fact]
    public void ApplyOnlyReplacesSelectedFields()
    {
        var now = DateTimeOffset.UnixEpoch;
        var actor = Guid.NewGuid();
        var record = Record("A1", "old name", "old lv1", "old lv2", "old lv3", 1);
        var catalog = new Dictionary<string, Assessment>
        {
            ["A1"] = Catalog("A1", "new name", "new lv1", "new lv2", "new lv3", 9)
        };
        var spec = new AssessmentRecordSnapshotReplacement(
            Name: true, GroupLv2Name: true, SheetStatuses: [AssessmentSheetStatus.Open]);

        var replaced = AssessmentSnapshotReplacementRules.Apply([record], catalog, spec, now, actor);

        Assert.Equal(1, replaced);
        Assert.Equal("new name", record.AssessmentSnapshot.Name);
        Assert.Equal("new lv2", record.AssessmentSnapshot.GroupLv2Name);
        Assert.Equal("old lv1", record.AssessmentSnapshot.GroupLv1Name);
        Assert.Equal("old lv3", record.AssessmentSnapshot.GroupLv3Name);
        Assert.Equal(1, record.AssessmentSnapshot.RowIndex);
        Assert.Equal(now, record.UpdatedAt);
        Assert.Equal(actor, record.UpdatedByUserId);
    }

    [Fact]
    public void ApplySkipsRecordsWhoseCodeIsNotInCatalog()
    {
        var record = Record("GONE", "old name", null, null, null, null);
        var catalog = new Dictionary<string, Assessment> { ["A1"] = Catalog("A1", "new name", null, null, null, 2) };
        var spec = new AssessmentRecordSnapshotReplacement(Name: true, SheetStatuses: [AssessmentSheetStatus.Open]);

        var replaced = AssessmentSnapshotReplacementRules.Apply([record], catalog, spec, DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Equal(0, replaced);
        Assert.Equal("old name", record.AssessmentSnapshot.Name);
    }

    [Fact]
    public void ApplyDoesNotStampRecordWhenSelectedValuesAlreadyMatch()
    {
        var record = Record("A1", "same name", "old lv1", null, null, 1);
        var catalog = new Dictionary<string, Assessment> { ["A1"] = Catalog("A1", "same name", "new lv1", null, null, 1) };
        // Only Name is selected and it is already equal -> nothing changes for this record.
        var spec = new AssessmentRecordSnapshotReplacement(Name: true, SheetStatuses: [AssessmentSheetStatus.Open]);
        var untouched = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        record.UpdatedAt = untouched;

        var replaced = AssessmentSnapshotReplacementRules.Apply([record], catalog, spec, DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Equal(0, replaced);
        Assert.Equal(untouched, record.UpdatedAt);
        Assert.Equal("old lv1", record.AssessmentSnapshot.GroupLv1Name);
    }
}
