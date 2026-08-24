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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EFCore.BulkExtensions;

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
    public string SpreadsheetId { get; set; } = string.Empty;
    public string SheetConfigRange { get; set; } = string.Empty;
    public string SheetConfigLastRow { get; set; } = string.Empty;
    public string AssessmentSheetTemplateFileId { get; set; } = string.Empty;

    public string DataSheetName { get; set; } = "data";
    public string PlanTemplateSheetName { get; set; } = "khcn_template";
    public long PlanTemplateSheetGid { get; set; } = 1320805599;
    public string ResultTemplateSheetName { get; set; } = "KQ_template";
    public long ResultTemplateSheetGid { get; set; } = 1903920808;

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
    private readonly SheetsService _sheetsService;
    private readonly DriveService _driveService;
    private readonly GoogleCredential _credential;
    private readonly HttpClient _httpClient;

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

        var credentialPath = googleSheetsSettings.CredentialFilePath ?? "google-credentials.json";

        using (var stream = new FileStream(credentialPath, FileMode.Open, FileAccess.Read))
        {
#pragma warning disable CS0618 // Type or member is obsolete
            _credential = GoogleCredential.FromStream(stream)
                                         .CreateScoped(SheetsService.Scope.Spreadsheets, DriveService.Scope.Drive);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        _sheetsService = new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = _credential,
            ApplicationName = "CleanArchGoogleSheets"
        });
        _driveService = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = _credential,
            ApplicationName = "CleanArchGoogleSheets"
        });
        _httpClient = new HttpClient();
    }
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

        var requestConfigLastRow = _sheetsService.Spreadsheets.Values
            .Get(settings.SpreadsheetId, settings.SheetConfigLastRow);
        var responseConfigLastRow = await requestConfigLastRow.ExecuteAsync(cancellationToken);
        var configValuesLastRow = responseConfigLastRow.Values;
        var _SheetConfigRange = settings.SheetConfigRange.Replace("{{lastRow}}", configValuesLastRow[0][0].ToString());
        var requestConfig = _sheetsService.Spreadsheets.Values
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
        var requestHeader = _sheetsService.Spreadsheets.Values.Get(googleSheetsSettings.SpreadsheetId, headerRange);
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

        var request = _sheetsService.Spreadsheets.Values.Get(_spreadsheetId, dataRange);
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

        var request = _sheetsService.Spreadsheets.Values.Get(_spreadsheetId, dataRange);
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
                _sheetsService?.Dispose();
                _driveService?.Dispose();
                _httpClient?.Dispose();
            }
            _disposed = true;
        }
    }

    public async Task<SyncAssessmentsFromGoogleSheetsResponse> SyncAssessmentsAsync(SyncAssessmentsFromGoogleSheetsRequest request, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        EnsureAssessmentSyncRole(actor);
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
                Name = (x.Item ?? string.Empty).Trim(),
                RowIndex = int.TryParse(x.RowIndex, out var rowIndex) ? rowIndex : 0,
                GroupLv1Name = x.NhomTuoi?.Trim(),
                GroupLv2Name = x.GroupLv2?.Trim(),
                GroupLv3Name = x.GroupLv3?.Trim(),
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

        return new SyncAssessmentsFromGoogleSheetsResponse(
            SheetsTotalRows: data.Count,
            DatabaseTotalRows: sheetLatestByStudentId.Count,
            InsertedRows: assessmentToInsert.Count,
            UpdatedRows: recordLatestsToInsert.Count,
            DeletedRows: 0
        );
    }

    public async Task<string> EnsureAssessmentSheetSpreadsheetAsync(AssessmentSheet sheet, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(sheet.AssessmentSheetSpreadsheetId))
            return sheet.AssessmentSheetSpreadsheetId;
        var _assessmentSheetTemplateFileId = googleSheetsSettings.AssessmentSheetTemplateFileId;
        if (string.IsNullOrWhiteSpace(_assessmentSheetTemplateFileId))
            throw GoogleOperationFailed("Chưa cấu hình GoogleSheets:AssessmentSheetTemplateFileId.");

        var folderId = await GetStudentDriveFolderIdAsync(sheet.StudentId, cancellationToken);

        Google.Apis.Drive.v3.Data.File copy;
        try
        {
            var body = new Google.Apis.Drive.v3.Data.File
            {
                Name = $"{sheet.StudentSnapshot.StudentCode}.{sheet.StudentSnapshot.FullName}_{sheet.Name}",
                Parents = folderId is null ? null : [folderId]
            };
            copy = await _driveService.Files.Copy(body, _assessmentSheetTemplateFileId).ExecuteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw GoogleOperationFailed("Lỗi khi copy file mẫu gen_assessment_sheet trên Drive để tạo [F01].", ex);
        }

        sheet.AssessmentSheetSpreadsheetId = copy.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        return copy.Id;
    }

    /// <summary>
    /// Id thư mục Drive riêng của học sinh — nhập thủ công ở UI quản lý Student (Student.DriveFolderId),
    /// backend chỉ đọc, không tự tạo. Null nếu chưa nhập, khi đó file sẽ tạo ở vị trí mặc định của Drive API
    /// (thư mục cha của file mẫu khi copy [F01], gốc Drive của service account khi tạo PDF mới).
    /// </summary>
    private async Task<string?> GetStudentDriveFolderIdAsync(Guid studentId, CancellationToken cancellationToken) =>
        await dbContext.Students.AsNoTracking()
            .Where(x => x.Id == studentId)
            .Select(x => x.DriveFolderId)
            .SingleOrDefaultAsync(cancellationToken);

    // TẠM/CHƯA XÁC NHẬN: định dạng cột dưới đây là suy đoán hợp lý, chưa có mapping thật từ đội vận hành
    // (xem requirements 09 mục 15 — "Định dạng chi tiết từng cột... sẽ được bổ sung sau"). Sửa lại theo
    // mapping thật khi có, đừng coi đây là nguồn sự thật lâu dài.
    public Task WriteAssessmentSheetDataAsync(string spreadsheetId, IReadOnlyList<AssessmentRecord> records, CancellationToken cancellationToken) =>
        WriteRecordsToSheetAsync(spreadsheetId, googleSheetsSettings.DataSheetName, records, includePlan: true, includeFinal: true, cancellationToken);

    public async Task<string> GenerateAssessmentSheetPlanPdfAsync(
        string spreadsheetId, Guid assessmentSheetId, Guid studentId, string? existingFileLink,
        IReadOnlyList<AssessmentRecord> records, CancellationToken cancellationToken)
    {
        await WriteRecordsToSheetAsync(spreadsheetId, googleSheetsSettings.PlanTemplateSheetName, records, includePlan: true, includeFinal: false, cancellationToken);
        var bytes = await ExportSheetToPdfAsync(spreadsheetId, googleSheetsSettings.PlanTemplateSheetGid, cancellationToken);
        return await SavePdfToDriveAsync(studentId, existingFileLink, assessmentSheetId, "plan.pdf", bytes, cancellationToken);
    }

    public async Task<string> GenerateAssessmentSheetResultPdfAsync(
        string spreadsheetId, Guid assessmentSheetId, Guid studentId, string? existingFileLink,
        IReadOnlyList<AssessmentRecord> records, CancellationToken cancellationToken)
    {
        await WriteRecordsToSheetAsync(spreadsheetId, googleSheetsSettings.ResultTemplateSheetName, records, includePlan: false, includeFinal: true, cancellationToken);
        var bytes = await ExportSheetToPdfAsync(spreadsheetId, googleSheetsSettings.ResultTemplateSheetGid, cancellationToken);
        return await SavePdfToDriveAsync(studentId, existingFileLink, assessmentSheetId, "result.pdf", bytes, cancellationToken);
    }

    public async Task WriteFinalGradesToSourceSheetAsync(string studentCode, IReadOnlyList<AssessmentRecord> records, CancellationToken cancellationToken)
    {
        var toWrite = records.Where(x => x.FinalGrade is not null).ToList();
        if (toWrite.Count == 0)
            return;

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
        _ = int.TryParse(_resultSource_FirstStudentColumnIndexString, out int _resultSource_FirstStudentColumnIndex);
        _ = int.TryParse(_resultSource_FirstDataRowString, out int _resultSource_FirstDataRow);
        try
        {
            var itemCodesResponse = await _sheetsService.Spreadsheets.Values
                .Get(_spreadsheetId, _resultSource_AssessmentCodeRange)
                .ExecuteAsync(cancellationToken);
            itemCodes = (itemCodesResponse.Values ?? [])
                .Select(row => row.Count > 0 ? row[0]?.ToString() : null)
                .ToList();

            var studentCodesResponse = await _sheetsService.Spreadsheets.Values
                .Get(_spreadsheetId, _resultSource_StudentCodeRange)
                .ExecuteAsync(cancellationToken);
            var studentRow = studentCodesResponse.Values?.FirstOrDefault() ?? [];
            studentCodes = studentRow.Select(v => v?.ToString()).ToList();
        }
        catch (Exception ex)
        {
            throw GoogleOperationFailed("Lỗi khi đọc vị trí mã mục đánh giá/mã học sinh trong [F0.ĐG].", ex);
        }

        var columnIndex = GoogleSheetsGridLocator.FindAbsoluteColumnIndex(studentCodes, studentCode, _resultSource_FirstStudentColumnIndex)
            ?? throw GoogleOperationFailed(
                $"Không tìm thấy mã học sinh '{studentCode}' trong hàng {_resultSource_FirstDataRow} của sheet {_resultSource_SheetName}.");
        var columnLetter = GoogleSheetsGridLocator.ColumnIndexToLetter(columnIndex);

        var updates = new List<Google.Apis.Sheets.v4.Data.ValueRange>();
        var notFound = new List<string>();
        foreach (var record in toWrite)
        {
            var row = GoogleSheetsGridLocator.FindAbsoluteRow(itemCodes, record.AssessmentSnapshot.Code, _resultSource_FirstDataRow);
            if (row is null)
            {
                notFound.Add(record.AssessmentSnapshot.Code);
                continue;
            }

            updates.Add(new Google.Apis.Sheets.v4.Data.ValueRange
            {
                Range = $"{_resultSource_SheetName}!{columnLetter}{row}",
                Values = [[AssessmentSheetRules.GradeLabel(record.FinalGrade!.Value)]]
            });
        }

        if (notFound.Count > 0)
            throw GoogleOperationFailed($"Không tìm thấy mã mục đánh giá trong sheet {_resultSource_SheetName}: {string.Join(", ", notFound)}.");

        try
        {
            var batchRequest = new Google.Apis.Sheets.v4.Data.BatchUpdateValuesRequest
            {
                ValueInputOption = "USER_ENTERED",
                Data = updates
            };
            await _sheetsService.Spreadsheets.Values.BatchUpdate(batchRequest, _spreadsheetId).ExecuteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw GoogleOperationFailed("Lỗi khi ghi kết quả vào [F0.ĐG].", ex);
        }
    }

    private async Task WriteRecordsToSheetAsync(
        string spreadsheetId,
        string sheetName,
        IReadOnlyList<AssessmentRecord> records,
        bool includePlan,
        bool includeFinal,
        CancellationToken cancellationToken)
    {
        var rows = new List<IList<object>> { BuildHeaderRow(includePlan, includeFinal) };
        foreach (var record in records.OrderBy(x => x.AssessmentRowIndex ?? int.MaxValue))
        {
            var row = new List<object>
            {
                record.AssessmentSnapshot.Code,
                record.AssessmentSnapshot.Name,
                record.AssessmentSnapshot.GroupLv1Name ?? "",
                record.AssessmentSnapshot.GroupLv2Name ?? "",
                record.AssessmentSnapshot.GroupLv3Name ?? ""
            };
            if (includePlan)
            {
                row.Add(record.PlanGrade is null ? "" : AssessmentSheetRules.GradeLabel(record.PlanGrade.Value));
                row.Add(record.PlanNote ?? "");
            }
            if (includeFinal)
            {
                row.Add(record.FinalGrade is null ? "" : AssessmentSheetRules.GradeLabel(record.FinalGrade.Value));
                row.Add(record.FinalNote ?? "");
            }
            rows.Add(row);
        }

        try
        {
            await _sheetsService.Spreadsheets.Values
                .Clear(new Google.Apis.Sheets.v4.Data.ClearValuesRequest(), spreadsheetId, $"{sheetName}!A1:Z2000")
                .ExecuteAsync(cancellationToken);

            var updateRequest = _sheetsService.Spreadsheets.Values.Update(
                new Google.Apis.Sheets.v4.Data.ValueRange { Values = rows }, spreadsheetId, $"{sheetName}!A1");
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await updateRequest.ExecuteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw GoogleOperationFailed($"Lỗi khi ghi dữ liệu vào sheet {sheetName} của file [F01].", ex);
        }
    }

    private static List<object> BuildHeaderRow(bool includePlan, bool includeFinal)
    {
        var header = new List<object> { "Mã mục", "Tên mục", "Nhóm 1", "Nhóm 2", "Nhóm 3" };
        if (includePlan) { header.Add("Kế hoạch"); header.Add("Ghi chú kế hoạch"); }
        if (includeFinal) { header.Add("Kết quả"); header.Add("Ghi chú kết quả"); }
        return header;
    }

    private async Task<byte[]> ExportSheetToPdfAsync(string spreadsheetId, long gid, CancellationToken cancellationToken)
    {
        try
        {
            var accessToken = await ((ITokenAccess)_credential).GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://docs.google.com/spreadsheets/d/{spreadsheetId}/export?format=pdf&gid={gid}&portrait=true&size=A4");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw GoogleOperationFailed("Lỗi khi export PDF từ Google Sheets/Drive.", ex);
        }
    }

    // Không lưu PDF xuống đĩa cục bộ — luôn tạo/cập nhật file thật trên Google Drive, trả về webViewLink.
    // Nếu đã có link cũ (regenerate), cập nhật đè lên đúng file Drive đó (Files.Update) thay vì tạo file mới,
    // tránh tích luỹ file rác trên Drive mỗi lần bấm sinh lại PDF.
    private async Task<string> SavePdfToDriveAsync(
        Guid studentId, string? existingFileLink, Guid assessmentSheetId, string fileName, byte[] content, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new MemoryStream(content);
            var existingFileId = ExtractDriveFileId(existingFileLink);
            Google.Apis.Drive.v3.Data.File file;

            if (existingFileId is not null)
            {
                var updateRequest = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File(), existingFileId, stream, "application/pdf");
                updateRequest.Fields = "id, webViewLink";
                var progress = await updateRequest.UploadAsync(cancellationToken);
                if (progress.Status != Google.Apis.Upload.UploadStatus.Completed)
                    throw progress.Exception ?? new InvalidOperationException("Cập nhật PDF trên Drive thất bại.");
                file = updateRequest.ResponseBody;
            }
            else
            {
                var folderId = await GetStudentDriveFolderIdAsync(studentId, cancellationToken);
                var metadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = $"{assessmentSheetId}-{fileName}",
                    MimeType = "application/pdf",
                    Parents = folderId is null ? null : [folderId]
                };
                var createRequest = _driveService.Files.Create(metadata, stream, "application/pdf");
                createRequest.Fields = "id, webViewLink";
                var progress = await createRequest.UploadAsync(cancellationToken);
                if (progress.Status != Google.Apis.Upload.UploadStatus.Completed)
                    throw progress.Exception ?? new InvalidOperationException("Tải PDF lên Drive thất bại.");
                file = createRequest.ResponseBody;
            }

            return file.WebViewLink ?? $"https://drive.google.com/file/d/{file.Id}/view";
        }
        catch (Exception ex)
        {
            throw GoogleOperationFailed("Lỗi khi lưu PDF lên Google Drive.", ex);
        }
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

    private static NormalException GoogleOperationFailed(string message, Exception? exception = null) =>
        new(message, ProblemCodes.AssessmentSheetGoogleOperationFailed, exception is null ? null : new Dictionary<string, object?>
        {
            { "exception_message", exception.Message },
            { "stack_trace", exception.StackTrace }
        });
}
