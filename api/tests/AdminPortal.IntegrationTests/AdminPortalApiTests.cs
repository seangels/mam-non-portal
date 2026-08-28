using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdminPortal.Application.Assessments;
using AdminPortal.Application.AssessmentSheets;
using AdminPortal.Application.Auth;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.GoogleSheets;
using AdminPortal.Application.StudentGroups;
using AdminPortal.Application.Students;
using AdminPortal.Application.Teachers;
using AdminPortal.Application.Users;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using AdminPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AdminPortal.IntegrationTests;

public sealed class AdminPortalApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] MondayThursday = ["Monday", "Thursday"];
    private static readonly string[] ThursdayMonday = ["Thursday", "Monday"];
    private static readonly string[] Tuesday = ["Tuesday"];
    private static readonly string[] DuplicateMonday = ["Monday", "Monday"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task LogoutWithoutBearerRevokesAccessSession()
    {
        using var client = CreateClient();
        var auth = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);

        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        csrfResponse.EnsureSuccessStatusCode();
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<CsrfTokenResponse>(JsonOptions);
        Assert.Equal(auth.CsrfToken, csrf?.CsrfToken);

        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", auth.CsrfToken);
        var logoutResponse = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var meResponse = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task DevelopmentOpenApiIncludesUnmarkedSummaryAndLegacyHalfDayPart()
    {
        using var developmentFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = developmentFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var document = await client.GetStringAsync("/openapi/v1.json");
        Assert.Contains("Unmarked", document, StringComparison.Ordinal);
        Assert.Contains("unmarked", document, StringComparison.Ordinal);
        Assert.Contains("halfDayPart", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GoogleSheetsCredentialSmokeIsAnonymousAndReturnsSafeMetadata()
    {
        var googleSheets = new FakeGoogleSheetsService
        {
            SmokeResponse = new GoogleSheetsCredentialSmokeResponse(
                Success: true,
                IsConfigured: true,
                SpreadsheetId: "spreadsheet-test-id",
                SpreadsheetTitle: "Configured source",
                FirstSheetTitle: "Data",
                ReadRange: null,
                ReadRowCount: null,
                ErrorCode: null)
        };
        using var smokeFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGoogleSheetsService>();
            services.AddSingleton<IGoogleSheetsService>(googleSheets);
        }));
        using var client = smokeFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        var response = await client.GetAsync("/api/v1/google-sheets/credential-smoke");

        response.EnsureSuccessStatusCode();
        Assert.True(googleSheets.SmokeWasCalled);
        var smoke = await response.Content.ReadFromJsonAsync<GoogleSheetsCredentialSmokeResponse>(JsonOptions);
        Assert.NotNull(smoke);
        Assert.True(smoke.Success);
        Assert.True(smoke.IsConfigured);
        Assert.Equal("spreadsheet-test-id", smoke.SpreadsheetId);
        Assert.Equal("Configured source", smoke.SpreadsheetTitle);
        Assert.Equal("Data", smoke.FirstSheetTitle);
        Assert.Null(smoke.ErrorCode);
    }

    [Fact]
    public async Task AssessmentListWithStudentIdReturnsAllAssessmentsAndLatestRecordColumns()
    {
        using var client = CreateClient();
        var auth = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var studentWithLatest = await CreateStudentAsync(client, $"AL-{marker}", $"Latest {marker}");
        var studentWithoutLatest = await CreateStudentAsync(client, $"AN-{marker}", $"No Latest {marker}");
        var firstAssessmentId = Guid.NewGuid();
        var secondAssessmentId = Guid.NewGuid();
        var latestSheetId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
            var actor = await dbContext.Users.AsNoTracking()
                .SingleAsync(x => x.Email == ApiFactory.SuperAdminEmail);
            var firstAssessment = new Assessment
            {
                Id = firstAssessmentId,
                Code = $"ASM-{marker}-001",
                Name = $"Assessment {marker} A",
                Note = $"Assessment note {marker}",
                RowIndex = 1,
                GroupLv1Name = $"Age {marker}",
                GroupLv2Name = "Group 2",
                GroupLv3Name = "Group 3",
                UpdatedByUserId = actor.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            var secondAssessment = new Assessment
            {
                Id = secondAssessmentId,
                Code = $"ASM-{marker}-002",
                Name = $"Assessment {marker} B",
                Note = null,
                RowIndex = 2,
                GroupLv1Name = $"Age {marker}",
                GroupLv2Name = "Group 2",
                GroupLv3Name = "Group 3",
                UpdatedByUserId = actor.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            var latestSheet = new AssessmentSheetLatest
            {
                Id = latestSheetId,
                Name = "Káº¿t quáº£ gáº§n nháº¥t",
                AssessmentSheetStatus = AssessmentSheetStatus.Open,
                StudentId = studentWithLatest.Id,
                StudentSnapshot = new StudentSnapshot
                {
                    StudentCode = studentWithLatest.StudentCode,
                    FullName = studentWithLatest.FullName,
                    NickName = studentWithLatest.NickName,
                    DateOfBirth = studentWithLatest.DateOfBirth,
                    Gender = studentWithLatest.Gender
                },
                CreatedAt = now,
                UpdatedAt = now
            };
            var latestRecord = new AssessmentRecordLatest
            {
                Id = Guid.NewGuid(),
                AssessmentSheetLatestId = latestSheetId,
                AssessmentSheetLatest = latestSheet,
                AssessmentId = firstAssessmentId,
                Assessment = firstAssessment,
                LatestGrade = AssessmentGrade.B,
                Note = $"Latest note {marker}",
                CreatedAt = now,
                UpdatedAt = now
            };

            await dbContext.Assessments.AddRangeAsync(firstAssessment, secondAssessment);
            await dbContext.AssessmentSheetLatests.AddAsync(latestSheet);
            await dbContext.AssessmentRecordLatests.AddAsync(latestRecord);
            await dbContext.SaveChangesAsync();
        }

        var withLatest = await client.GetFromJsonAsync<PagedResponse<AssessmentListItemResponse>>(
            $"/api/v1/assessments?studentId={studentWithLatest.Id}&search={marker}&page=1&pageSize=10&sortBy=rowindex&sortOrder=asc",
            JsonOptions);
        Assert.NotNull(withLatest);
        Assert.Equal(2, withLatest.Pagination.TotalItems);
        Assert.Equal([firstAssessmentId, secondAssessmentId], withLatest.Items.Select(x => x.Id).ToArray());
        Assert.Equal(AssessmentGrade.B, withLatest.Items[0].LatestGrade);
        Assert.Equal($"Latest note {marker}", withLatest.Items[0].LatestNote);
        Assert.Equal($"Assessment note {marker}", withLatest.Items[0].Note);
        Assert.Null(withLatest.Items[1].LatestGrade);
        Assert.Null(withLatest.Items[1].LatestNote);

        var withoutLatestSheet = await client.GetFromJsonAsync<PagedResponse<AssessmentListItemResponse>>(
            $"/api/v1/assessments?studentId={studentWithoutLatest.Id}&search={marker}&page=1&pageSize=10&sortBy=rowindex&sortOrder=asc",
            JsonOptions);
        Assert.NotNull(withoutLatestSheet);
        Assert.Equal(2, withoutLatestSheet.Pagination.TotalItems);
        Assert.All(withoutLatestSheet.Items, item =>
        {
            Assert.Null(item.LatestGrade);
            Assert.Null(item.LatestNote);
        });
    }

    [Fact]
    public async Task AssessmentSheetCreatePersistsPlanSeedFromSubmittedRecords()
    {
        using var client = CreateClient();
        var auth = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var student = await CreateStudentAsync(client, $"AC-{marker}", $"Create Sheet {marker}");
        var firstAssessmentId = Guid.NewGuid();
        var secondAssessmentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
            var actor = await dbContext.Users.AsNoTracking()
                .SingleAsync(x => x.Email == ApiFactory.SuperAdminEmail);
            await dbContext.Assessments.AddRangeAsync(
                new Assessment
                {
                    Id = firstAssessmentId,
                    Code = $"ASC-{marker}-001",
                    Name = $"Create Assessment {marker} A",
                    RowIndex = 1,
                    UpdatedByUserId = actor.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new Assessment
                {
                    Id = secondAssessmentId,
                    Code = $"ASC-{marker}-002",
                    Name = $"Create Assessment {marker} B",
                    RowIndex = 2,
                    UpdatedByUserId = actor.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            await dbContext.SaveChangesAsync();
        }

        var create = await client.PostAsJsonAsync("/api/v1/assessment-sheets", new
        {
            studentId = student.Id,
            responsibleTeacherId = (Guid?)null,
            note = "  ghi chú sheet  ",
            startDate = "2026-08-25T00:00:00+07:00",
            dueDate = "2026-08-31T00:00:00+07:00",
            records = new[]
            {
                new { assessmentId = firstAssessmentId, latestGrade = (AssessmentGrade?)AssessmentGrade.C, note = "  cần hỗ trợ vận động  " },
                new { assessmentId = secondAssessmentId, latestGrade = (AssessmentGrade?)null, note = "   " }
            }
        }, JsonOptions);
        create.EnsureSuccessStatusCode();

        var sheet = await create.Content.ReadFromJsonAsync<AssessmentSheetDetailResponse>(JsonOptions);
        Assert.NotNull(sheet);
        Assert.Equal("ghi chú sheet", sheet.Note);
        Assert.Collection(
            sheet.Records,
            first =>
            {
                Assert.Equal($"ASC-{marker}-001", first.Assessment.Code);
                Assert.Equal(AssessmentGrade.C, first.PlanGrade);
                Assert.Equal("cần hỗ trợ vận động", first.PlanNote);
                Assert.Null(first.FinalGrade);
                Assert.Null(first.FinalNote);
            },
            second =>
            {
                Assert.Equal($"ASC-{marker}-002", second.Assessment.Code);
                Assert.Null(second.PlanGrade);
                Assert.Null(second.PlanNote);
                Assert.Null(second.FinalGrade);
                Assert.Null(second.FinalNote);
            });
    }

    [Fact]
    public async Task AssessmentSheetImportExcelPreviewAndSubmitCreatesThenUpdatesSheets()
    {
        using var client = CreateClient();
        var auth = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var student = await CreateStudentAsync(client, $"IM-{marker}", $"Import Student {marker}");
        var firstAssessmentId = Guid.NewGuid();
        var secondAssessmentId = Guid.NewGuid();
        var firstAssessmentCode = $"IMP-{marker}-001";
        var secondAssessmentCode = $"IMP-{marker}-002";
        var now = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
            var actor = await dbContext.Users.AsNoTracking()
                .SingleAsync(x => x.Email == ApiFactory.SuperAdminEmail);
            await dbContext.Assessments.AddRangeAsync(
                new Assessment
                {
                    Id = firstAssessmentId,
                    Code = firstAssessmentCode,
                    Name = $"Import Assessment {marker} A",
                    RowIndex = 1,
                    UpdatedByUserId = actor.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new Assessment
                {
                    Id = secondAssessmentId,
                    Code = secondAssessmentCode,
                    Name = $"Import Assessment {marker} B",
                    RowIndex = 2,
                    UpdatedByUserId = actor.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            await dbContext.SaveChangesAsync();
        }

        var workbook = CreateImportWorkbook(
        [
            ["planGrade", "planNote", "assessmentCode", "studentCode", "studentName", "startDate", "dueDate"],
            ["Đạt +", "Kế hoạch A", firstAssessmentCode, student.StudentCode.ToLowerInvariant(), student.FullName, "2026-06-01", "2026-08-31"],
            ["Đạt +", "Dòng trùng", firstAssessmentCode, student.StudentCode, student.FullName, "2026-06-01", "2026-08-31"],
            ["Hỗ trợ +", "Kế hoạch B", secondAssessmentCode, student.StudentCode, "Tên trong file khác", "2026-06-01", "2026-08-31"]
        ]);

        var previewResponse = await PostExcelAsync(client, "/api/v1/assessment-sheets/import-excel/preview", workbook);
        previewResponse.EnsureSuccessStatusCode();
        var preview = await previewResponse.Content.ReadFromJsonAsync<ImportAssessmentSheetsPreviewResponse>(JsonOptions);
        Assert.NotNull(preview);
        Assert.True(preview.Summary.CanImport);
        Assert.Equal(2, preview.Summary.ValidRowCount);
        Assert.Equal(0, preview.Summary.ErrorCount);
        Assert.Equal(2, preview.Summary.WarningCount);
        Assert.Equal(1, preview.Summary.SkippedDuplicateRowCount);
        Assert.Equal(1, preview.Summary.Groups);
        Assert.Equal(3, preview.Rows.Count);
        Assert.Contains(preview.Rows, row => row.Action == "SkippedDuplicate");
        Assert.All(preview.Rows, row => Assert.Empty(row.Errors));

        var importResponse = await PostExcelAsync(client, "/api/v1/assessment-sheets/import-excel", workbook);
        importResponse.EnsureSuccessStatusCode();
        var imported = await importResponse.Content.ReadFromJsonAsync<ImportAssessmentSheetsResponse>(JsonOptions);
        Assert.NotNull(imported);
        Assert.Equal(1, imported.CreatedSheetCount);
        Assert.Equal(0, imported.UpdatedSheetCount);
        Assert.Equal(2, imported.ImportedRecordCount);
        Assert.Equal(1, imported.SkippedDuplicateRowCount);
        Assert.Single(imported.Sheets);
        Assert.Equal("Created", imported.Sheets[0].Action);

        var detail = await client.GetFromJsonAsync<AssessmentSheetDetailResponse>(
            $"/api/v1/assessment-sheets/{imported.Sheets[0].Id}",
            JsonOptions);
        Assert.NotNull(detail);
        Assert.Null(detail.ResponsibleTeacherId);
        Assert.Equal(2, detail.Records.Count);
        Assert.Contains(detail.Records, record => record.Assessment.Code == firstAssessmentCode && record.PlanGrade == AssessmentGrade.A && record.PlanNote == "Kế hoạch A");
        Assert.Contains(detail.Records, record => record.Assessment.Code == secondAssessmentCode && record.PlanGrade == AssessmentGrade.C && record.PlanNote == "Kế hoạch B");
        Assert.All(detail.Records, record =>
        {
            Assert.Null(record.FinalGrade);
            Assert.Null(record.FinalNote);
        });

        var updateWorkbook = CreateImportWorkbook(
        [
            ["planGrade", "planNote", "assessmentCode", "studentCode", "studentName", "startDate", "dueDate"],
            ["Hỗ trợ -", "Kế hoạch cập nhật", secondAssessmentCode, student.StudentCode, student.FullName, "2026-06-01", "2026-08-31"]
        ]);
        var updateResponse = await PostExcelAsync(client, "/api/v1/assessment-sheets/import-excel", updateWorkbook);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<ImportAssessmentSheetsResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(0, updated.CreatedSheetCount);
        Assert.Equal(1, updated.UpdatedSheetCount);
        Assert.Equal(1, updated.ImportedRecordCount);
        Assert.Single(updated.Sheets);
        Assert.Equal(imported.Sheets[0].Id, updated.Sheets[0].Id);
        Assert.Equal("Updated", updated.Sheets[0].Action);

        var updatedDetail = await client.GetFromJsonAsync<AssessmentSheetDetailResponse>(
            $"/api/v1/assessment-sheets/{updated.Sheets[0].Id}",
            JsonOptions);
        Assert.NotNull(updatedDetail);
        Assert.Single(updatedDetail.Records);
        Assert.Equal(secondAssessmentCode, updatedDetail.Records[0].Assessment.Code);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
            var audits = await dbContext.AuditLogs.AsNoTracking()
                .Where(x => x.EntityId == updated.Sheets[0].Id && x.Action == "AssessmentSheet.ExcelImported")
                .ToListAsync();
            Assert.Equal(2, audits.Count);
            Assert.All(audits, audit =>
            {
                var newValues = audit.NewValues ?? string.Empty;
                Assert.DoesNotContain("PK", newValues, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("FileSizeBytes", newValues, StringComparison.Ordinal);
            });
        }
    }

    [Fact]
    public async Task AssessmentSheetImportExcelMapsSttAndGroupNamesIntoSnapshotAndKeepsThemOnRecordReplace()
    {
        using var client = CreateClient();
        var auth = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var student = await CreateStudentAsync(client, $"GS-{marker}", $"Group Snapshot {marker}");
        var codeA = $"GRP-{marker}-001";
        var codeB = $"GRP-{marker}-002";
        var codeC = $"GRP-{marker}-003";
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var idC = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
            var actor = await dbContext.Users.AsNoTracking().SingleAsync(x => x.Email == ApiFactory.SuperAdminEmail);
            await dbContext.Assessments.AddRangeAsync(
                new Assessment { Id = idA, Code = codeA, Name = "Cat A", RowIndex = 10, GroupLv1Name = "Age", GroupLv2Name = "Catalog Lv2", GroupLv3Name = "Catalog Lv3 X", UpdatedByUserId = actor.Id, CreatedAt = now, UpdatedAt = now },
                new Assessment { Id = idB, Code = codeB, Name = "Cat B", RowIndex = 11, GroupLv1Name = "Age", GroupLv2Name = "Catalog Lv2", GroupLv3Name = "Catalog Lv3 X", UpdatedByUserId = actor.Id, CreatedAt = now, UpdatedAt = now },
                new Assessment { Id = idC, Code = codeC, Name = "Cat C", RowIndex = 12, GroupLv1Name = "Age", GroupLv2Name = "Catalog Lv2", GroupLv3Name = "Catalog Lv3 X", UpdatedByUserId = actor.Id, CreatedAt = now, UpdatedAt = now });
            await dbContext.SaveChangesAsync();
        }

        // groupLv2Name/groupLv3Name theo kiểu ô merge: chỉ điền ở dòng đầu mỗi cụm. STT reset theo nhóm lv3.
        var workbook = CreateImportWorkbook(
        [
            ["planGrade", "planNote", "assessmentCode", "studentCode", "studentName", "startDate", "dueDate", "STT", "groupLv2Name", "groupLv3Name"],
            ["", "", codeA, student.StudentCode, student.FullName, "2026-06-01", "2026-08-31", "1", "PHÁT TRIỂN THỂ CHẤT", "vận động thô"],
            ["", "", codeB, student.StudentCode, student.FullName, "2026-06-01", "2026-08-31", "2", "", ""],
            ["", "", codeC, student.StudentCode, student.FullName, "2026-06-01", "2026-08-31", "1", "", "vận động tinh"]
        ]);

        var previewResponse = await PostExcelAsync(client, "/api/v1/assessment-sheets/import-excel/preview", workbook);
        previewResponse.EnsureSuccessStatusCode();
        var preview = await previewResponse.Content.ReadFromJsonAsync<ImportAssessmentSheetsPreviewResponse>(JsonOptions);
        Assert.NotNull(preview);
        Assert.True(preview.Summary.CanImport);
        var previewByCode = preview.Rows.ToDictionary(x => x.AssessmentCode!, x => x);
        Assert.Equal("PHÁT TRIỂN THỂ CHẤT", previewByCode[codeA].GroupLv2Name);
        Assert.Equal("PHÁT TRIỂN THỂ CHẤT", previewByCode[codeB].GroupLv2Name); // fill-down
        Assert.Equal("vận động thô", previewByCode[codeB].GroupLv3Name); // fill-down
        Assert.Equal("vận động tinh", previewByCode[codeC].GroupLv3Name);
        Assert.Equal(1, previewByCode[codeA].Stt);
        Assert.Equal(1, previewByCode[codeC].Stt);

        var importResponse = await PostExcelAsync(client, "/api/v1/assessment-sheets/import-excel", workbook);
        importResponse.EnsureSuccessStatusCode();
        var imported = await importResponse.Content.ReadFromJsonAsync<ImportAssessmentSheetsResponse>(JsonOptions);
        Assert.NotNull(imported);
        var sheetId = imported.Sheets[0].Id;

        var detail = await client.GetFromJsonAsync<AssessmentSheetDetailResponse>($"/api/v1/assessment-sheets/{sheetId}", JsonOptions);
        Assert.NotNull(detail);
        var recordByCode = detail.Records.ToDictionary(x => x.Assessment.Code, x => x);
        Assert.Equal("PHÁT TRIỂN THỂ CHẤT", recordByCode[codeA].Assessment.GroupLv2Name);
        Assert.Equal("PHÁT TRIỂN THỂ CHẤT", recordByCode[codeB].Assessment.GroupLv2Name);
        Assert.Equal("vận động thô", recordByCode[codeB].Assessment.GroupLv3Name);
        Assert.Equal("vận động tinh", recordByCode[codeC].Assessment.GroupLv3Name);
        // DisplayOrder là số chạy toàn cục theo thứ tự dòng file.
        Assert.Equal(new int?[] { 1, 2, 3 }, detail.Records.OrderBy(x => x.DisplayOrder).Select(x => x.DisplayOrder).ToArray());

        // Thay toàn bộ records (đổi planNote) không được làm mất tên nhóm snapshot đã import.
        var replaceBody = new
        {
            records = detail.Records.Select(x => new
            {
                assessmentId = x.Assessment.Code == codeA ? idA : x.Assessment.Code == codeB ? idB : idC,
                planGrade = (string?)null,
                planNote = "đã chỉnh",
                finalGrade = (string?)null,
                finalNote = (string?)null,
                displayOrder = x.DisplayOrder
            }).ToArray()
        };
        var replaceResponse = await client.PutAsJsonAsync($"/api/v1/assessment-sheets/{sheetId}/records", replaceBody, JsonOptions);
        replaceResponse.EnsureSuccessStatusCode();
        var afterReplace = await replaceResponse.Content.ReadFromJsonAsync<AssessmentSheetDetailResponse>(JsonOptions);
        Assert.NotNull(afterReplace);
        var afterByCode = afterReplace.Records.ToDictionary(x => x.Assessment.Code, x => x);
        Assert.Equal("PHÁT TRIỂN THỂ CHẤT", afterByCode[codeA].Assessment.GroupLv2Name);
        Assert.Equal("vận động thô", afterByCode[codeB].Assessment.GroupLv3Name);
        Assert.Equal("vận động tinh", afterByCode[codeC].Assessment.GroupLv3Name);
        Assert.All(afterReplace.Records, x => Assert.Equal("đã chỉnh", x.PlanNote));
    }

    [Fact]
    public async Task AssessmentSheetRecordReplacePersistsRequestProvidedSnapshotGroupNamesWithoutTouchingCatalog()
    {
        using var client = CreateClient();
        var superAdmin = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdmin.AccessToken);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var student = await CreateStudentAsync(client, $"RG-{marker}", $"Record Group {marker}");
        var firstAssessmentId = Guid.NewGuid();
        var secondAssessmentId = Guid.NewGuid();
        var firstCode = $"RG-{marker}-001";
        var secondCode = $"RG-{marker}-002";
        var now = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
            var actor = await dbContext.Users.AsNoTracking().SingleAsync(x => x.Email == ApiFactory.SuperAdminEmail);
            await dbContext.Assessments.AddRangeAsync(
                new Assessment
                {
                    Id = firstAssessmentId,
                    Code = firstCode,
                    Name = $"Record Group {marker} A",
                    GroupLv1Name = "Nhóm tuổi",
                    GroupLv2Name = "Phát triển thể chất",
                    GroupLv3Name = "Vận động",
                    UpdatedByUserId = actor.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new Assessment
                {
                    Id = secondAssessmentId,
                    Code = secondCode,
                    Name = $"Record Group {marker} B",
                    GroupLv1Name = "Nhóm tuổi",
                    GroupLv2Name = "Phát triển thể chất",
                    GroupLv3Name = "Vận động",
                    UpdatedByUserId = actor.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            await dbContext.SaveChangesAsync();
        }

        var createResponse = await client.PostAsJsonAsync("/api/v1/assessment-sheets", new
        {
            studentId = student.Id,
            responsibleTeacherId = (Guid?)null,
            note = (string?)null,
            startDate = "2026-08-01T00:00:00+07:00",
            dueDate = "2026-08-31T00:00:00+07:00",
            records = new[]
            {
                new { assessmentId = firstAssessmentId, latestGrade = (string?)null, note = (string?)null },
                new { assessmentId = secondAssessmentId, latestGrade = (string?)null, note = (string?)null }
            }
        });
        createResponse.EnsureSuccessStatusCode();
        var sheet = await createResponse.Content.ReadFromJsonAsync<AssessmentSheetDetailResponse>(JsonOptions);
        Assert.NotNull(sheet);

        // UI ASH-GRP-01: đổi tên nhóm ngay trên ô merge được gửi kèm PUT .../records khi bấm "Lưu thay đổi".
        var replaceBody = new
        {
            records = sheet.Records.Select(x => new
            {
                assessmentId = x.Assessment.Code == firstCode ? firstAssessmentId : secondAssessmentId,
                planGrade = (string?)null,
                planNote = (string?)null,
                finalGrade = (string?)null,
                finalNote = (string?)null,
                displayOrder = x.DisplayOrder,
                groupLv2Name = "  Phát triển thể chất (đổi)  ",
                groupLv3Name = "  Vận động mới  "
            }).ToArray()
        };
        var replaceResponse = await client.PutAsJsonAsync($"/api/v1/assessment-sheets/{sheet.Id}/records", replaceBody, JsonOptions);
        replaceResponse.EnsureSuccessStatusCode();
        var afterReplace = await replaceResponse.Content.ReadFromJsonAsync<AssessmentSheetDetailResponse>(JsonOptions);
        Assert.NotNull(afterReplace);
        Assert.All(afterReplace.Records, x => Assert.Equal("Phát triển thể chất (đổi)", x.Assessment.GroupLv2Name));
        Assert.All(afterReplace.Records, x => Assert.Equal("Vận động mới", x.Assessment.GroupLv3Name));

        // Bỏ trống group name trên request thì giữ nguyên snapshot đã lưu (không rơi về Assessment gốc).
        var keepBody = new
        {
            records = afterReplace.Records.Select(x => new
            {
                assessmentId = x.Assessment.Code == firstCode ? firstAssessmentId : secondAssessmentId,
                planGrade = (string?)null,
                planNote = "đã chỉnh",
                finalGrade = (string?)null,
                finalNote = (string?)null,
                displayOrder = x.DisplayOrder,
                groupLv2Name = (string?)null,
                groupLv3Name = (string?)null
            }).ToArray()
        };
        var keepResponse = await client.PutAsJsonAsync($"/api/v1/assessment-sheets/{sheet.Id}/records", keepBody, JsonOptions);
        keepResponse.EnsureSuccessStatusCode();
        var afterKeep = await keepResponse.Content.ReadFromJsonAsync<AssessmentSheetDetailResponse>(JsonOptions);
        Assert.NotNull(afterKeep);
        Assert.All(afterKeep.Records, x => Assert.Equal("Phát triển thể chất (đổi)", x.Assessment.GroupLv2Name));
        Assert.All(afterKeep.Records, x => Assert.Equal("Vận động mới", x.Assessment.GroupLv3Name));
        Assert.All(afterKeep.Records, x => Assert.Equal("đã chỉnh", x.PlanNote));

        // Bảng Assessment danh mục không đổi khi chỉ lưu snapshot của sheet.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
            var catalog = await dbContext.Assessments.AsNoTracking()
                .Where(x => x.Id == firstAssessmentId || x.Id == secondAssessmentId)
                .ToListAsync();
            Assert.Equal(2, catalog.Count);
            Assert.All(catalog, x => Assert.Equal("Phát triển thể chất", x.GroupLv2Name));
            Assert.All(catalog, x => Assert.Equal("Vận động", x.GroupLv3Name));
        }
    }

    [Fact]
    public async Task AssessmentGroupCatalogPatchUpdatesCatalogForManagersOnlyAndAudits()
    {
        using var client = CreateClient();
        var superAdmin = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdmin.AccessToken);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var firstAssessmentId = Guid.NewGuid();
        var secondAssessmentId = Guid.NewGuid();
        var firstCode = $"CG-{marker}-001";
        var secondCode = $"CG-{marker}-002";
        var now = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
            var actor = await dbContext.Users.AsNoTracking().SingleAsync(x => x.Email == ApiFactory.SuperAdminEmail);
            await dbContext.Assessments.AddRangeAsync(
                new Assessment { Id = firstAssessmentId, Code = firstCode, Name = $"Catalog {marker} A", GroupLv1Name = "Nhóm tuổi", GroupLv2Name = "Phát triển thể chất", GroupLv3Name = "Vận động", UpdatedByUserId = actor.Id, CreatedAt = now, UpdatedAt = now },
                new Assessment { Id = secondAssessmentId, Code = secondCode, Name = $"Catalog {marker} B", GroupLv1Name = "Nhóm tuổi", GroupLv2Name = "Phát triển thể chất", GroupLv3Name = "Vận động", UpdatedByUserId = actor.Id, CreatedAt = now, UpdatedAt = now });
            await dbContext.SaveChangesAsync();
        }

        var teacherEmail = $"cg-teacher-{marker}@example.test";
        var createTeacher = await client.PostAsJsonAsync("/api/v1/teachers", new
        {
            teacherCode = $"CG-{marker}",
            email = teacherEmail,
            fullName = $"Teacher {marker}",
            phoneNumber = (string?)null,
            status = "Active",
            password = "StrongTeacherPassword1!",
            note = (string?)null
        });
        createTeacher.EnsureSuccessStatusCode();

        using var teacherClient = CreateClient();
        var teacher = await LoginAsync(teacherClient, teacherEmail, "StrongTeacherPassword1!");
        teacherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacher.AccessToken);
        var forbidden = await teacherClient.PatchAsJsonAsync("/api/v1/assessments/group", new
        {
            level = 3,
            assessmentCodes = new[] { firstCode, secondCode },
            name = "Không được lưu"
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var managerPatch = await client.PatchAsJsonAsync("/api/v1/assessments/group", new
        {
            level = 3,
            assessmentCodes = new[] { firstCode, secondCode },
            name = "  Vận động chính thức  "
        });
        managerPatch.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
            var catalog = await dbContext.Assessments.AsNoTracking()
                .Where(x => x.Id == firstAssessmentId || x.Id == secondAssessmentId)
                .ToListAsync();
            Assert.Equal(2, catalog.Count);
            Assert.All(catalog, x => Assert.Equal("Vận động chính thức", x.GroupLv3Name));
            Assert.All(catalog, x => Assert.Equal("Phát triển thể chất", x.GroupLv2Name));

            var audit = await dbContext.AuditLogs.AsNoTracking()
                .SingleAsync(x => x.EntityType == "Assessment" &&
                    x.Action == "Assessment.GroupUpdated" &&
                    x.NewValues!.Contains("Vận động chính thức"));
            Assert.Contains("AssessmentCodes", audit.NewValues, StringComparison.Ordinal);
            Assert.DoesNotContain("password", audit.NewValues, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task AssessmentSheetUploadPlanAndResultPdfSaveDriveLinksWithoutUsingLegacyGenerateEndpoints()
    {
        var googleSheets = new FakeGoogleSheetsService();
        using var uploadFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGoogleSheetsService>();
            services.AddSingleton<IGoogleSheetsService>(googleSheets);
        }));
        using var client = uploadFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var auth = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var student = await CreateStudentAsync(client, $"UP-{marker}", $"Upload Pdf {marker}");
        var assessmentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = uploadFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
            var actor = await dbContext.Users.AsNoTracking()
                .SingleAsync(x => x.Email == ApiFactory.SuperAdminEmail);
            var persistedStudent = await dbContext.Students.SingleAsync(x => x.Id == student.Id);
            persistedStudent.DriveFolderId = $"folder-{marker}";
            await dbContext.Assessments.AddAsync(new Assessment
            {
                Id = assessmentId,
                Code = $"PDF-{marker}",
                Name = $"PDF Assessment {marker}",
                RowIndex = 1,
                UpdatedByUserId = actor.Id,
                CreatedAt = now,
                UpdatedAt = now
            });
            await dbContext.SaveChangesAsync();
        }

        var create = await client.PostAsJsonAsync("/api/v1/assessment-sheets", new
        {
            studentId = student.Id,
            responsibleTeacherId = (Guid?)null,
            note = (string?)null,
            startDate = "2026-06-01T00:00:00+07:00",
            dueDate = "2026-08-31T00:00:00+07:00",
            records = new[]
            {
                new { assessmentId, latestGrade = (AssessmentGrade?)AssessmentGrade.A, note = "plan note" }
            }
        }, JsonOptions);
        create.EnsureSuccessStatusCode();
        var sheet = await create.Content.ReadFromJsonAsync<AssessmentSheetDetailResponse>(JsonOptions);
        Assert.NotNull(sheet);

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent("%PDF-test"u8.ToArray()), "file", "ke-hoach-ca-nhan-test.pdf");

        var upload = await client.PostAsync($"/api/v1/assessment-sheets/{sheet.Id}/upload-plan-pdf", multipart);
        upload.EnsureSuccessStatusCode();
        var uploadedSheet = await upload.Content.ReadFromJsonAsync<AssessmentSheetDetailResponse>(JsonOptions);

        Assert.Equal($"https://drive.example.test/{sheet.Id:N}/plan.pdf", uploadedSheet?.PlanFileLinkPdf);
        Assert.Equal(sheet.Id, googleSheets.UploadedAssessmentSheetId);
        Assert.Equal(student.Id, googleSheets.UploadedStudentId);
        Assert.Equal("ke-hoach-ca-nhan-test.pdf", googleSheets.UploadedFileName);
        Assert.Equal("%PDF-test"u8.ToArray(), googleSheets.UploadedContent);

        using var resultMultipart = new MultipartFormDataContent();
        resultMultipart.Add(new ByteArrayContent("%PDF-result-test"u8.ToArray()), "file", "ket-qua-ca-nhan-test.pdf");

        var resultUpload = await client.PostAsync($"/api/v1/assessment-sheets/{sheet.Id}/upload-result-pdf", resultMultipart);
        resultUpload.EnsureSuccessStatusCode();
        var resultUploadedSheet = await resultUpload.Content.ReadFromJsonAsync<AssessmentSheetDetailResponse>(JsonOptions);

        Assert.Equal($"https://drive.example.test/{sheet.Id:N}/result.pdf", resultUploadedSheet?.ResultFileLinkPdf);
        Assert.Equal(sheet.Id, googleSheets.UploadedAssessmentSheetId);
        Assert.Equal(student.Id, googleSheets.UploadedStudentId);
        Assert.Equal("ket-qua-ca-nhan-test.pdf", googleSheets.UploadedFileName);
        Assert.Equal("%PDF-result-test"u8.ToArray(), googleSheets.UploadedContent);

        using (var scope = uploadFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
            var uploadAudits = await dbContext.AuditLogs.AsNoTracking()
                .Where(x => x.EntityId == sheet.Id &&
                    (x.Action == "AssessmentSheet.PlanPdfUploaded" || x.Action == "AssessmentSheet.ResultPdfUploaded"))
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();

            Assert.Contains(uploadAudits, x =>
                x.Action == "AssessmentSheet.PlanPdfUploaded" &&
                x.NewValues!.Contains("ke-hoach-ca-nhan-test.pdf", StringComparison.Ordinal) &&
                x.NewValues.Contains("FileSizeBytes", StringComparison.Ordinal) &&
                x.NewValues.Contains("/plan.pdf", StringComparison.Ordinal));
            Assert.Contains(uploadAudits, x =>
                x.Action == "AssessmentSheet.ResultPdfUploaded" &&
                x.NewValues!.Contains("ket-qua-ca-nhan-test.pdf", StringComparison.Ordinal) &&
                x.NewValues.Contains("FileSizeBytes", StringComparison.Ordinal) &&
                x.NewValues.Contains("/result.pdf", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task SubmitAssessmentSheetResultsCallsGoogleSheetsAndAuditsChangedCells()
    {
        var googleSheets = new FakeGoogleSheetsService();
        using var uploadFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGoogleSheetsService>();
                services.AddSingleton<IGoogleSheetsService>(googleSheets);
            });
        });
        using var client = uploadFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var auth = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var student = await CreateStudentAsync(client, $"RS-{marker}", $"Result Submit {marker}");
        var assessmentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = uploadFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
            var actor = await dbContext.Users.AsNoTracking()
                .SingleAsync(x => x.Email == ApiFactory.SuperAdminEmail);
            await dbContext.Assessments.AddAsync(new Assessment
            {
                Id = assessmentId,
                Code = $"RS-A-{marker}",
                Name = $"Result Source Assessment {marker}",
                RowIndex = 1,
                UpdatedByUserId = actor.Id,
                CreatedAt = now,
                UpdatedAt = now
            });
            await dbContext.SaveChangesAsync();
        }

        var create = await client.PostAsJsonAsync("/api/v1/assessment-sheets", new
        {
            studentId = student.Id,
            responsibleTeacherId = (Guid?)null,
            note = (string?)null,
            startDate = "2026-06-01T00:00:00+07:00",
            dueDate = "2026-08-31T00:00:00+07:00",
            records = new[]
            {
                new { assessmentId, latestGrade = (AssessmentGrade?)AssessmentGrade.A, note = "plan note" }
            }
        }, JsonOptions);
        create.EnsureSuccessStatusCode();
        var sheet = await create.Content.ReadFromJsonAsync<AssessmentSheetDetailResponse>(JsonOptions);
        Assert.NotNull(sheet);

        googleSheets.ResultSourceUpdates =
        [
            new ResultSourceCellUpdate(
                "spreadsheet-test",
                "F0.ĐG",
                "H20",
                20,
                "H",
                "FinalGrade",
                "Chưa đạt",
                "Đạt",
                student.StudentCode,
                $"RS-A-{marker}",
                $"Result Source Assessment {marker}",
                AssessmentGrade.A,
                "Đạt",
                "ghi chú kết quả"),
            new ResultSourceCellUpdate(
                "spreadsheet-test",
                "F0.ĐG",
                "I20",
                20,
                "I",
                "FinalNote",
                "",
                "ghi chú kết quả",
                student.StudentCode,
                $"RS-A-{marker}",
                $"Result Source Assessment {marker}",
                AssessmentGrade.A,
                "Đạt",
                "ghi chú kết quả")
        ];

        var submit = await client.PostAsync($"/api/v1/assessment-sheets/{sheet.Id}/submit-results", null);
        submit.EnsureSuccessStatusCode();
        var submittedSheet = await submit.Content.ReadFromJsonAsync<AssessmentSheetDetailResponse>(JsonOptions);

        Assert.NotNull(submittedSheet?.SubmissionDate);
        Assert.Equal(student.StudentCode, googleSheets.SubmittedStudentCode);
        Assert.Single(googleSheets.SubmittedRecords);

        using (var scope = uploadFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
            var resultAudits = await dbContext.AuditLogs.AsNoTracking()
                .Where(x => x.EntityId == sheet.Id &&
                    (x.Action == "AssessmentSheet.ResultsSubmitted" || x.Action == "AssessmentSheet.ResultSourceCellUpdated"))
                .ToListAsync();

            Assert.Contains(resultAudits, x =>
                x.Action == "AssessmentSheet.ResultsSubmitted" &&
                AuditJsonValueEquals(x.NewValues, "ChangedCellCount", 2));
            Assert.Contains(resultAudits, x =>
                x.Action == "AssessmentSheet.ResultSourceCellUpdated" &&
                AuditJsonValueEquals(x.NewValues, "Cell", "H20") &&
                AuditJsonValueEquals(x.NewValues, "Kind", "FinalGrade"));
            Assert.Contains(resultAudits, x =>
                x.Action == "AssessmentSheet.ResultSourceCellUpdated" &&
                AuditJsonValueEquals(x.NewValues, "Cell", "I20") &&
                AuditJsonValueEquals(x.NewValues, "Kind", "FinalNote"));
        }
    }

    [Fact]
    public async Task StudentPutClearsNullableFieldsAndCodeCanBeReusedAfterDelete()
    {
        using var client = CreateClient();
        var auth = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var code = $"HS-{Guid.NewGuid():N}"[..20];

        var createResponse = await client.PostAsJsonAsync("/api/v1/students", new
        {
            studentCode = code,
            fullName = "Nguyễn An",
            nickName = "An",
            dateOfBirth = "2021-01-02",
            gender = "Female",
            status = "Active",
            guardianName = "Nguyễn Bình",
            guardianPhone = "0900000000",
            note = "Ghi chú",
            studySchedule = FullWeekSchedule
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<StudentResponse>(JsonOptions);
        Assert.NotNull(created);

        var duplicateResponse = await client.PostAsJsonAsync("/api/v1/students", new
        {
            studentCode = code,
            fullName = "Trùng mã",
            nickName = "Trùng",
            dateOfBirth = "2021-01-02",
            status = "Active",
            studySchedule = FullWeekSchedule
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/students/{created.Id}", new
        {
            studentCode = code,
            fullName = "Nguyễn An",
            nickName = "An",
            dateOfBirth = "2021-01-02",
            gender = (string?)null,
            status = "Inactive",
            guardianName = (string?)null,
            guardianPhone = (string?)null,
            note = (string?)null,
            studySchedule = FullWeekSchedule,
            expectedVersion = created.Version
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<StudentResponse>(JsonOptions);
        Assert.Null(updated?.Gender);
        Assert.Null(updated?.GuardianName);
        Assert.Null(updated?.GuardianPhone);
        Assert.Null(updated?.Note);

        var deleteResponse = await client.DeleteAsync($"/api/v1/students/{created.Id}?expectedVersion={updated!.Version}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var deletedGetResponse = await client.GetAsync($"/api/v1/students/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deletedGetResponse.StatusCode);

        var reuseResponse = await client.PostAsJsonAsync("/api/v1/students", new
        {
            studentCode = code,
            fullName = "Trần Bình",
            nickName = "Bình",
            dateOfBirth = "2022-02-03",
            gender = "Male",
            status = "Active",
            guardianName = (string?)null,
            guardianPhone = (string?)null,
            note = (string?)null,
            studySchedule = FullWeekSchedule
        });
        Assert.Equal(HttpStatusCode.Created, reuseResponse.StatusCode);
    }

    [Fact]
    public async Task StudentCreateRejectsMissingDateOfBirth()
    {
        using var client = CreateClient();
        var auth = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/students", new
        {
            studentCode = $"HS-{Guid.NewGuid():N}"[..20],
            fullName = "Missing Date",
            nickName = "Missing",
            status = "Active",
            studySchedule = FullWeekSchedule
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminCannotCreateAnotherAdmin()
    {
        using var client = CreateClient();
        var superAdmin = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdmin.AccessToken);
        var adminEmail = $"admin-{Guid.NewGuid():N}@example.test";
        var createAdmin = await client.PostAsJsonAsync("/api/v1/users", new
        {
            email = adminEmail,
            fullName = "Test Admin",
            phoneNumber = (string?)null,
            role = "Admin",
            status = "Active",
            password = "StrongAdminPassword1!"
        });
        Assert.Equal(HttpStatusCode.Created, createAdmin.StatusCode);

        using var adminClient = CreateClient();
        var admin = await LoginAsync(adminClient, adminEmail, "StrongAdminPassword1!");
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        var forbidden = await adminClient.PostAsJsonAsync("/api/v1/users", new
        {
            email = $"other-{Guid.NewGuid():N}@example.test",
            fullName = "Other Admin",
            phoneNumber = (string?)null,
            role = "Admin",
            status = "Active",
            password = "StrongOtherPassword1!"
        });

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task AuthenticationAndTeacherAuthorizationReturnExpectedStatusCodes()
    {
        using var anonymousClient = CreateClient();
        var unauthorized = await anonymousClient.GetAsync("/api/v1/students");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var invalidLogin = await anonymousClient.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = ApiFactory.SuperAdminEmail,
            password = "WrongPassword1!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, invalidLogin.StatusCode);

        var superAdmin = await LoginAsync(anonymousClient, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        anonymousClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superAdmin.AccessToken);
        var teacherEmail = $"teacher-{Guid.NewGuid():N}@example.test";
        var createTeacher = await anonymousClient.PostAsJsonAsync("/api/v1/teachers", new
        {
            teacherCode = $"GV-{Guid.NewGuid():N}"[..20],
            email = teacherEmail,
            fullName = "Test Teacher",
            phoneNumber = (string?)null,
            status = "Active",
            password = "StrongTeacherPassword1!",
            note = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, createTeacher.StatusCode);
        var teacherDetail = await createTeacher.Content.ReadFromJsonAsync<TeacherDetailResponse>(JsonOptions);
        Assert.NotNull(teacherDetail);

        var assignedGroup = await CreateGroupAsync(anonymousClient, $"G-{Guid.NewGuid():N}"[..20]);
        var otherGroup = await CreateGroupAsync(anonymousClient, $"G-{Guid.NewGuid():N}"[..20]);
        var assignTeacher = await anonymousClient.PutAsJsonAsync(
            $"/api/v1/student-groups/{assignedGroup.Id}/responsible-teacher",
            new { teacherId = teacherDetail.Id });
        assignTeacher.EnsureSuccessStatusCode();
        var assignedStudent = await CreateStudentAsync(anonymousClient, $"AS-{Guid.NewGuid():N}"[..20], "Assigned Student");
        var otherStudent = await CreateStudentAsync(anonymousClient, $"OS-{Guid.NewGuid():N}"[..20], "Other Student");
        var unassignedStudent = await CreateStudentAsync(anonymousClient, $"US-{Guid.NewGuid():N}"[..20], "Unassigned Student");
        assignedStudent = await AssignStudentGroupAsync(anonymousClient, assignedStudent, assignedGroup.Id);
        otherStudent = await AssignStudentGroupAsync(anonymousClient, otherStudent, otherGroup.Id);

        using var teacherClient = CreateClient();
        var teacher = await LoginAsync(teacherClient, teacherEmail, "StrongTeacherPassword1!");
        teacherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacher.AccessToken);
        var scopedListResponse = await teacherClient.GetAsync("/api/v1/students?page=1&pageSize=20&sortBy=studentCode&sortOrder=asc");
        scopedListResponse.EnsureSuccessStatusCode();
        var scopedList = await scopedListResponse.Content.ReadFromJsonAsync<PagedResponse<StudentResponse>>(JsonOptions);
        Assert.NotNull(scopedList);
        Assert.Contains(scopedList.Items, student => student.Id == assignedStudent.Id);
        Assert.DoesNotContain(scopedList.Items, student => student.Id == otherStudent.Id);
        Assert.DoesNotContain(scopedList.Items, student => student.Id == unassignedStudent.Id);

        var scopedGet = await teacherClient.GetAsync($"/api/v1/students/{assignedStudent.Id}");
        scopedGet.EnsureSuccessStatusCode();
        var outsideScopeGet = await teacherClient.GetAsync($"/api/v1/students/{otherStudent.Id}");
        Assert.Equal(HttpStatusCode.NotFound, outsideScopeGet.StatusCode);

        var createForbidden = await teacherClient.PostAsJsonAsync("/api/v1/students", new
        {
            studentCode = $"TF-{Guid.NewGuid():N}"[..20],
            fullName = "Teacher Forbidden",
            nickName = "Forbidden",
            dateOfBirth = "2021-01-02",
            status = "Active",
            studySchedule = FullWeekSchedule
        });
        Assert.Equal(HttpStatusCode.Forbidden, createForbidden.StatusCode);

        var updateForbidden = await teacherClient.PutAsJsonAsync($"/api/v1/students/{assignedStudent.Id}", new
        {
            assignedStudent.StudentCode,
            assignedStudent.FullName,
            assignedStudent.NickName,
            assignedStudent.DateOfBirth,
            assignedStudent.Gender,
            assignedStudent.Status,
            assignedStudent.GuardianName,
            assignedStudent.GuardianPhone,
            assignedStudent.Note,
            assignedStudent.DriveFolderId,
            studySchedule = new { mode = "FullDay", weekdays = MondayThursday },
            expectedVersion = assignedStudent.Version
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, updateForbidden.StatusCode);

        var groupForbidden = await teacherClient.PutAsJsonAsync(
            $"/api/v1/students/{assignedStudent.Id}/group",
            new { groupId = (Guid?)null, expectedVersion = assignedStudent.Version });
        Assert.Equal(HttpStatusCode.Forbidden, groupForbidden.StatusCode);

        var deleteForbidden = await teacherClient.DeleteAsync(
            $"/api/v1/students/{assignedStudent.Id}?expectedVersion={assignedStudent.Version}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteForbidden.StatusCode);
    }

    [Fact]
    public async Task UserPutClearsPhoneAndDuplicateEmailReturnsConflict()
    {
        using var client = CreateClient();
        var auth = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var firstEmail = $"first-{Guid.NewGuid():N}@example.test";
        var secondEmail = $"second-{Guid.NewGuid():N}@example.test";
        var first = await CreateAdminAsync(client, firstEmail, "0900000000");
        _ = await CreateAdminAsync(client, secondEmail, null);

        var clearPhone = await client.PutAsJsonAsync($"/api/v1/users/{first.Id}", new
        {
            email = firstEmail,
            fullName = "First Admin Updated",
            phoneNumber = (string?)null,
            role = "Admin",
            status = "Active"
        });
        clearPhone.EnsureSuccessStatusCode();
        var updated = await clearPhone.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        Assert.Null(updated?.PhoneNumber);

        var duplicate = await client.PutAsJsonAsync($"/api/v1/users/{first.Id}", new
        {
            email = secondEmail,
            fullName = "First Admin Updated",
            phoneNumber = (string?)null,
            role = "Admin",
            status = "Active"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task StudentListReturnsFilteredPaginationMetadataAndStableSort()
    {
        using var client = CreateClient();
        var auth = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var marker = Guid.NewGuid().ToString("N")[..8];
        await CreateStudentAsync(client, $"{marker}-B", $"{marker} Beta");
        await CreateStudentAsync(client, $"{marker}-A", $"{marker} Alpha");

        var response = await client.GetAsync($"/api/v1/students?search={marker}&status=Active&page=1&pageSize=1&sortBy=fullName&sortOrder=asc");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<StudentResponse>>(JsonOptions);
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(1, page.Pagination.Page);
        Assert.Equal(1, page.Pagination.PageSize);
        Assert.Equal(2, page.Pagination.TotalItems);
        Assert.Equal(2, page.Pagination.TotalPages);
        Assert.Contains("Alpha", page.Items[0].FullName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StudentScheduleRoundTripsFiltersAndRejectsStaleWrites()
    {
        using var client = CreateClient();
        var auth = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var create = await client.PostAsJsonAsync("/api/v1/students", new
        {
            studentCode = $"SC-{marker}",
            fullName = $"Schedule {marker}",
            nickName = $"Nick {marker}",
            dateOfBirth = "2021-01-02",
            gender = (string?)null,
            status = "Active",
            guardianName = (string?)null,
            guardianPhone = (string?)null,
            note = (string?)null,
            studySchedule = new
            {
                mode = "OneToOne",
                weekdays = ThursdayMonday
            }
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var student = await create.Content.ReadFromJsonAsync<StudentResponse>(JsonOptions);
        Assert.NotNull(student);
        Assert.Equal(1, student.Version);
        Assert.Equal(StudyMode.OneToOne, student.StudySchedule.Mode);
        Assert.Equal([StudyWeekday.Monday, StudyWeekday.Thursday], student.StudySchedule.Weekdays);

        var filtered = await client.GetFromJsonAsync<PagedResponse<StudentResponse>>(
            $"/api/v1/students?search={marker}&studyMode=OneToOne&studyWeekday=Monday&sortBy=studyMode&sortOrder=asc",
            JsonOptions);
        Assert.NotNull(filtered);
        Assert.Single(filtered.Items);
        Assert.Equal(student.Id, filtered.Items[0].Id);

        var noOp = await client.PutAsJsonAsync($"/api/v1/students/{student.Id}", new
        {
            student.StudentCode,
            student.FullName,
            student.NickName,
            student.DateOfBirth,
            student.Gender,
            student.Status,
            student.GuardianName,
            student.GuardianPhone,
            student.Note,
            studySchedule = new { mode = "OneToOne", weekdays = MondayThursday },
            expectedVersion = student.Version
        }, JsonOptions);
        noOp.EnsureSuccessStatusCode();
        var versionTwo = await noOp.Content.ReadFromJsonAsync<StudentResponse>(JsonOptions);
        Assert.Equal(2, versionTwo?.Version);

        var stale = await client.PutAsJsonAsync($"/api/v1/students/{student.Id}", new
        {
            student.StudentCode,
            fullName = "Must Not Persist",
            student.NickName,
            student.DateOfBirth,
            student.Gender,
            student.Status,
            student.GuardianName,
            student.GuardianPhone,
            student.Note,
            studySchedule = new { mode = "FullDay", weekdays = Tuesday },
            expectedVersion = 1
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var problem = JsonDocument.Parse(await stale.Content.ReadAsStringAsync()))
        {
            Assert.Equal("StudentVersionConflict", problem.RootElement.GetProperty("code").GetString());
            Assert.Equal(2, problem.RootElement.GetProperty("currentVersion").GetInt32());
        }

        var staleDelete = await client.DeleteAsync($"/api/v1/students/{student.Id}?expectedVersion=1");
        Assert.Equal(HttpStatusCode.Conflict, staleDelete.StatusCode);
        var current = await client.GetFromJsonAsync<StudentResponse>($"/api/v1/students/{student.Id}", JsonOptions);
        Assert.Equal(student.FullName, current?.FullName);
        Assert.Equal(StudyMode.OneToOne, current?.StudySchedule.Mode);
        var delete = await client.DeleteAsync($"/api/v1/students/{student.Id}?expectedVersion=2");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var duplicateDays = await client.PostAsJsonAsync("/api/v1/students", new
        {
            studentCode = $"SD-{marker}", fullName = "Duplicate Days", nickName = "Duplicate",
            dateOfBirth = "2021-01-02", status = "Active",
            studySchedule = new { mode = "FullDay", weekdays = DuplicateMonday }
        });
        Assert.Equal(HttpStatusCode.BadRequest, duplicateDays.StatusCode);
        using var validation = JsonDocument.Parse(await duplicateDays.Content.ReadAsStringAsync());
        Assert.True(validation.RootElement.GetProperty("errors").TryGetProperty("studySchedule.weekdays", out _));

        var missingMode = await client.PostAsJsonAsync("/api/v1/students", new
        {
            studentCode = $"SM-{marker}", fullName = "Missing Mode", nickName = "Missing",
            dateOfBirth = "2021-01-02", status = "Active",
            studySchedule = new { weekdays = MondayThursday }
        });
        Assert.Equal(HttpStatusCode.BadRequest, missingMode.StatusCode);
        using var modeValidation = JsonDocument.Parse(await missingMode.Content.ReadAsStringAsync());
        Assert.True(modeValidation.RootElement.GetProperty("errors").TryGetProperty("studySchedule.mode", out _));
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private static async Task<HttpResponseMessage> PostExcelAsync(HttpClient client, string requestUri, byte[] workbook)
    {
        using var multipart = new MultipartFormDataContent();
        var content = new ByteArrayContent(workbook);
        content.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        multipart.Add(content, "file", "import_khcn.xlsx");
        return await client.PostAsync(requestUri, multipart);
    }

    private static byte[] CreateImportWorkbook(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteZipEntry(archive, "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            WriteZipEntry(archive, "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            WriteZipEntry(archive, "xl/workbook.xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            WriteZipEntry(archive, "xl/_rels/workbook.xml.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            WriteZipEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(rows));
        }

        return stream.ToArray();
    }

    private static string BuildWorksheetXml(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.AppendLine("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var rowNumber = rowIndex + 1;
            builder.Append(CultureInfo.InvariantCulture, $"<row r=\"{rowNumber}\">");
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Count; columnIndex++)
            {
                var cell = $"{ColumnName(columnIndex)}{rowNumber}";
                builder.Append(CultureInfo.InvariantCulture, $"<c r=\"{cell}\" t=\"inlineStr\"><is><t>");
                builder.Append(EscapeXml(rows[rowIndex][columnIndex]));
                builder.Append("</t></is></c>");
            }
            builder.AppendLine("</row>");
        }

        builder.AppendLine("</sheetData></worksheet>");
        return builder.ToString();
    }

    private static void WriteZipEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static string ColumnName(int zeroBasedIndex)
    {
        var index = zeroBasedIndex;
        var name = string.Empty;
        do
        {
            name = (char)('A' + index % 26) + name;
            index = index / 26 - 1;
        } while (index >= 0);

        return name;
    }

    private static string EscapeXml(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);

    private static async Task<AccessTokenResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var setupResponse = await client.PostAsJsonAsync("/api/v1/setup/super-admin", new
        {
            email = ApiFactory.SuperAdminEmail,
            fullName = "Integration SuperAdmin",
            password = ApiFactory.SuperAdminPassword
        });
        Assert.True(
            setupResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            $"Unexpected setup response: {(int)setupResponse.StatusCode} {setupResponse.StatusCode}");

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password), JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Login response was empty.");
    }

    private static async Task<UserResponse> CreateAdminAsync(HttpClient client, string email, string? phoneNumber)
    {
        var response = await client.PostAsJsonAsync("/api/v1/users", new
        {
            email,
            fullName = "Admin",
            phoneNumber,
            role = "Admin",
            status = "Active",
            password = "StrongTeacherPassword1!"
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Create user response was empty.");
    }

    private static async Task<StudentResponse> CreateStudentAsync(HttpClient client, string code, string fullName)
    {
        var response = await client.PostAsJsonAsync("/api/v1/students", new
        {
            studentCode = code,
            fullName,
            nickName = fullName,
            dateOfBirth = "2021-01-02",
            status = "Active",
            studySchedule = FullWeekSchedule
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StudentResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Create student response was empty.");
    }

    private static async Task<StudentGroupResponse> CreateGroupAsync(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync("/api/v1/student-groups", new
        {
            code,
            name = $"Group {code}",
            status = "Active"
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StudentGroupResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Create group response was empty.");
    }

    private static async Task<StudentResponse> AssignStudentGroupAsync(
        HttpClient client,
        StudentResponse student,
        Guid groupId)
    {
        var response = await client.PutAsJsonAsync(
            $"/api/v1/students/{student.Id}/group",
            new { groupId, expectedVersion = student.Version });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StudentResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Assign student group response was empty.");
    }

    private static object FullWeekSchedule => new
    {
        mode = "FullDay",
        weekdays = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" }
    };

    private static bool AuditJsonValueEquals(string? json, string propertyName, string expectedValue)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            string.Equals(property.GetString(), expectedValue, StringComparison.Ordinal);
    }

    private static bool AuditJsonValueEquals(string? json, string propertyName, int expectedValue)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out var actualValue) &&
            actualValue == expectedValue;
    }

    private sealed class FakeGoogleSheetsService : IGoogleSheetsService
    {
        public Guid UploadedAssessmentSheetId { get; private set; }
        public Guid UploadedStudentId { get; private set; }
        public string? UploadedFileName { get; private set; }
        public byte[]? UploadedContent { get; private set; }
        public string? SubmittedStudentCode { get; private set; }
        public IReadOnlyList<AssessmentRecord> SubmittedRecords { get; private set; } = [];
        public IReadOnlyList<ResultSourceCellUpdate> ResultSourceUpdates { get; set; } = [];
        public bool SmokeWasCalled { get; private set; }
        public GoogleSheetsCredentialSmokeResponse SmokeResponse { get; set; } = new(
            Success: true,
            IsConfigured: true,
            SpreadsheetId: "fake-spreadsheet-id",
            SpreadsheetTitle: "Fake spreadsheet",
            FirstSheetTitle: "Fake sheet",
            ReadRange: null,
            ReadRowCount: null,
            ErrorCode: null);

        public Task<GoogleSheetsCredentialSmokeResponse> SmokeTestCredentialAsync(CancellationToken cancellationToken)
        {
            SmokeWasCalled = true;
            return Task.FromResult(SmokeResponse);
        }

        public Task<SyncAssessmentsFromGoogleSheetsResponse> SyncAssessmentsAsync(
            SyncAssessmentsFromGoogleSheetsRequest request,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<string> UploadAssessmentSheetPlanPdfAsync(
            Guid assessmentSheetId,
            Guid studentId,
            string? existingFileLink,
            string fileName,
            byte[] content,
            CancellationToken cancellationToken)
        {
            UploadedAssessmentSheetId = assessmentSheetId;
            UploadedStudentId = studentId;
            UploadedFileName = fileName;
            UploadedContent = content;
            return Task.FromResult($"https://drive.example.test/{assessmentSheetId:N}/plan.pdf");
        }

        public Task<string> UploadAssessmentSheetResultPdfAsync(
            Guid assessmentSheetId,
            Guid studentId,
            string? existingFileLink,
            string fileName,
            byte[] content,
            CancellationToken cancellationToken)
        {
            UploadedAssessmentSheetId = assessmentSheetId;
            UploadedStudentId = studentId;
            UploadedFileName = fileName;
            UploadedContent = content;
            return Task.FromResult($"https://drive.example.test/{assessmentSheetId:N}/result.pdf");
        }

        public Task<IReadOnlyList<ResultSourceCellUpdate>> WriteFinalGradesToSourceSheetAsync(
            string studentCode,
            IReadOnlyList<AssessmentRecord> records,
            CancellationToken cancellationToken)
        {
            SubmittedStudentCode = studentCode;
            SubmittedRecords = records;
            return Task.FromResult(ResultSourceUpdates);
        }
    }
}
