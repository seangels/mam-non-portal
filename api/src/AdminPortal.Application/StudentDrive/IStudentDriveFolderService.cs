namespace AdminPortal.Application.StudentDrive;

public interface IStudentDriveFolderService
{
    string GetRequiredPoolFolderId();
    Task<DriveFolderInfo> ValidateFolderAsync(string folderId, CancellationToken cancellationToken);
    Task<StudentDriveMutation> EnsureStudentFolderAsync(
        string studentCode,
        string fullName,
        string? overrideFolderId,
        string destinationFolderId,
        CancellationToken cancellationToken);
    Task<StudentDriveMutation> MoveFolderAsync(
        string folderId,
        string destinationFolderId,
        CancellationToken cancellationToken);
    Task<DriveFolderInfo> RenameFolderAsync(
        string folderId,
        string newName,
        CancellationToken cancellationToken);
    Task RestoreParentsAsync(
        string folderId,
        IReadOnlyList<string> parentIds,
        CancellationToken cancellationToken);
    Task DeleteFolderIfEmptyAsync(string folderId, CancellationToken cancellationToken);
}
