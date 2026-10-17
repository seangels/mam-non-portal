using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using AdminPortal.Application.Auth;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.StudentDrive;
using AdminPortal.Application.GoogleSheets;
using AdminPortal.Application.Students;
using AdminPortal.Application.Teachers;
using Microsoft.Extensions.DependencyInjection;

namespace AdminPortal.IntegrationTests;

public sealed class StudentDriveFolderApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] FullDayWeekdays =
        ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task TeacherAndStudentDriveContractsRoundTripRenameAndReconcile()
    {
        using var client = factory.CreateClient();
        var setup = await client.PostAsJsonAsync("/api/v1/setup/super-admin", new
        {
            email = ApiFactory.SuperAdminEmail,
            fullName = "Integration SuperAdmin",
            password = ApiFactory.SuperAdminPassword
        });
        Assert.True(setup.IsSuccessStatusCode || setup.StatusCode == System.Net.HttpStatusCode.Conflict);
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword));
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions);
        Assert.NotNull(login);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var teacherFolderId = $"teacher-{Guid.NewGuid():N}";
        var teacherEmail = $"drive-{Guid.NewGuid():N}@example.test";
        var teacherResponse = await client.PostAsJsonAsync("/api/v1/teachers", new
        {
            teacherCode = $"GV-{Guid.NewGuid():N}"[..20],
            fullName = "Drive Teacher",
            email = teacherEmail,
            phoneNumber = (string?)null,
            status = "Active",
            password = "StrongTeacherPassword1!",
            note = (string?)null,
            driveFolderId = $"https://drive.google.com/drive/folders/{teacherFolderId}"
        });
        teacherResponse.EnsureSuccessStatusCode();
        var teacher = await teacherResponse.Content.ReadFromJsonAsync<TeacherDetailResponse>(JsonOptions);
        Assert.NotNull(teacher);
        Assert.Equal(teacherFolderId, teacher.DriveFolderId);

        using (var teacherClient = factory.CreateClient())
        {
            var teacherLoginResponse = await teacherClient.PostAsJsonAsync(
                "/api/v1/auth/login",
                new LoginRequest(teacherEmail, "StrongTeacherPassword1!"));
            teacherLoginResponse.EnsureSuccessStatusCode();
            var teacherLogin = await teacherLoginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions);
            Assert.NotNull(teacherLogin);
            teacherClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", teacherLogin.AccessToken);
            var forbiddenSuggestions = await teacherClient.GetAsync("/api/v1/students/student-code-suggestions");
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenSuggestions.StatusCode);
        }

        var studentCode = $"HS-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var studentResponse = await client.PostAsJsonAsync("/api/v1/students", new
        {
            studentCode,
            fullName = "Drive Student",
            nickName = "Student",
            dateOfBirth = "2021-01-02",
            gender = "Male",
            status = "Active",
            guardianName = (string?)null,
            guardianPhone = (string?)null,
            note = (string?)null,
            driveFolderId = (string?)null,
            studySchedule = new
            {
                mode = "FullDay",
                weekdays = FullDayWeekdays
            }
        });
        studentResponse.EnsureSuccessStatusCode();
        var student = await studentResponse.Content.ReadFromJsonAsync<StudentResponse>(JsonOptions);
        Assert.NotNull(student);
        Assert.False(string.IsNullOrWhiteSpace(student.DriveFolderId));

        var renameResponse = await client.PostAsJsonAsync(
            $"/api/v1/students/{student.Id}/drive-folder/rename",
            new { expectedVersion = student.Version });
        renameResponse.EnsureSuccessStatusCode();
        var renamed = await renameResponse.Content.ReadFromJsonAsync<StudentDriveFolderActionResponse>(JsonOptions);
        Assert.NotNull(renamed);
        Assert.Equal($"{studentCode} - Drive Student", renamed.FolderName);

        var changedCode = $"NEW-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var updateResponse = await client.PutAsJsonAsync($"/api/v1/students/{student.Id}", new
        {
            studentCode = changedCode,
            fullName = "Tên mới",
            nickName = "Nickname mới",
            dateOfBirth = "2021-02-03",
            gender = "Male",
            status = "Active",
            guardianName = (string?)null,
            guardianPhone = (string?)null,
            note = (string?)null,
            driveFolderId = (string?)null,
            studySchedule = new { mode = "FullDay", weekdays = FullDayWeekdays },
            expectedVersion = student.Version
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<StudentResponse>(JsonOptions);
        Assert.NotNull(updated);

        var secondUpdateResponse = await client.PutAsJsonAsync($"/api/v1/students/{student.Id}", new
        {
            studentCode = changedCode,
            fullName = "Tên mới lần hai",
            nickName = "Nickname lần hai",
            dateOfBirth = "2021-03-04",
            gender = "Male",
            status = "Active",
            guardianName = (string?)null,
            guardianPhone = (string?)null,
            note = (string?)null,
            driveFolderId = (string?)null,
            studySchedule = new { mode = "FullDay", weekdays = FullDayWeekdays },
            expectedVersion = updated.Version
        });
        secondUpdateResponse.EnsureSuccessStatusCode();

        var sheetService = Assert.IsType<FakeStudentGoogleSheetsService>(
            factory.Services.GetRequiredService<IStudentGoogleSheetsService>());
        var relevantCalls = sheetService.Calls.Where(x =>
            string.Equals(x.SheetStudentId, studentCode, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Contains(relevantCalls, x => x.Operation == "Create" && x.FullName == "Drive Student");
        Assert.Contains(relevantCalls, x =>
            x.Operation == "Update" && x.FullName == "Tên mới" &&
            x.NickName == "Nickname mới" && x.DateOfBirth == new DateOnly(2021, 2, 3));
        Assert.Contains(relevantCalls, x =>
            x.Operation == "Update" && x.FullName == "Tên mới lần hai" &&
            x.NickName == "Nickname lần hai" && x.DateOfBirth == new DateOnly(2021, 3, 4));
        Assert.DoesNotContain(sheetService.Calls, x =>
            x.Operation == "Update" && string.Equals(x.SheetStudentId, changedCode, StringComparison.OrdinalIgnoreCase));

        var freeCode1 = $"FREE-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var freeCode2 = $"FREE-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        sheetService.Suggestions = [freeCode1, changedCode, freeCode2];
        var suggestionsResponse = await client.GetAsync("/api/v1/students/student-code-suggestions");
        suggestionsResponse.EnsureSuccessStatusCode();
        var suggestions = await suggestionsResponse.Content.ReadFromJsonAsync<StudentCodeSuggestionsResponse>(JsonOptions);
        Assert.NotNull(suggestions);
        Assert.Equal([freeCode1, freeCode2], suggestions.Items);

        var unavailableCode = $"USED-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        sheetService.UnavailableCodes[unavailableCode] = 0;
        var unavailableResponse = await client.PostAsJsonAsync("/api/v1/students", new
        {
            studentCode = unavailableCode,
            fullName = "Không được ghi đè",
            nickName = "Blocked",
            dateOfBirth = "2021-04-05",
            gender = "Male",
            status = "Active",
            guardianName = (string?)null,
            guardianPhone = (string?)null,
            note = (string?)null,
            driveFolderId = (string?)null,
            studySchedule = new { mode = "FullDay", weekdays = FullDayWeekdays }
        });
        Assert.Equal(HttpStatusCode.Conflict, unavailableResponse.StatusCode);
        using (var problem = JsonDocument.Parse(await unavailableResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("StudentGoogleSheetsCodeUnavailable", problem.RootElement.GetProperty("code").GetString());
        }

        var rolledBackResponse = await client.GetAsync(
            $"/api/v1/students?search={Uri.EscapeDataString(unavailableCode)}");
        rolledBackResponse.EnsureSuccessStatusCode();
        var rolledBackList = await rolledBackResponse.Content.ReadFromJsonAsync<PagedResponse<StudentResponse>>(JsonOptions);
        Assert.NotNull(rolledBackList);
        Assert.Equal(0, rolledBackList.Pagination.TotalItems);

        var reconcileResponse = await client.PostAsync("/api/v1/students/drive-folders/reconcile", null);
        reconcileResponse.EnsureSuccessStatusCode();
        var reconcile = await reconcileResponse.Content.ReadFromJsonAsync<StudentDriveReconcileResponse>(JsonOptions);
        Assert.NotNull(reconcile);
        Assert.True(reconcile.Processed >= 1);
        Assert.Equal(0, reconcile.Failed);
    }
}
