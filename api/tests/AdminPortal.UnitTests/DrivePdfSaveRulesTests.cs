using AdminPortal.Application.GoogleSheets;

namespace AdminPortal.UnitTests;

public sealed class DrivePdfSaveRulesTests
{
    [Fact]
    public void ResolveUsesUpdateWhenExistingDriveFileIdCanBeExtracted()
    {
        var target = DrivePdfSaveRules.Resolve(
            "https://drive.google.com/file/d/existing-file_123/view?usp=sharing");

        Assert.Equal(DrivePdfSaveOperation.Update, target.Operation);
        Assert.Equal("existing-file_123", target.ExistingFileId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://drive.google.com/open?id=unsupported-shape")]
    public void ResolveUsesCreateWhenExistingDriveFileIdCannotBeExtracted(string? existingFileLink)
    {
        var target = DrivePdfSaveRules.Resolve(existingFileLink);

        Assert.Equal(DrivePdfSaveOperation.Create, target.Operation);
        Assert.Null(target.ExistingFileId);
    }
}
