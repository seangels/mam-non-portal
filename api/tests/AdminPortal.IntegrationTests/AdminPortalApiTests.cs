using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdminPortal.Application.Auth;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.StudentGroups;
using AdminPortal.Application.Students;
using AdminPortal.Application.Teachers;
using AdminPortal.Application.Users;
using AdminPortal.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

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
}
