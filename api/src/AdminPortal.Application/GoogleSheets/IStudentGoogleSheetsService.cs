namespace AdminPortal.Application.GoogleSheets;

public interface IStudentGoogleSheetsService
{
    Task<StudentSheetMutation> CreateOrUpdateAsync(
        string studentCode,
        string fullName,
        string nickName,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken);

    Task<StudentSheetMutation> UpdateAsync(
        string sheetStudentId,
        string fullName,
        string nickName,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken);

    Task RestoreAsync(StudentSheetMutation mutation, CancellationToken cancellationToken);
}

public sealed record StudentSheetMutation(
    int RowNumber,
    string? PreviousStudentId,
    string? PreviousFullName,
    string? PreviousNickName,
    object? PreviousDateOfBirth,
    bool StudentIdWasWritten);
