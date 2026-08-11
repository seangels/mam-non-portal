using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdminPortal.Application.Auth;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.Students;
using AdminPortal.Application.Teachers;
using AdminPortal.Application.Users;
using AdminPortal.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AdminPortal.IntegrationTests;

public sealed class AdminPortalApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
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
            note = "Ghi chú"
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
            status = "Active"
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
            note = (string?)null
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<StudentResponse>(JsonOptions);
        Assert.Null(updated?.Gender);
        Assert.Null(updated?.GuardianName);
        Assert.Null(updated?.GuardianPhone);
        Assert.Null(updated?.Note);

        var deleteResponse = await client.DeleteAsync($"/api/v1/students/{created.Id}");
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
            note = (string?)null
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
            status = "Active"
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

        using var teacherClient = CreateClient();
        var teacher = await LoginAsync(teacherClient, teacherEmail, "StrongTeacherPassword1!");
        teacherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacher.AccessToken);
        var forbidden = await teacherClient.GetAsync("/api/v1/students");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
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

    private static async Task CreateStudentAsync(HttpClient client, string code, string fullName)
    {
        var response = await client.PostAsJsonAsync("/api/v1/students", new
        {
            studentCode = code,
            fullName,
            nickName = fullName,
            dateOfBirth = "2021-01-02",
            status = "Active"
        });
        response.EnsureSuccessStatusCode();
    }
}
