using AdminPortal.Application.GoogleSheets;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;

namespace AdminPortal.IntegrationTests;

internal sealed class FakeStudentGoogleSheetsService : IStudentGoogleSheetsService
{
    public System.Collections.Concurrent.ConcurrentQueue<StudentSheetCall> Calls { get; } = new();
    public System.Collections.Concurrent.ConcurrentDictionary<string, byte> UnavailableCodes { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> Suggestions { get; set; } = [];

    public Task<IReadOnlyList<string>> GetAvailableStudentCodesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Suggestions);

    public Task<StudentSheetMutation> CreateOrUpdateAsync(
        string studentCode,
        string fullName,
        string nickName,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken)
    {
        Calls.Enqueue(new StudentSheetCall("Create", studentCode, fullName, nickName, dateOfBirth));
        if (UnavailableCodes.ContainsKey(studentCode))
        {
            throw new ConflictException(
                $"StudentID '{studentCode}' đã có tên trong Google Sheets.",
                ProblemCodes.StudentGoogleSheetsCodeUnavailable);
        }
        return Task.FromResult(new StudentSheetMutation(2, null, null, null, null, true));
    }

    public Task<StudentSheetMutation> UpdateAsync(
        string sheetStudentId,
        string fullName,
        string nickName,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken)
    {
        Calls.Enqueue(new StudentSheetCall("Update", sheetStudentId, fullName, nickName, dateOfBirth));
        return Task.FromResult(new StudentSheetMutation(2, sheetStudentId, null, null, null, false));
    }

    public Task RestoreAsync(StudentSheetMutation mutation, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed record StudentSheetCall(
    string Operation,
    string SheetStudentId,
    string FullName,
    string NickName,
    DateOnly DateOfBirth);
