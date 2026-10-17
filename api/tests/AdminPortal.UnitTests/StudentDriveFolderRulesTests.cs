using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.StudentDrive;

namespace AdminPortal.UnitTests;

public sealed class StudentDriveFolderRulesTests
{
    [Theory]
    [InlineData("folder_123-AbC", "folder_123-AbC")]
    [InlineData("https://drive.google.com/drive/folders/folder_123-AbC?usp=sharing", "folder_123-AbC")]
    [InlineData("https://drive.google.com/open?id=folder_123-AbC", "folder_123-AbC")]
    public void NormalizeFolderIdAcceptsRawIdAndSupportedUrls(string input, string expected)
    {
        Assert.Equal(expected, StudentDriveFolderRules.NormalizeFolderId(input));
    }

    [Theory]
    [InlineData("https://example.com/folders/folder_123")]
    [InlineData("https://drive.google.com/file/d/not-a-folder/view")]
    [InlineData("invalid id")]
    public void NormalizeFolderIdRejectsUnsupportedValues(string input)
    {
        Assert.Throws<AppValidationException>(() => StudentDriveFolderRules.NormalizeFolderId(input));
    }

    [Fact]
    public void BuildFolderNameUsesCanonicalStudentCodeAndTrimmedFullName()
    {
        Assert.Equal("HS-001 - Nguyễn Văn An", StudentDriveFolderRules.BuildFolderName(" hs-001 ", " Nguyễn Văn An "));
    }
}
