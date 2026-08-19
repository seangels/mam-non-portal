using System.Text.RegularExpressions;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Domain.Entities;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdminPortal.Application.GoogleSheets;

public class GoogleSheetsSettings : IGoogleSheetsSettings
{
    public string CredentialFilePath { get; set; } = string.Empty;
    public string SpreadsheetId { get; set; } = string.Empty;
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
    private readonly string _spreadsheetId;
    private bool _disposed;
    private readonly IApplicationDbContext dbContext;
    private readonly ICurrentActor currentActor;
    private readonly ILogger<GoogleSheetsService> logger;
    private readonly TimeProvider timeProvider;
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
        _spreadsheetId = configuration.Value.SpreadsheetId ?? "";
        var credentialPath = configuration.Value.CredentialFilePath ?? "google-credentials.json";

        GoogleCredential credential;
        using (var stream = new FileStream(credentialPath, FileMode.Open, FileAccess.Read))
        {
#pragma warning disable CS0618 // Type or member is obsolete
            credential = GoogleCredential.FromStream(stream)
                                         .CreateScoped(SheetsService.Scope.Spreadsheets);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        _sheetsService = new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "CleanArchGoogleSheets"
        });
    }
    async Task<Dictionary<string, int>> ReadHeaderMappingsAsync(string headerRange, List<string> headerNames, CancellationToken cancellationToken)
    {
        var headerMappings = new Dictionary<string, int>();
        var requestHeader = _sheetsService.Spreadsheets.Values.Get(_spreadsheetId, headerRange);
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
        var headerRange = "_data_DG_only_item!A7:F7"; // Adjust the range as needed
        var headerNames = new List<string> {
            "item_id", "item", "nhom_tuoi", "group_lv2", "group_lv3", "row_index"
         };
        var headerMappings = await ReadHeaderMappingsAsync(headerRange, headerNames, cancellationToken);

        var range = "_data_DG_only_item!A10:F"; // Adjust the range as needed
        var request = _sheetsService.Spreadsheets.Values.Get(_spreadsheetId, range);
        var response = await request.ExecuteAsync(cancellationToken);
        var values = response.Values;

        var assessments = new List<AssessmentGoogleSheetResponse>();
        if (values == null) return assessments;

        foreach (var row in values)
        {
            assessments.Add(new AssessmentGoogleSheetResponse(
                row.Count > 0 ? row[headerMappings.GetValueOrDefault("item_id", -1)]?.ToString() ?? "" : "",
                row.Count > 1 ? row[headerMappings.GetValueOrDefault("item", -1)]?.ToString() ?? "" : "",
                row.Count > 2 ? row[headerMappings.GetValueOrDefault("nhom_tuoi", -1)]?.ToString() ?? "" : "",
                row.Count > 3 ? row[headerMappings.GetValueOrDefault("group_lv2", -1)]?.ToString() ?? "" : "",
                row.Count > 4 ? row[headerMappings.GetValueOrDefault("group_lv3", -1)]?.ToString() ?? "" : "",
                row.Count > 5 ? row[headerMappings.GetValueOrDefault("row_index", -1)]?.ToString() ?? "" : ""
            ));
        }
        return assessments;
    }
    // 2. Hiện thực hàm Dispose để giải phóng _sheetsService
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Giải phóng đối tượng SheetsService của Google
                _sheetsService?.Dispose();
            }
            _disposed = true;
        }
    }

    public async Task<SyncAssessmentsFromGoogleSheetsResponse> SyncAssessmentsAsync(SyncAssessmentsFromGoogleSheetsRequest request, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        var data = new List<AssessmentGoogleSheetResponse>();
        try
        {
            data = await ReadAssessmentsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new NormalException(
                "Lỗi khi đọc dữ liệu từ Google Sheets.",
                "ReadFromGoogleSheets",
                new Dictionary<string, object?>
                {
                    { "exception_message", ex.Message },
                    { "stack_trace", ex.StackTrace }
                }
            );
        }
        var now = timeProvider.GetUtcNow();
        var assessments = data
            .Where(x => !string.IsNullOrWhiteSpace(x.Item))
            .Select(x => new Assessment
            {
                Id = Guid.NewGuid(),
                Code = x.ItemId,
                Name = x.Item.Trim(),
                RowIndex = int.TryParse(x.RowIndex, out var rowIndex) ? rowIndex : 0,
                GroupLv1Name = x.NhomTuoi.Trim(),
                GroupLv2Name = x.GroupLv2.Trim(),
                GroupLv3Name = x.GroupLv3.Trim(),
                UpdatedByUserId = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now,
            })
            .ToList();
       
        var assessmentToInsert = assessments;
        try
        {
            await dbContext.Assessments.ExecuteDeleteAsync(cancellationToken);
            await dbContext.Assessments.AddRangeAsync(assessmentToInsert, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
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


        // Implement the logic to sync assessments
        return new SyncAssessmentsFromGoogleSheetsResponse(
            SheetsTotalRows: data.Count,
            DatabaseTotalRows: 0, // Replace with actual count from the database
            InsertedRows: assessmentToInsert.Count,
            UpdatedRows: 0, // Replace with actual updated count
            DeletedRows: 0 // Replace with actual deleted count
        );
    }
}