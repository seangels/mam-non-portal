using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdminPortal.Application.Auth;
using AdminPortal.Application.StudentDrive;
using AdminPortal.Application.Students;
using AdminPortal.Application.Teachers;

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
        var teacherResponse = await client.PostAsJsonAsync("/api/v1/teachers", new
        {
            teacherCode = $"GV-{Guid.NewGuid():N}"[..20],
            fullName = "Drive Teacher",
            email = $"drive-{Guid.NewGuid():N}@example.test",
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

        var reconcileResponse = await client.PostAsync("/api/v1/students/drive-folders/reconcile", null);
        reconcileResponse.EnsureSuccessStatusCode();
        var reconcile = await reconcileResponse.Content.ReadFromJsonAsync<StudentDriveReconcileResponse>(JsonOptions);
        Assert.NotNull(reconcile);
        Assert.True(reconcile.Processed >= 1);
        Assert.Equal(0, reconcile.Failed);
    }
}
