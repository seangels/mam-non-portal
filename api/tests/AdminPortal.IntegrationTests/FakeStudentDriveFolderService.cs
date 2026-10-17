using System.Collections.Concurrent;
using AdminPortal.Application.StudentDrive;

namespace AdminPortal.IntegrationTests;

internal sealed class FakeStudentDriveFolderService : IStudentDriveFolderService
{
    private const string PoolId = "integration-test-pool";
    private readonly ConcurrentDictionary<string, DriveFolderInfo> folders = new();

    public string GetRequiredPoolFolderId() => PoolId;

    public Task<DriveFolderInfo> ValidateFolderAsync(string folderId, CancellationToken cancellationToken) =>
        Task.FromResult(folders.GetOrAdd(folderId,
            id => new DriveFolderInfo(id, id, $"https://drive.google.com/drive/folders/{id}", [PoolId])));

    public async Task<StudentDriveMutation> EnsureStudentFolderAsync(
        string studentCode,
        string fullName,
        string? overrideFolderId,
        string destinationFolderId,
        CancellationToken cancellationToken)
    {
        var id = overrideFolderId ?? $"student-{Guid.NewGuid():N}";
        var existing = folders.TryGetValue(id, out var current);
        current ??= new DriveFolderInfo(
            id,
            StudentDriveFolderRules.BuildFolderName(studentCode, fullName),
            $"https://drive.google.com/drive/folders/{id}",
            [PoolId]);
        folders[id] = current;
        var moved = await MoveFolderAsync(id, destinationFolderId, cancellationToken);
        return moved with { Created = !existing && overrideFolderId is null, Reused = false };
    }

    public Task<StudentDriveMutation> MoveFolderAsync(
        string folderId,
        string destinationFolderId,
        CancellationToken cancellationToken)
    {
        var current = folders.GetOrAdd(folderId,
            id => new DriveFolderInfo(id, id, $"https://drive.google.com/drive/folders/{id}", [PoolId]));
        var moved = current.ParentIds.Count != 1 || current.ParentIds[0] != destinationFolderId;
        var updated = current with { ParentIds = [destinationFolderId] };
        folders[folderId] = updated;
        return Task.FromResult(new StudentDriveMutation(updated, false, false, moved, current.ParentIds));
    }

    public Task<DriveFolderInfo> RenameFolderAsync(string folderId, string newName, CancellationToken cancellationToken)
    {
        var current = folders[folderId];
        var updated = current with { Name = newName };
        folders[folderId] = updated;
        return Task.FromResult(updated);
    }

    public Task RestoreParentsAsync(string folderId, IReadOnlyList<string> parentIds, CancellationToken cancellationToken)
    {
        if (folders.TryGetValue(folderId, out var current)) folders[folderId] = current with { ParentIds = parentIds };
        return Task.CompletedTask;
    }

    public Task DeleteFolderIfEmptyAsync(string folderId, CancellationToken cancellationToken)
    {
        folders.TryRemove(folderId, out _);
        return Task.CompletedTask;
    }
}
