using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.GoogleSheets;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace AdminPortal.Application.StudentDrive;

public sealed partial class GoogleStudentDriveFolderService : IStudentDriveFolderService, IDisposable
{
    private const string FolderMimeType = "application/vnd.google-apps.folder";
    private readonly GoogleSheetsSettings settings;
    private readonly ILogger<GoogleStudentDriveFolderService> logger;
    private readonly Lazy<UserCredential> credential;
    private readonly Lazy<DriveService> drive;
    private bool disposed;

    public GoogleStudentDriveFolderService(
        IOptions<GoogleSheetsSettings> options,
        ILogger<GoogleStudentDriveFolderService> logger)
    {
        settings = options.Value;
        this.logger = logger;
        credential = new Lazy<UserCredential>(CreateCredential);
        drive = new Lazy<DriveService>(() => new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential.Value,
            ApplicationName = "GV Portal"
        }));
    }

    public string GetRequiredPoolFolderId() =>
        StudentDriveFolderRules.NormalizeFolderId(settings.StudentDriveFolderPoolId, "studentDriveFolderPoolId")
        ?? throw new NormalException(
            "Chưa cấu hình thư mục pool Google Drive cho học sinh.",
            ProblemCodes.StudentDrivePoolNotConfigured);

    public async Task<DriveFolderInfo> ValidateFolderAsync(string folderId, CancellationToken cancellationToken)
    {
        var normalized = StudentDriveFolderRules.NormalizeFolderId(folderId)!;
        try
        {
            var request = drive.Value.Files.Get(normalized);
            request.Fields = "id,name,mimeType,trashed,parents,webViewLink,capabilities(canEdit)";
            request.SupportsAllDrives = true;
            var folder = await request.ExecuteAsync(cancellationToken);
            if (folder.Trashed == true || folder.MimeType != FolderMimeType || folder.Capabilities?.CanEdit != true)
                throw InvalidFolder();
            return Map(folder);
        }
        catch (AppException) { throw; }
        catch (Exception exception)
        {
            LogDriveFailure(logger, nameof(ValidateFolderAsync), exception.GetType().Name);
            throw InvalidFolder();
        }
    }

    public async Task<StudentDriveMutation> EnsureStudentFolderAsync(
        string studentCode,
        string fullName,
        string? overrideFolderId,
        string destinationFolderId,
        CancellationToken cancellationToken)
    {
        var poolId = GetRequiredPoolFolderId();
        await ValidateFolderAsync(poolId, cancellationToken);
        await ValidateFolderAsync(destinationFolderId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(overrideFolderId))
            return await MoveFolderAsync(StudentDriveFolderRules.NormalizeFolderId(overrideFolderId)!, destinationFolderId, cancellationToken);

        var name = StudentDriveFolderRules.BuildFolderName(studentCode, fullName);
        string? createdFolderId = null;
        try
        {
            var list = drive.Value.Files.List();
            list.Q = $"mimeType = '{FolderMimeType}' and trashed = false and name = '{EscapeQuery(name)}' and '{EscapeQuery(poolId)}' in parents";
            list.Fields = "files(id,name,mimeType,trashed,parents,webViewLink,capabilities(canEdit))";
            list.Spaces = "drive";
            list.PageSize = 3;
            list.SupportsAllDrives = true;
            list.IncludeItemsFromAllDrives = true;
            var matches = (await list.ExecuteAsync(cancellationToken)).Files ?? [];
            if (matches.Count > 1)
                throw new ConflictException(
                    "Có nhiều thư mục học sinh trùng tên trong pool Google Drive.",
                    ProblemCodes.StudentDriveDuplicateFolder);

            if (matches.Count == 1)
            {
                var moved = await MoveFolderAsync(matches[0].Id, destinationFolderId, cancellationToken);
                return moved with { Reused = true };
            }

            var create = drive.Value.Files.Create(new DriveFile
            {
                Name = name,
                MimeType = FolderMimeType,
                Parents = [poolId]
            });
            create.Fields = "id,name,mimeType,trashed,parents,webViewLink,capabilities(canEdit)";
            create.SupportsAllDrives = true;
            var created = await create.ExecuteAsync(cancellationToken);
            createdFolderId = created.Id;
            var result = new StudentDriveMutation(Map(created), true, false, false, []);
            if (poolId == destinationFolderId) return result;
            var movedCreated = await MoveFolderAsync(created.Id, destinationFolderId, cancellationToken);
            return movedCreated with { Created = true, PreviousParentIds = [] };
        }
        catch (AppException)
        {
            if (createdFolderId is not null)
            {
                try { await DeleteFolderIfEmptyAsync(createdFolderId, cancellationToken); }
                catch (Exception compensationException)
                {
                    LogDriveFailure(logger, "EnsureStudentFolderCompensation", compensationException.GetType().Name);
                }
            }
            throw;
        }
        catch (Exception exception)
        {
            if (createdFolderId is not null)
            {
                try
                {
                    await DeleteFolderIfEmptyAsync(createdFolderId, cancellationToken);
                }
                catch (Exception compensationException)
                {
                    LogDriveFailure(logger, "EnsureStudentFolderCompensation", compensationException.GetType().Name);
                }
            }
            LogDriveFailure(logger, nameof(EnsureStudentFolderAsync), exception.GetType().Name);
            throw DriveFailed();
        }
    }

    public async Task<StudentDriveMutation> MoveFolderAsync(
        string folderId,
        string destinationFolderId,
        CancellationToken cancellationToken)
    {
        var folder = await ValidateFolderAsync(folderId, cancellationToken);
        var destination = await ValidateFolderAsync(destinationFolderId, cancellationToken);
        if (folder.ParentIds.Count == 1 && folder.ParentIds[0] == destination.Id)
            return new StudentDriveMutation(folder, false, false, false, folder.ParentIds);

        try
        {
            var update = drive.Value.Files.Update(new DriveFile(), folder.Id);
            update.AddParents = destination.Id;
            update.RemoveParents = folder.ParentIds.Count == 0 ? null : string.Join(',', folder.ParentIds);
            update.Fields = "id,name,mimeType,trashed,parents,webViewLink,capabilities(canEdit)";
            update.SupportsAllDrives = true;
            var updated = await update.ExecuteAsync(cancellationToken);
            return new StudentDriveMutation(Map(updated), false, false, true, folder.ParentIds);
        }
        catch (Exception exception)
        {
            LogDriveFailure(logger, nameof(MoveFolderAsync), exception.GetType().Name);
            throw DriveFailed();
        }
    }

    public async Task<DriveFolderInfo> RenameFolderAsync(string folderId, string newName, CancellationToken cancellationToken)
    {
        await ValidateFolderAsync(folderId, cancellationToken);
        try
        {
            var update = drive.Value.Files.Update(new DriveFile { Name = newName }, folderId);
            update.Fields = "id,name,mimeType,trashed,parents,webViewLink,capabilities(canEdit)";
            update.SupportsAllDrives = true;
            return Map(await update.ExecuteAsync(cancellationToken));
        }
        catch (Exception exception)
        {
            LogDriveFailure(logger, nameof(RenameFolderAsync), exception.GetType().Name);
            throw DriveFailed();
        }
    }

    public async Task RestoreParentsAsync(string folderId, IReadOnlyList<string> parentIds, CancellationToken cancellationToken)
    {
        var current = await ValidateFolderAsync(folderId, cancellationToken);
        var update = drive.Value.Files.Update(new DriveFile(), folderId);
        update.AddParents = parentIds.Count == 0 ? null : string.Join(',', parentIds);
        update.RemoveParents = current.ParentIds.Count == 0 ? null : string.Join(',', current.ParentIds);
        update.Fields = "id";
        update.SupportsAllDrives = true;
        await update.ExecuteAsync(cancellationToken);
    }

    public async Task DeleteFolderIfEmptyAsync(string folderId, CancellationToken cancellationToken)
    {
        var list = drive.Value.Files.List();
        list.Q = $"trashed = false and '{EscapeQuery(folderId)}' in parents";
        list.Fields = "files(id)";
        list.PageSize = 1;
        list.SupportsAllDrives = true;
        list.IncludeItemsFromAllDrives = true;
        if (((await list.ExecuteAsync(cancellationToken)).Files?.Count ?? 0) != 0) return;
        var delete = drive.Value.Files.Delete(folderId);
        delete.SupportsAllDrives = true;
        await delete.ExecuteAsync(cancellationToken);
    }

    private UserCredential CreateCredential()
    {
        var credentialPath = string.IsNullOrWhiteSpace(settings.CredentialFilePath) ? "google-credentials.json" : settings.CredentialFilePath;
        var tokenStorePath = string.IsNullOrWhiteSpace(settings.TokenStorePath) ? "google-token-store" : settings.TokenStorePath;
        var authUser = string.IsNullOrWhiteSpace(settings.AuthUser) ? "user" : settings.AuthUser;
        using var stream = new FileStream(credentialPath, FileMode.Open, FileAccess.Read);
        return GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            [SheetsService.Scope.Spreadsheets, DriveService.Scope.Drive], authUser, CancellationToken.None,
            new FileDataStore(tokenStorePath, true)).GetAwaiter().GetResult();
    }

    private static DriveFolderInfo Map(DriveFile file) => new(
        file.Id,
        file.Name,
        string.IsNullOrWhiteSpace(file.WebViewLink) ? $"https://drive.google.com/drive/folders/{file.Id}" : file.WebViewLink,
        file.Parents?.ToArray() ?? []);

    private static string EscapeQuery(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);
    private static ConflictException InvalidFolder() => new(
        "Thư mục Google Drive không hợp lệ, đã bị xóa hoặc tài khoản không có quyền chỉnh sửa.",
        ProblemCodes.StudentDriveFolderInvalid);
    private static NormalException DriveFailed() => new(
        "Không thể hoàn tất thao tác Google Drive.",
        ProblemCodes.StudentDriveOperationFailed);

    public void Dispose()
    {
        if (disposed) return;
        if (drive.IsValueCreated) drive.Value.Dispose();
        disposed = true;
    }

    [LoggerMessage(EventId = 31, Level = LogLevel.Warning, Message = "Google Drive student-folder operation {Operation} failed with {ExceptionType}.")]
    private static partial void LogDriveFailure(ILogger logger, string operation, string exceptionType);
}
