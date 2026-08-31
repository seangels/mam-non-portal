using AdminPortal.Application.AssessmentSheets;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EFCore.BulkExtensions;
using System.Text.Json;

namespace AdminPortal.Application.GoogleSheets;

public class SpreadsheetConfig
{
    public required string SpreadsheetId { get; set; }
    public required string SheetConfigRange { get; set; }
    public required string SheetConfigLastRow { get; set; }
}
public class GoogleSheetsSettings : IGoogleSheetsSettings
{

    public string CredentialFilePath { get; set; } = string.Empty;
    public string TokenStorePath { get; set; } = string.Empty;
    public string AuthUser { get; set; } = string.Empty;
    public string SpreadsheetId { get; set; } = string.Empty;
    public string SheetConfigRange { get; set; } = string.Empty;
    public string SheetConfigLastRow { get; set; } = string.Empty;

}
internal sealed class AssessmentGroupSync
{
    public Guid Id { get; set; } = Guid.Empty;
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public int? DisplayOrder { get; set; }
    public string? ParentKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class GoogleSheetsService : IGoogleSheetsService, IDisposable
{
    private readonly Lazy<UserCredential> _credential;
    private readonly Lazy<SheetsService> _sheetsService;
    private readonly Lazy<DriveService> _driveService;

    private bool _disposed;
    private readonly IApplicationDbContext dbContext;
    private readonly ICurrentActor currentActor;
    private readonly ILogger<GoogleSheetsService> logger;
    private readonly TimeProvider timeProvider;
    private readonly GoogleSheetsSettings googleSheetsSettings;
    private const string Assessment_fullDataRange = "Assessment_fullDataRange";
    private const string Assessment_fullHeaderRange = "Assessment_fullHeaderRange";
    private const string Assessment_headerNames = "Assessment_headerNames";

    private const string LatestResults_fullDataRange = "LatestResults_fullDataRange";
    private const string LatestResults_fullHeaderRange = "LatestResults_fullHeaderRange";
    private const string LatestResults_headerNames = "LatestResults_headerNames";

    private const string ResultSource_SheetName = "ResultSource_SheetName";
    private const string ResultSource_FirstDataRow = "ResultSource_FirstDataRow";
    private const string ResultSource_LastRow = "ResultSource_LastRow";
    private const string ResultSource_FirstStudentColumnIndex = "ResultSource_FirstStudentColumnIndex";
    private const string ResultSource_AssessmentCodeRange = "ResultSource_AssessmentCodeRange";
    private const string ResultSource_StudentCodeRange = "ResultSource_StudentCodeRange";
    private Dictionary<string, string>? ResultLatestSheetConfigCacheInTransient;
    private static readonly Action<ILogger, string, Exception?> GoogleSheetsCredentialSmokeFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, nameof(GoogleSheetsCredentialSmokeFailed)),
            "Google Sheets credential smoke failed with {ExceptionType}.");

    public GoogleSheetsService(
        IOptions<GoogleSheetsSettings> configuration,
        IApplicationDbContext dbContext,
        ICurrentActor currentActor,
        TimeProvider timeProvider,
        ILogger<GoogleSheetsService> logger)
    {
        this.dbContext = dbContext;
        this.currentActor = currentActor;
        this.logger = logger;
        this.timeProvider = timeProvider;
        this.googleSheetsSettings = configuration.Value;

        _credential = new Lazy<UserCredential>(CreateCredential);
        _sheetsService = new Lazy<SheetsService>(CreateSheetsService);
        _driveService = new Lazy<DriveService>(CreateDriveService);
    }

    // Dùng OAuth 2.0 "installed app" flow của tài khoản Google cá nhân (không phải service account):
    // credentialPath trỏ tới OAuth client secret (Desktop app) tải từ Google Cloud Console. Lần đầu chạy
    // sẽ mở trình duyệt để người dùng đăng nhập/consent; refresh token sau đó được cache trong TokenStorePath
    // nên các lần khởi động sau không cần consent lại.
    private UserCredential CreateCredential()
    {
        var credentialPath = string.IsNullOrWhiteSpace(googleSheetsSettings.CredentialFilePath)
            ? "google-credentials.json"
            : googleSheetsSettings.CredentialFilePath;
        var tokenStorePath = string.IsNullOrWhiteSpace(googleSheetsSettings.TokenStorePath)
            ? "google-token-store"
            : googleSheetsSettings.TokenStorePath;
        var authUser = string.IsNullOrWhiteSpace(googleSheetsSettings.AuthUser)
            ? "user"
            : googleSheetsSettings.AuthUser;

        using var stream = new FileStream(credentialPath, FileMode.Open, FileAccess.Read);
        var clientSecrets = GoogleClientSecrets.FromStream(stream).Secrets;

        return GoogleWebAuthorizationBroker.AuthorizeAsync(
            clientSecrets,
            [SheetsService.Scope.Spreadsheets, DriveService.Scope.Drive],
            authUser,
            CancellationToken.None,
            new FileDataStore(tokenStorePath, true)).GetAwaiter().GetResult();
    }

    private SheetsService CreateSheetsService() => new(new BaseClientService.Initializer
    {
        HttpClientInitializer = _credential.Value,
        ApplicationName = "CleanArchGoogleSheets"
    });

    private DriveService CreateDriveService() => new(new BaseClientService.Initializer
    {
        HttpClientInitializer = _credential.Value,
        ApplicationName = "CleanArchGoogleSheets"
    });

    // 2. Hiện thực hàm Dispose để giải phóng _sheetsService
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    async Task<Dictionary<string, string>> GetResultLatestSheetConfig(CancellationToken cancellationToken)
    {
        if (ResultLatestSheetConfigCacheInTransient != null && ResultLatestSheetConfigCacheInTransient.Count > 2)
        {
            return ResultLatestSheetConfigCacheInTransient;
        }
        var configValues = await GetSheetConfig(new SpreadsheetConfig
        {
            SpreadsheetId = googleSheetsSettings.SpreadsheetId,
            SheetConfigRange = googleSheetsSettings.SheetConfigRange,
            SheetConfigLastRow = googleSheetsSettings.SheetConfigLastRow
        }, cancellationToken);
        ResultLatestSheetConfigCacheInTransient = configValues;
        return ResultLatestSheetConfigCacheInTransient;
    }
    async Task<Dictionary<string, string>> GetSheetConfig(SpreadsheetConfig settings, CancellationToken cancellationToken)
    {

        var requestConfigLastRow = _sheetsService.Value.Spreadsheets.Values
            .Get(settings.SpreadsheetId, settings.SheetConfigLastRow);
        var responseConfigLastRow = await requestConfigLastRow.ExecuteAsync(cancellationToken);
        var configValuesLastRow = responseConfigLastRow.Values;
        var _SheetConfigRange = settings.SheetConfigRange.Replace("{{lastRow}}", configValuesLastRow[0][0].ToString());
        var requestConfig = _sheetsService.Value.Spreadsheets.Values
            .Get(settings.SpreadsheetId, _SheetConfigRange);
        var responseConfig = await requestConfig.ExecuteAsync(cancellationToken);
        var configValues = responseConfig.Values
            .Where(x => x[0] != null && !string.IsNullOrWhiteSpace(x[0].ToString())
                && x[1] != null && !string.IsNullOrWhiteSpace(x[1].ToString())
            )
            .Select(x => new
            {
                Key = x[0].ToString()!,
                Value = x[1].ToString()!,
            })
            .GroupBy(x => x.Key)
            .ToDictionary(x => x.Key, x => x.First().Value)
            ;

        return configValues;
    }
    async Task<Dictionary<string, int>> ReadHeaderMappingsAsync(string headerRange, List<string> headerNames, CancellationToken cancellationToken)
    {
        var headerMappings = new Dictionary<string, int>();
        var requestHeader = _sheetsService.Value.Spreadsheets.Values.Get(googleSheetsSettings.SpreadsheetId, headerRange);
        var responseHeader = await requestHeader.ExecuteAsync(cancellationToken);
        var headerValues = responseHeader.Values;
        for (int i = 0; i < headerValues[0].Count; i++)
        {
            var headerName = headerValues[0][i]?.ToString();
            if (!string.IsNullOrEmpty(headerName))
            {
                headerName = headerName.Replace("{{", "").Replace("}}", "");
                headerMappings.TryAdd(headerName, i);
            }
        }
        return headerMappings;
    }
    async Task<List<AssessmentGoogleSheetResponse>> ReadAssessmentsAsync(CancellationToken cancellationToken)
    {
        var _spreadsheetId = googleSheetsSettings.SpreadsheetId;
        var latestResultConfig = await GetResultLatestSheetConfig(cancellationToken);
        var headerRange = latestResultConfig.GetValueOrDefault(Assessment_fullHeaderRange, string.Empty);
        var headerNamesString = latestResultConfig.GetValueOrDefault(Assessment_headerNames, string.Empty);
        var dataRange = latestResultConfig.GetValueOrDefault(Assessment_fullDataRange, string.Empty);
        var checkEmpties = new Dictionary<string, string>
        {
            [Assessment_fullHeaderRange] = headerRange,
            [Assessment_headerNames] = headerNamesString,
            [Assessment_fullDataRange] = dataRange,
        };
        if (checkEmpties.Any(x => string.IsNullOrWhiteSpace(x.Value)))
        {

            throw new AppValidationException(
                "Không tìm thấy sheet config key/value.",
                new Dictionary<string, string[]>
                {
                    ["missing_config_keyvalue"] = checkEmpties
                        .Where(x => string.IsNullOrWhiteSpace(x.Value))
                        .Select(x => x.Key)
                        .ToArray()
                });
        }
        var headerNames = headerNamesString.Split(",").ToList();
        var headerMappings = await ReadHeaderMappingsAsync(headerRange, headerNames, cancellationToken);

        var request = _sheetsService.Value.Spreadsheets.Values.Get(_spreadsheetId, dataRange);
        var response = await request.ExecuteAsync(cancellationToken);
        var values = response.Values;

        var assessments = new List<AssessmentGoogleSheetResponse>();
        if (values == null) return assessments;
        var listFields = headerNames;
        var listColumnIndexs = listFields
            .ToDictionary(x => x, x => headerMappings.GetValueOrDefault(x, -1));
        if (listColumnIndexs.Any(x => x.Value < 0))
        {

            throw new AppValidationException(
                "Không tìm thấy header key",
                new Dictionary<string, string[]>
                {
                    ["missing_header_key_assessment"] = listColumnIndexs
                        .Where(x => x.Value < 0)
                        .Select(x => x.Key)
                        .ToArray()
                }, ProblemCodes.AssessmentSheetGoogleOperationFailed);
        }

        foreach (var row in values)
        {
            var cellValues = listColumnIndexs
                .Select(x => new
                {
                    x.Key,
                    value = row.Count > headerMappings.GetValueOrDefault(x.Key, -1) ? row[headerMappings.GetValueOrDefault(x.Key, -1)]?.ToString() ?? null : null,
                })
                .ToDictionary(x => x.Key, x => x.value);
            assessments.Add(new AssessmentGoogleSheetResponse(
                cellValues.GetValueOrDefault("item_id", null),
                cellValues.GetValueOrDefault("item", null),
                cellValues.GetValueOrDefault("nhom_tuoi", null),
                cellValues.GetValueOrDefault("group_lv2", null),
                cellValues.GetValueOrDefault("group_lv3", null),
                cellValues.GetValueOrDefault("row_index", null)
            ));
        }
        return assessments;
    }


    async Task<List<AssessmentLastResultGoogleSheetResponse>> ReadAssessmentLatestResultsAsync(CancellationToken cancellationToken)
    {
        var _spreadsheetId = googleSheetsSettings.SpreadsheetId;
        var latestResultConfig = await GetResultLatestSheetConfig(cancellationToken);
        var headerRange = latestResultConfig.GetValueOrDefault(LatestResults_fullHeaderRange, string.Empty);
        var headerNamesString = latestResultConfig.GetValueOrDefault(LatestResults_headerNames, string.Empty);
        var dataRange = latestResultConfig.GetValueOrDefault(LatestResults_fullDataRange, string.Empty);
        var checkEmpties = new Dictionary<string, string>
        {
            [LatestResults_fullHeaderRange] = headerRange,
            [LatestResults_headerNames] = headerNamesString,
            [LatestResults_fullDataRange] = dataRange,
        };
        if (checkEmpties.Any(x => string.IsNullOrWhiteSpace(x.Value)))
        {

            throw new AppValidationException(
                "Không tìm thấy sheet config key/value.",
                new Dictionary<string, string[]>
                {
                    ["missing_config_keyvalue"] = checkEmpties
                        .Where(x => string.IsNullOrWhiteSpace(x.Value))
                        .Select(x => x.Key)
                        .ToArray()
                });
        }
        var headerNames = headerNamesString.Split(",").ToList();
        var headerMappings = await ReadHeaderMappingsAsync(headerRange, headerNames, cancellationToken);

        var request = _sheetsService.Value.Spreadsheets.Values.Get(_spreadsheetId, dataRange);
        var response = await request.ExecuteAsync(cancellationToken);
        var values = response.Values;

        var assessments = new List<AssessmentLastResultGoogleSheetResponse>();
        if (values == null) return assessments;
        var listFields = headerNames;
        var listColumnIndexs = listFields
            .ToDictionary(x => x, x => headerMappings.GetValueOrDefault(x, -1));
        if (listColumnIndexs.Any(x => x.Value < 0))
        {

            throw new AppValidationException(
                "Không tìm thấy header key",
                new Dictionary<string, string[]>
                {
                    ["missing_header_key"] = listColumnIndexs
                        .Where(x => x.Value < 0)
                        .Select(x => x.Key)
                        .ToArray()
                }, ProblemCodes.AssessmentSheetGoogleOperationFailed);
        }

        foreach (var row in values)
        {
            var cellValues = listColumnIndexs
                .Select(x => new
                {
                    x.Key,
                    value = row.Count > headerMappings.GetValueOrDefault(x.Key, -1) ? row[headerMappings.GetValueOrDefault(x.Key, -1)]?.ToString() ?? null : null,
                })
                .ToDictionary(x => x.Key, x => x.value);
            assessments.Add(new AssessmentLastResultGoogleSheetResponse(
                cellValues.GetValueOrDefault("item_id", null),
                cellValues.GetValueOrDefault("ma_hs", null),
                cellValues.GetValueOrDefault("ket_qua", null),
                cellValues.GetValueOrDefault("nhom_tuoi", null),
                cellValues.GetValueOrDefault("group_lv2", null),
                cellValues.GetValueOrDefault("group_lv3", null),
                cellValues.GetValueOrDefault("item", null),
                cellValues.GetValueOrDefault("row_index", null),
                cellValues.GetValueOrDefault("ten_hs", null),
                cellValues.GetValueOrDefault("ghi_chu", null)
            ));
        }
        return assessments;
    }


    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Giải phóng đối tượng SheetsService của Google
                if (_sheetsService.IsValueCreated) _sheetsService.Value.Dispose();
                if (_driveService.IsValueCreated) _driveService.Value.Dispose();
            }
            _disposed = true;
        }
    }

    public async Task<GoogleSheetsCredentialSmokeResponse> SmokeTestCredentialAsync(CancellationToken cancellationToken)
    {
        var spreadsheetId = googleSheetsSettings.SpreadsheetId?.Trim();
        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            return new GoogleSheetsCredentialSmokeResponse(
                Success: false,
                IsConfigured: false,
                SpreadsheetId: null,
                SpreadsheetTitle: null,
                FirstSheetTitle: null,
                ReadRange: null,
                ReadRowCount: null,
                ErrorCode: "SpreadsheetIdNotConfigured");
        }

        try
        {
            var request = _sheetsService.Value.Spreadsheets.Get(spreadsheetId);
            request.IncludeGridData = false;
            request.Fields = "spreadsheetId,properties.title,sheets.properties.title";
            var spreadsheet = await request.ExecuteAsync(cancellationToken);

            return new GoogleSheetsCredentialSmokeResponse(
                Success: true,
                IsConfigured: true,
                SpreadsheetId: spreadsheet.SpreadsheetId ?? spreadsheetId,
                SpreadsheetTitle: spreadsheet.Properties?.Title,
                FirstSheetTitle: spreadsheet.Sheets?.FirstOrDefault()?.Properties?.Title,
                ReadRange: null,
                ReadRowCount: null,
                ErrorCode: null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            GoogleSheetsCredentialSmokeFailed(logger, ex.GetType().Name, null);
            return new GoogleSheetsCredentialSmokeResponse(
                Success: false,
                IsConfigured: true,
                SpreadsheetId: spreadsheetId,
                SpreadsheetTitle: null,
                FirstSheetTitle: null,
                ReadRange: null,
                ReadRowCount: null,
                ErrorCode: "GoogleSheetsReadFailed");
        }
    }

    public async Task<SyncAssessmentsFromGoogleSheetsResponse> SyncAssessmentsAsync(SyncAssessmentsFromGoogleSheetsRequest request, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        EnsureAssessmentSyncRole(actor);

        var replacement = request.ReplaceRecordSnapshots;
        if (replacement is not null)
        {
            // Tùy chọn 2 (ghi đè snapshot bản ghi) chỉ dành cho quản trị; teacher chỉ được đồng bộ mặc định.
            if (actor.Role is not (UserRole.SuperAdmin or UserRole.Admin))
                throw new ForbiddenException("Không đủ quyền thay thế snapshot bảng đánh giá.");
            AssessmentSnapshotReplacementRules.Validate(replacement);
        }

        var data = new List<AssessmentLastResultGoogleSheetResponse>();
        var assessments = new List<AssessmentGoogleSheetResponse>();
        try
        {
            assessments = await ReadAssessmentsAsync(cancellationToken);
            data = await ReadAssessmentLatestResultsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new NormalException(
                "Lỗi khi đọc dữ liệu từ Google Sheets.",
                ProblemCodes.AssessmentSheetGoogleOperationFailed,
                new Dictionary<string, object?>
                {
                    { "method", "ReadAssessmentsAsync" },
                    { "exception_message", ex.Message },
                    { "stack_trace", ex.StackTrace }
                }
            );
        }
        var now = timeProvider.GetUtcNow();
        var assessmentToInsert = assessments
            .Where(x => !string.IsNullOrWhiteSpace(x.Item))
            .GroupBy(x => x.RowIndex)
            .Select(x => x.First())
            .Select(x => new Assessment
            {
                Id = Guid.NewGuid(),
                Code = x.ItemId ?? string.Empty,
                Name = AssessmentSyncTextNormalizer.NormalizeRequiredName(x.Item),
                RowIndex = int.TryParse(x.RowIndex, out var rowIndex) ? rowIndex : 0,
                GroupLv1Name = AssessmentSyncTextNormalizer.NormalizeOptionalName(x.NhomTuoi),
                GroupLv2Name = AssessmentSyncTextNormalizer.NormalizeOptionalName(x.GroupLv2),
                GroupLv3Name = AssessmentSyncTextNormalizer.NormalizeOptionalName(x.GroupLv3),
                UpdatedByUserId = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now,
            })
            .ToList();
        // Mã mục đánh giá phải duy nhất để tra cứu AssessmentRecordLatest bên dưới; nếu Sheet có mã trùng, giữ dòng đầu tiên.
        var assessmentByCode = assessmentToInsert
            .GroupBy(x => x.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var studentCodes = data.Select(x => x.MaHs).Distinct(StringComparer.Ordinal).ToList();
        var students = await dbContext.Students.AsNoTracking()
            .Where(x => studentCodes.Contains(x.StudentCode))
            .ToListAsync(cancellationToken);
        var studentByCode = students.ToDictionary(x => x.StudentCode, StringComparer.Ordinal);

        var sheetLatestByStudentId = new Dictionary<Guid, AssessmentSheetLatest>();
        var recordLatestsToInsert = new List<AssessmentRecordLatest>();
        foreach (var row in data)
        {
            if (!studentByCode.TryGetValue(row.MaHs!, out var student))
                continue; // mã học sinh trong _data_DG không khớp Student nào trong hệ thống, bỏ qua dòng này

            if (!sheetLatestByStudentId.TryGetValue(student.Id, out var sheetLatest))
            {
                sheetLatest = new AssessmentSheetLatest
                {
                    Id = Guid.NewGuid(),
                    Name = "Kết quả gần nhất",
                    AssessmentSheetStatus = AssessmentSheetStatus.Open,
                    StudentId = student.Id,
                    StudentSnapshot = new StudentSnapshot
                    {
                        StudentCode = student.StudentCode,
                        FullName = student.FullName,
                        NickName = student.NickName,
                        DateOfBirth = student.DateOfBirth,
                        Gender = student.Gender
                    },
                    CreatedAt = now,
                    UpdatedAt = now
                };
                sheetLatestByStudentId[student.Id] = sheetLatest;
            }

            if (row.KetQua is null)
                continue; // chưa có kết quả cho mục này, chỉ cần tạo AssessmentSheetLatest cho học sinh
            if (!assessmentByCode.TryGetValue(row.ItemId!, out var assessment))
                continue; // mã mục đánh giá không khớp Assessment nào vừa đồng bộ, bỏ qua dòng này
            if (!AssessmentSheetRules.TryParseGradeLabel(row.KetQua, out var grade))
                continue; // nhãn kết quả không khớp bảng mapping đã xác nhận, bỏ qua dòng này

            recordLatestsToInsert.Add(new AssessmentRecordLatest
            {
                Id = Guid.NewGuid(),
                AssessmentSheetLatestId = sheetLatest.Id,
                AssessmentSheetLatest = sheetLatest,
                AssessmentId = assessment.Id,
                Assessment = assessment,
                LatestGrade = grade,
                Note = row.GhiChu,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        try
        {
            // Xoá theo thứ tự con trước cha (AssessmentRecordLatest FK Restrict tới cả Assessment lẫn AssessmentSheetLatest).
            await dbContext.AssessmentRecordLatests.ExecuteDeleteAsync(cancellationToken);
            await dbContext.AssessmentSheetLatests.ExecuteDeleteAsync(cancellationToken);
            await dbContext.Assessments.ExecuteDeleteAsync(cancellationToken);

            await dbContext.Assessments.AddRangeAsync(assessmentToInsert, cancellationToken);
            await dbContext.AssessmentSheetLatests.AddRangeAsync(sheetLatestByStudentId.Values, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await ((DbContext)dbContext).BulkInsertAsync(recordLatestsToInsert, cancellationToken: cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new ConflictException(
                "Lỗi khi xử lý database",
                "InsertDatabase",
                new Dictionary<string, object?>
                {
                    { "exception_message", ex.Message },
                    { "entries", ex.Entries },
                    { "data", ex.Data },
                    { "stack_trace", ex.StackTrace }
                }
            );
        }
        catch (Exception ex)
        {
            throw new ConflictException(
                "Lỗi chưa biết khi xử lý database",
                "InsertDatabase",
                new Dictionary<string, object?>
                {
                    { "exception_message", ex.Message },
                    { "stack_trace", ex.StackTrace }
                }
            );
        }

        var replacedRecordSnapshots = 0;
        if (replacement is not null)
        {
            var statuses = replacement.SheetStatuses!.Distinct().ToArray();
            var recordsInScope = await dbContext.AssessmentRecords
                .Include(x => x.AssessmentSheet)
                .Where(x => statuses.Contains(x.AssessmentSheet.AssessmentSheetStatus))
                .ToListAsync(cancellationToken);

            replacedRecordSnapshots = AssessmentSnapshotReplacementRules.Apply(
                recordsInScope, assessmentByCode, replacement, now, actor.UserId);

            if (replacedRecordSnapshots > 0)
                await dbContext.SaveChangesAsync(cancellationToken);
        }

        var response = new SyncAssessmentsFromGoogleSheetsResponse(
            SheetsTotalRows: data.Count,
            DatabaseTotalRows: sheetLatestByStudentId.Count,
            InsertedRows: assessmentToInsert.Count,
            UpdatedRows: recordLatestsToInsert.Count,
            DeletedRows: 0,
            ReplacedRecordSnapshots: replacedRecordSnapshots
        );

        AddGoogleSheetsAudit(actor, "GoogleSheets.AssessmentsSynced", null, new
        {
            googleSheetsSettings.SpreadsheetId,
            response.SheetsTotalRows,
            response.DatabaseTotalRows,
            response.InsertedRows,
            response.UpdatedRows,
            response.DeletedRows,
            AssessmentsReadRows = assessments.Count,
            LatestResultReadRows = data.Count,
            StudentLatestMirrorCount = sheetLatestByStudentId.Count,
            AssessmentInsertCount = assessmentToInsert.Count,
            RecordLatestInsertOrUpdateCount = recordLatestsToInsert.Count,
            response.ReplacedRecordSnapshots,
            ReplaceRecordSnapshotFields = replacement is null
                ? null
                : new
                {
                    replacement.Name,
                    replacement.GroupLv1Name,
                    replacement.GroupLv2Name,
                    replacement.GroupLv3Name,
                    replacement.RowIndex
                },
            ReplaceRecordSnapshotSheetStatuses = replacement?.SheetStatuses?.Select(x => x.ToString()).ToArray(),
            ActorUserId = actor.UserId,
            ActorRole = actor.Role.ToString(),
            SyncedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return response;
    }

    /// <summary>
    /// Id thư mục Drive riêng của học sinh — nhập thủ công ở UI quản lý Student (Student.DriveFolderId),
    /// backend chỉ đọc, không tự tạo. Flow upload PDF của AssessmentSheet yêu cầu có folder này.
    /// </summary>
    private async Task<string?> GetStudentDriveFolderIdAsync(Guid studentId, CancellationToken cancellationToken) =>
        await dbContext.Students.AsNoTracking()
            .Where(x => x.Id == studentId)
            .Select(x => x.DriveFolderId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<string> UploadAssessmentSheetPlanPdfAsync(
        Guid assessmentSheetId, Guid studentId, string? existingFileLink, string fileName,
        byte[] content, CancellationToken cancellationToken) =>
        await SavePdfToDriveAsync(
            studentId,
            existingFileLink,
            assessmentSheetId,
            NormalizePdfFileName(fileName),
            content,
            cancellationToken,
            requireStudentFolder: true);

    public async Task<string> UploadAssessmentSheetResultPdfAsync(
        Guid assessmentSheetId, Guid studentId, string? existingFileLink, string fileName,
        byte[] content, CancellationToken cancellationToken) =>
        await SavePdfToDriveAsync(
            studentId,
            existingFileLink,
            assessmentSheetId,
            NormalizePdfFileName(fileName),
            content,
            cancellationToken,
            requireStudentFolder: true);

    public async Task<IReadOnlyList<ResultSourceCellUpdate>> WriteFinalGradesToSourceSheetAsync(
        string studentCode,
        IReadOnlyList<AssessmentRecord> records,
        CancellationToken cancellationToken)
    {
        var changeSet = await ResolveResultSourceChangesAsync(studentCode, records, cancellationToken);
        if (changeSet.ChangedUpdates.Count == 0)
            return [];

        try
        {
            var batchRequest = new Google.Apis.Sheets.v4.Data.BatchUpdateValuesRequest
            {
                ValueInputOption = "USER_ENTERED",
                Data = changeSet.ChangedUpdates
                    .Select(x => new Google.Apis.Sheets.v4.Data.ValueRange
                    {
                        Range = x.Range,
                        Values = [[x.NewValue]]
                    })
                    .ToList()
            };
            await _sheetsService.Value.Spreadsheets.Values.BatchUpdate(batchRequest, changeSet.SpreadsheetId).ExecuteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw GoogleOperationFailed("Lỗi khi ghi kết quả vào [F0.ĐG].", ex);
        }

        return changeSet.ChangedUpdates
            .Select(x => MapResultSourceCellUpdate(changeSet.SpreadsheetId, changeSet.SheetName, studentCode, x))
            .ToList();
    }

    // Bản dry-run của WriteFinalGradesToSourceSheetAsync: đọc + đối chiếu [F0.ĐG] để lấy đúng tập ô sẽ thay đổi
    // (kèm giá trị hiện tại) nhưng KHÔNG ghi. Dùng cho popup xác nhận trước khi submit kết quả.
    public async Task<IReadOnlyList<ResultSourceCellUpdate>> PreviewFinalGradesToSourceSheetAsync(
        string studentCode,
        IReadOnlyList<AssessmentRecord> records,
        CancellationToken cancellationToken)
    {
        var changeSet = await ResolveResultSourceChangesAsync(studentCode, records, cancellationToken);
        return changeSet.ChangedUpdates
            .Select(x => MapResultSourceCellUpdate(changeSet.SpreadsheetId, changeSet.SheetName, studentCode, x))
            .ToList();
    }

    private static ResultSourceCellUpdate MapResultSourceCellUpdate(
        string spreadsheetId,
        string sheetName,
        string studentCode,
        PendingResultSourceUpdate x) =>
        new(
            SpreadsheetId: spreadsheetId,
            SheetName: sheetName,
            Cell: x.Cell,
            Row: x.Row,
            Column: x.Column,
            Kind: x.Kind,
            CurrentValue: x.CurrentValue,
            NewValue: x.NewValue,
            StudentCode: studentCode,
            AssessmentCode: x.Record.AssessmentSnapshot.Code,
            AssessmentName: x.Record.AssessmentSnapshot.Name,
            FinalGrade: x.Record.FinalGrade,
            FinalGradeLabel: x.Record.FinalGrade is null ? null : AssessmentSheetRules.GradeLabel(x.Record.FinalGrade.Value),
            FinalNote: x.Record.FinalNote);

    private async Task<ResultSourceChangeSet> ResolveResultSourceChangesAsync(
        string studentCode,
        IReadOnlyList<AssessmentRecord> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
            return new ResultSourceChangeSet(string.Empty, string.Empty, []);

        List<string?> itemCodes;
        List<string?> studentCodes;

        var _spreadsheetId = googleSheetsSettings.SpreadsheetId;
        var latestResultConfig = await GetResultLatestSheetConfig(cancellationToken);
        var _resultSource_AssessmentCodeRange = latestResultConfig.GetValueOrDefault(ResultSource_AssessmentCodeRange, string.Empty);
        var _resultSource_StudentCodeRange = latestResultConfig.GetValueOrDefault(ResultSource_StudentCodeRange, string.Empty);
        var _resultSource_FirstStudentColumnIndexString = latestResultConfig.GetValueOrDefault(ResultSource_FirstStudentColumnIndex, string.Empty);
        var _resultSource_FirstDataRowString = latestResultConfig.GetValueOrDefault(ResultSource_FirstDataRow, string.Empty);
        var _resultSource_SheetName = latestResultConfig.GetValueOrDefault(ResultSource_SheetName, string.Empty);
        var checkEmpties = new Dictionary<string, string>
        {
            [ResultSource_SheetName] = _resultSource_SheetName,
            [ResultSource_AssessmentCodeRange] = _resultSource_AssessmentCodeRange,
            [ResultSource_StudentCodeRange] = _resultSource_StudentCodeRange,
            [ResultSource_FirstStudentColumnIndex] = _resultSource_FirstStudentColumnIndexString,
            [ResultSource_FirstDataRow] = _resultSource_FirstDataRowString,
        };
        if (checkEmpties.Any(x => string.IsNullOrWhiteSpace(x.Value)))
        {

            throw new AppValidationException(
                "Không tìm thấy sheet config key/value.",
                new Dictionary<string, string[]>
                {
                    ["missing_config_keyvalue"] = checkEmpties
                        .Where(x => string.IsNullOrWhiteSpace(x.Value))
                        .Select(x => x.Key)
                        .ToArray()
                });
        }
        if (!int.TryParse(_resultSource_FirstStudentColumnIndexString, out int _resultSource_FirstStudentColumnIndex) ||
            !int.TryParse(_resultSource_FirstDataRowString, out int _resultSource_FirstDataRow))
        {
            throw new AppValidationException(
                "Sheet config ResultSource khÃ´ng há»£p lá»‡.",
                new Dictionary<string, string[]>
                {
                    ["invalid_config_keyvalue"] =
                    [
                        ResultSource_FirstStudentColumnIndex,
                        ResultSource_FirstDataRow
                    ]
                });
        }
        try
        {
            var itemCodesResponse = await _sheetsService.Value.Spreadsheets.Values
                .Get(_spreadsheetId, _resultSource_AssessmentCodeRange)
                .ExecuteAsync(cancellationToken);
            itemCodes = (itemCodesResponse.Values ?? [])
                .Select(row => row.Count > 0 ? row[0]?.ToString() : null)
                .ToList();

            var studentCodesResponse = await _sheetsService.Value.Spreadsheets.Values
                .Get(_spreadsheetId, _resultSource_StudentCodeRange)
                .ExecuteAsync(cancellationToken);
            var studentRow = studentCodesResponse.Values?.FirstOrDefault() ?? [];
            studentCodes = studentRow.Select(v => v?.ToString()).ToList();
        }
        catch (Exception ex)
        {
            throw GoogleOperationFailed("Lỗi khi đọc vị trí mã mục đánh giá/mã học sinh trong [F0.ĐG].", ex);
        }

        var gradeColumnIndex = GoogleSheetsGridLocator.FindAbsoluteColumnIndex(studentCodes, studentCode, _resultSource_FirstStudentColumnIndex)
            ?? throw GoogleOperationFailed(
                $"Không tìm thấy mã học sinh '{studentCode}' trong hàng {_resultSource_FirstDataRow} của sheet {_resultSource_SheetName}.");
        var gradeColumnLetter = GoogleSheetsGridLocator.ColumnIndexToLetter(gradeColumnIndex);
        var noteColumnIndex = gradeColumnIndex + 1;
        var noteColumnLetter = GoogleSheetsGridLocator.ColumnIndexToLetter(noteColumnIndex);
        var noteColumnOffset = noteColumnIndex - _resultSource_FirstStudentColumnIndex;
        var noteHeaderValue = noteColumnOffset >= 0 && noteColumnOffset < studentCodes.Count
            ? studentCodes[noteColumnOffset]
            : null;
        if (!string.IsNullOrWhiteSpace(noteHeaderValue))
        {
            throw GoogleOperationFailed(
                $"Cột ghi chú kế bên cột kết quả của học sinh '{studentCode}' phải để trống ở hàng định vị mã học sinh.");
        }

        var pendingUpdates = new List<PendingResultSourceUpdate>();
        var notFound = new List<string>();
        var sheetPrefix = QuoteSheetName(_resultSource_SheetName);
        foreach (var record in records)
        {
            var row = GoogleSheetsGridLocator.FindAbsoluteRow(itemCodes, record.AssessmentSnapshot.Code, _resultSource_FirstDataRow);
            if (row is null)
            {
                notFound.Add(record.AssessmentSnapshot.Code);
                continue;
            }

            var finalGradeLabel = record.FinalGrade is null
                ? string.Empty
                : AssessmentSheetRules.GradeLabel(record.FinalGrade.Value);
            var finalNote = record.FinalNote ?? string.Empty;
            pendingUpdates.Add(new PendingResultSourceUpdate(
                Record: record,
                Row: row.Value,
                ColumnIndex: gradeColumnIndex,
                Column: gradeColumnLetter,
                Cell: $"{gradeColumnLetter}{row}",
                Range: $"{sheetPrefix}{gradeColumnLetter}{row}",
                Kind: "FinalGrade",
                NewValue: finalGradeLabel));
            pendingUpdates.Add(new PendingResultSourceUpdate(
                Record: record,
                Row: row.Value,
                ColumnIndex: noteColumnIndex,
                Column: noteColumnLetter,
                Cell: $"{noteColumnLetter}{row}",
                Range: $"{sheetPrefix}{noteColumnLetter}{row}",
                Kind: "FinalNote",
                NewValue: finalNote));
        }

        if (notFound.Count > 0)
            throw GoogleOperationFailed($"Không tìm thấy mã mục đánh giá trong sheet {_resultSource_SheetName}: {string.Join(", ", notFound)}.");
        if (pendingUpdates.Count == 0)
            return new ResultSourceChangeSet(_spreadsheetId, _resultSource_SheetName, []);

        try
        {
            var batchGetRequest = _sheetsService.Value.Spreadsheets.Values.BatchGet(_spreadsheetId);
            batchGetRequest.Ranges = pendingUpdates.Select(x => x.Range).ToList();
            var currentValuesResponse = await batchGetRequest.ExecuteAsync(cancellationToken);
            var currentValuesByRange = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var i = 0; i < pendingUpdates.Count; i++)
            {
                var valueRange = currentValuesResponse.ValueRanges is not null && i < currentValuesResponse.ValueRanges.Count
                    ? currentValuesResponse.ValueRanges[i]
                    : null;
                currentValuesByRange[pendingUpdates[i].Range] = valueRange?.Values?.FirstOrDefault()?.FirstOrDefault()?.ToString();
            }

            var changedUpdates = new List<PendingResultSourceUpdate>();
            foreach (var pendingUpdate in pendingUpdates)
            {
                currentValuesByRange.TryGetValue(pendingUpdate.Range, out var currentValue);
                currentValue ??= string.Empty;
                if (string.Equals(currentValue, pendingUpdate.NewValue, StringComparison.Ordinal))
                    continue;

                changedUpdates.Add(pendingUpdate with { CurrentValue = currentValue });
            }

            return new ResultSourceChangeSet(_spreadsheetId, _resultSource_SheetName, changedUpdates);
        }
        catch (Exception ex)
        {
            throw GoogleOperationFailed("Lỗi khi đọc và đối chiếu dữ liệu [F0.ĐG].", ex);
        }
    }

    // Không lưu PDF xuống đĩa cục bộ — luôn tạo file THẬT MỚI trên Google Drive rồi trả về webViewLink.
    // Cơ chế đã chốt (ASH-FB-W1 / G8): không dùng Files.Update để đè nội dung file cũ; thay vào đó tạo
    // file mới xong rồi mới xóa file cũ theo ID (nếu có). Thứ tự này tránh mất cả hai khi lỗi giữa chừng;
    // file cũ đã bị xóa tay trên Drive thì bỏ qua lỗi not-found.
    private async Task<string> SavePdfToDriveAsync(
        Guid studentId, string? existingFileLink, Guid assessmentSheetId, string fileName, byte[] content,
        CancellationToken cancellationToken, bool requireStudentFolder = false)
    {
        var existingFileId = ExtractDriveFileId(existingFileLink);
        Google.Apis.Drive.v3.Data.File file;
        try
        {
            using var stream = new MemoryStream(content);
            var folderId = await GetStudentDriveFolderIdAsync(studentId, cancellationToken);
            if (requireStudentFolder && string.IsNullOrWhiteSpace(folderId))
            {
                throw new ConflictException(
                    "Học sinh chưa có Drive folder id, không thể tạo PDF kế hoạch lên Google Drive.",
                    ProblemCodes.StudentDriveFolderRequired);
            }
            var metadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = $"{fileName}",
                MimeType = "application/pdf",
                Parents = folderId is null ? null : [folderId]
            };
            var createRequest = _driveService.Value.Files.Create(metadata, stream, "application/pdf");
            createRequest.Fields = "id, webViewLink";
            var progress = await createRequest.UploadAsync(cancellationToken);
            if (progress.Status != Google.Apis.Upload.UploadStatus.Completed)
                throw progress.Exception ?? new InvalidOperationException("Tải PDF lên Drive thất bại.");
            file = createRequest.ResponseBody;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw GoogleOperationFailed("Lỗi khi lưu PDF lên Google Drive.", ex);
        }

        // File mới đã tạo xong — giờ mới xóa file cũ. Lỗi ở bước này không làm hỏng kết quả upload:
        // đã có link mới hợp lệ, chỉ còn rủi ro để lại 1 file rác nếu xóa thất bại vì lý do khác not-found.
        if (existingFileId is not null && !string.Equals(existingFileId, file.Id, StringComparison.Ordinal))
            await TryDeleteDriveFileAsync(existingFileId, cancellationToken);

        return file.WebViewLink ?? $"https://drive.google.com/file/d/{file.Id}/view";
    }

    public async Task<DriveFileContent> DownloadAssessmentSheetPdfAsync(string fileLink, CancellationToken cancellationToken)
    {
        var fileId = ExtractDriveFileId(fileLink)
            ?? throw GoogleOperationFailed("Link PDF trên Google Drive không hợp lệ.");
        try
        {
            var metadataRequest = _driveService.Value.Files.Get(fileId);
            metadataRequest.Fields = "name";
            var metadata = await metadataRequest.ExecuteAsync(cancellationToken);

            using var stream = new MemoryStream();
            await _driveService.Value.Files.Get(fileId).DownloadAsync(stream, cancellationToken);

            var name = string.IsNullOrWhiteSpace(metadata.Name) ? $"{fileId}.pdf" : metadata.Name;
            return new DriveFileContent(stream.ToArray(), name);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw GoogleOperationFailed("Lỗi khi tải PDF từ Google Drive.", ex);
        }
    }

    private async Task TryDeleteDriveFileAsync(string fileId, CancellationToken cancellationToken)
    {
        try
        {
            await _driveService.Value.Files.Delete(fileId).ExecuteAsync(cancellationToken);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // File cũ đã bị xóa tay trên Drive — coi như đã xong.
        }
    }

    private static string NormalizePdfFileName(string fileName)
    {
        var name = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? "ke-hoach-ca-nhan.pdf" : fileName.Trim());
        return name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? name : $"{name}.pdf";
    }

    private static string? ExtractDriveFileId(string? webViewLink)
    {
        if (string.IsNullOrWhiteSpace(webViewLink))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(webViewLink, "/d/([a-zA-Z0-9_-]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static void EnsureAssessmentSyncRole(ActorContext actor)
    {
        if (actor.Role is not (UserRole.SuperAdmin or UserRole.Admin or UserRole.Teacher))
            throw new ForbiddenException("Không đủ quyền.");
    }

    private void AddGoogleSheetsAudit(ActorContext actor, string action, Guid? entityId, object? newValue) =>
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor.UserId,
            Action = action,
            EntityType = "GoogleSheets",
            EntityId = entityId,
            OldValues = null,
            NewValues = newValue is null ? null : JsonSerializer.Serialize(newValue),
            IpAddress = actor.IpAddress,
            CreatedAt = timeProvider.GetUtcNow()
        });

    private static string QuoteSheetName(string sheetName) =>
        $"'{sheetName.Replace("'", "''")}'!";

    private sealed record PendingResultSourceUpdate(
        AssessmentRecord Record,
        int Row,
        int ColumnIndex,
        string Column,
        string Cell,
        string Range,
        string Kind,
        string NewValue)
    {
        public string? CurrentValue { get; init; }
    }

    private sealed record ResultSourceChangeSet(
        string SpreadsheetId,
        string SheetName,
        List<PendingResultSourceUpdate> ChangedUpdates);

    private static NormalException GoogleOperationFailed(string message, Exception? exception = null) =>
        new(message, ProblemCodes.AssessmentSheetGoogleOperationFailed, exception is null ? null : new Dictionary<string, object?>
        {
            { "exception_message", exception.Message },
            { "stack_trace", exception.StackTrace }
        });
}
