using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdminPortal.Application.Auth;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.StudentGroups;
using AdminPortal.Application.Teachers;
using AdminPortal.Domain.Enums;
using AdminPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdminPortal.IntegrationTests;

public sealed class TeacherManagementApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string TeacherPassword = "StrongTeacherPassword1!";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task ListSearchUsesVietnameseFoldLiteralCharactersAndExactPagination()
    {
        using var client = await CreateManagerClientAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var first = await CreateTeacherAsync(
            client,
            $" gv%{marker} ",
            "Nguyễn   Thị Hoàng",
            phoneNumber: "090 123-4567");
        var second = await CreateTeacherAsync(
            client,
            $"gv_{marker}b",
            "Đặng Minh");

        Assert.Equal($"GV%{marker.ToUpperInvariant()}", first.TeacherCode);
        Assert.Equal(1, first.Version);
        Assert.Equal(7, first.AttendanceEditWindowDays);

        var accentPage = await ListAsync(client,
            $"search={Uri.EscapeDataString("  NGUYEN   THI  ")}&page=1&pageSize=1&sortBy=fullName&sortOrder=asc");
        Assert.Equal(1, accentPage.Pagination.TotalItems);
        Assert.Equal(first.Id, Assert.Single(accentPage.Items).Id);

        var decomposedPage = await ListAsync(client,
            $"search={Uri.EscapeDataString("HOA\u0300NG")}&page=1&pageSize=20");
        Assert.Equal(first.Id, Assert.Single(decomposedPage.Items).Id);

        var dPage = await ListAsync(client, "search=dang&page=1&pageSize=20");
        Assert.Equal(second.Id, Assert.Single(dPage.Items).Id);

        var phonePage = await ListAsync(client, "search=090123&page=1&pageSize=20");
        Assert.Equal(first.Id, Assert.Single(phonePage.Items).Id);

        var percentPage = await ListAsync(client, "search=%25&page=1&pageSize=20");
        Assert.Equal(first.Id, Assert.Single(percentPage.Items).Id);
        var underscorePage = await ListAsync(client, "search=_&page=1&pageSize=20");
        Assert.Equal(second.Id, Assert.Single(underscorePage.Items).Id);

        var secondPage = await ListAsync(client,
            $"search={marker}&page=2&pageSize=1&sortBy=teacherCode&sortOrder=asc");
        Assert.Single(secondPage.Items);
        Assert.Equal(2, secondPage.Pagination.TotalItems);
        Assert.Equal(2, secondPage.Pagination.TotalPages);

        var beyondLastPage = await ListAsync(client, "search=%20%20%20&page=999&pageSize=1");
        Assert.Empty(beyondLastPage.Items);
        Assert.True(beyondLastPage.Pagination.TotalItems >= 2);

        var invalidFilters = await client.GetAsync(
            $"/api/v1/teachers?groupId={Guid.NewGuid()}&unassigned=true");
        Assert.Equal(HttpStatusCode.BadRequest, invalidFilters.StatusCode);
        Assert.Equal("ValidationFailed", await ProblemCodeAsync(invalidFilters));

        var detailResponse = await client.GetAsync($"/api/v1/teachers/{first.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var payload = await detailResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("session", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FullPutPolicyAndAuditRespectVersionNullableClearAndGroupSnapshot()
    {
        using var client = await CreateManagerClientAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var privateNote = $"PRIVATE-NOTE-{marker}";
        var teacher = await CreateTeacherAsync(
            client,
            $"GV-A-{marker}",
            $"Teacher A {marker}",
            phoneNumber: "0900000000",
            note: privateNote);
        var other = await CreateTeacherAsync(client, $"GV-B-{marker}", $"Teacher B {marker}");
        var group = await CreateAssignedGroupAsync(client, $"G{marker}", teacher.Id);

        var duplicateCode = await client.PostAsJsonAsync("/api/v1/teachers", CreateBody(
            teacher.TeacherCode.ToLowerInvariant(),
            $"duplicate-code-{marker}@example.test",
            "Duplicate Code"));
        Assert.Equal(HttpStatusCode.Conflict, duplicateCode.StatusCode);
        Assert.Equal("TeacherCodeAlreadyExists", await ProblemCodeAsync(duplicateCode));

        var duplicateEmail = await client.PostAsJsonAsync("/api/v1/teachers", CreateBody(
            $"GV-C-{marker}",
            teacher.Email.ToUpperInvariant(),
            "Duplicate Email"));
        Assert.Equal(HttpStatusCode.Conflict, duplicateEmail.StatusCode);
        Assert.Equal("EmailAlreadyExists", await ProblemCodeAsync(duplicateEmail));

        var updatedCode = $"GV-Z-{marker}".ToUpperInvariant();
        var clearNullable = await client.PutAsJsonAsync($"/api/v1/teachers/{teacher.Id}", new
        {
            teacherCode = updatedCode.ToLowerInvariant(),
            teacher.FullName,
            teacher.Email,
            phoneNumber = (string?)null,
            teacher.Status,
            note = (string?)null,
            expectedVersion = teacher.Version
        }, JsonOptions);
        clearNullable.EnsureSuccessStatusCode();
        var cleared = await ReadAsync<TeacherDetailResponse>(clearNullable);
        Assert.Equal(updatedCode, cleared.TeacherCode);
        Assert.Null(cleared.PhoneNumber);
        Assert.Null(cleared.Note);
        Assert.Equal(2, cleared.Version);
        Assert.Equal(group.SnapshotVersion, (await GetGroupAsync(client, group.Id)).SnapshotVersion);

        var conflictingEmail = await client.PutAsJsonAsync($"/api/v1/teachers/{teacher.Id}", new
        {
            teacherCode = cleared.TeacherCode,
            fullName = "Must Roll Back",
            email = other.Email,
            phoneNumber = (string?)null,
            status = "Inactive",
            note = "Must roll back",
            expectedVersion = cleared.Version
        });
        Assert.Equal(HttpStatusCode.Conflict, conflictingEmail.StatusCode);
        Assert.Equal("EmailAlreadyExists", await ProblemCodeAsync(conflictingEmail));
        var afterEmailConflict = await GetTeacherAsync(client, teacher.Id);
        Assert.Equal(cleared.FullName, afterEmailConflict.FullName);
        Assert.Equal(cleared.Email, afterEmailConflict.Email);
        Assert.Equal(cleared.Version, afterEmailConflict.Version);

        var renamed = await PutTeacherAsync(client, cleared with
        {
            FullName = $"Renamed Teacher {marker}"
        });
        Assert.Equal(3, renamed.Version);
        Assert.Equal(group.SnapshotVersion + 1, (await GetGroupAsync(client, group.Id)).SnapshotVersion);

        var stale = await client.PutAsJsonAsync($"/api/v1/teachers/{teacher.Id}", new
        {
            teacherCode = $"STALE-{marker}",
            fullName = "Stale Name",
            renamed.Email,
            renamed.PhoneNumber,
            renamed.Status,
            renamed.Note,
            expectedVersion = cleared.Version
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("TeacherVersionConflict", await ProblemCodeAsync(stale));
        Assert.Equal(renamed.Version, await ProblemIntAsync(stale, "currentVersion"));

        var invalidPolicy = await client.PutAsJsonAsync(
            $"/api/v1/teachers/{teacher.Id}/attendance-policy",
            new { attendanceEditWindowDays = 0, expectedVersion = renamed.Version });
        Assert.Equal(HttpStatusCode.BadRequest, invalidPolicy.StatusCode);
        Assert.Equal("InvalidAttendanceEditWindow", await ProblemCodeAsync(invalidPolicy));

        var policy = await client.PutAsJsonAsync(
            $"/api/v1/teachers/{teacher.Id}/attendance-policy",
            new { attendanceEditWindowDays = 1, expectedVersion = renamed.Version });
        policy.EnsureSuccessStatusCode();
        var policyUpdated = await ReadAsync<TeacherDetailResponse>(policy);
        Assert.Equal(1, policyUpdated.AttendanceEditWindowDays);
        Assert.Equal(4, policyUpdated.Version);
        Assert.Equal(group.SnapshotVersion + 1, (await GetGroupAsync(client, group.Id)).SnapshotVersion);

        var oldCodeReuse = await CreateTeacherAsync(
            client,
            teacher.TeacherCode,
            $"Code Reuse {marker}");
        Assert.Equal(teacher.TeacherCode, oldCodeReuse.TeacherCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
        var audits = await dbContext.AuditLogs.AsNoTracking()
            .Where(x => x.EntityId == teacher.Id)
            .Select(x => new { x.Action, x.OldValues, x.NewValues })
            .ToListAsync();
        Assert.Contains(audits, x => x.Action == "Teacher.Created");
        Assert.Contains(audits, x => x.Action == "Teacher.Updated");
        Assert.Contains(audits, x => x.Action == "Teacher.AttendancePolicyUpdated");
        Assert.DoesNotContain(audits, x =>
            (x.OldValues?.Contains(privateNote, StringComparison.Ordinal) ?? false) ||
            (x.NewValues?.Contains(privateNote, StringComparison.Ordinal) ?? false));
        Assert.DoesNotContain(audits, x =>
            (x.OldValues?.Contains(teacher.Email, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (x.NewValues?.Contains(teacher.Email, StringComparison.OrdinalIgnoreCase) ?? false));
        Assert.DoesNotContain(audits, x =>
            (x.OldValues?.Contains(teacher.FullName, StringComparison.Ordinal) ?? false) ||
            (x.NewValues?.Contains(teacher.FullName, StringComparison.Ordinal) ?? false));
        Assert.DoesNotContain(audits, x =>
            (x.OldValues?.Contains("0900000000", StringComparison.Ordinal) ?? false) ||
            (x.NewValues?.Contains("0900000000", StringComparison.Ordinal) ?? false));
    }

    [Fact]
    public async Task CanonicalBoundaryAuthorizationAndDeleteLifecycleFollowContract()
    {
        using var anonymous = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/v1/teachers")).StatusCode);

        using var manager = await CreateManagerClientAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var teacher = await CreateTeacherAsync(manager, $"GV-D-{marker}", $"Delete Teacher {marker}");

        var legacyCreate = await manager.PostAsJsonAsync("/api/v1/users", new
        {
            email = $"legacy-{marker}@example.test",
            fullName = "Legacy Teacher",
            phoneNumber = (string?)null,
            role = "Teacher",
            status = "Active",
            password = TeacherPassword
        });
        Assert.Equal(HttpStatusCode.Conflict, legacyCreate.StatusCode);
        Assert.Equal("TeacherMustBeManagedViaTeachers", await ProblemCodeAsync(legacyCreate));

        var legacyUpdate = await manager.PutAsJsonAsync($"/api/v1/users/{teacher.UserId}", new
        {
            teacher.Email,
            teacher.FullName,
            teacher.PhoneNumber,
            role = "Teacher",
            teacher.Status
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, legacyUpdate.StatusCode);
        Assert.Equal("TeacherMustBeManagedViaTeachers", await ProblemCodeAsync(legacyUpdate));

        var legacyGet = await manager.GetAsync($"/api/v1/users/{teacher.UserId}");
        Assert.Equal(HttpStatusCode.Conflict, legacyGet.StatusCode);
        Assert.Equal("TeacherMustBeManagedViaTeachers", await ProblemCodeAsync(legacyGet));
        var legacyDelete = await manager.DeleteAsync($"/api/v1/users/{teacher.UserId}");
        Assert.Equal(HttpStatusCode.Conflict, legacyDelete.StatusCode);
        Assert.Equal("TeacherMustBeManagedViaTeachers", await ProblemCodeAsync(legacyDelete));

        using var teacherClient = CreateClient();
        var teacherAuth = await LoginAsync(teacherClient, teacher.Email, TeacherPassword);
        teacherClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", teacherAuth.AccessToken);
        var changedPassword = "ChangedTeacherPassword2!";
        var passwordResponse = await manager.PutAsJsonAsync(
            $"/api/v1/users/{teacher.UserId}/password",
            new { password = changedPassword });
        Assert.Equal(HttpStatusCode.NoContent, passwordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await teacherClient.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await teacherClient.PostAsJsonAsync(
                "/api/v1/auth/login",
                new LoginRequest(teacher.Email, TeacherPassword),
                JsonOptions)).StatusCode);

        var passwordAuth = await LoginAsync(teacherClient, teacher.Email, changedPassword);
        teacherClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", passwordAuth.AccessToken);
        var inactivated = await PutTeacherAsync(manager, teacher with { Status = UserStatus.Inactive });
        Assert.Equal(2, inactivated.Version);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await teacherClient.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await teacherClient.PostAsJsonAsync(
                "/api/v1/auth/login",
                new LoginRequest(teacher.Email, changedPassword),
                JsonOptions)).StatusCode);

        var reactivated = await PutTeacherAsync(manager, inactivated with { Status = UserStatus.Active });
        Assert.Equal(3, reactivated.Version);
        var activeAuth = await LoginAsync(teacherClient, teacher.Email, changedPassword);
        teacherClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", activeAuth.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await teacherClient.GetAsync("/api/v1/teachers")).StatusCode);

        var group = await CreateAssignedGroupAsync(manager, $"D{marker}", teacher.Id);
        var blocked = await manager.DeleteAsync(
            $"/api/v1/teachers/{teacher.Id}?expectedVersion={reactivated.Version}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("TeacherHasResponsibleGroups", await ProblemCodeAsync(blocked));

        var unassign = await manager.PutAsJsonAsync(
            $"/api/v1/student-groups/{group.Id}/responsible-teacher",
            new { teacherId = (Guid?)null });
        unassign.EnsureSuccessStatusCode();

        var deleted = await manager.DeleteAsync(
            $"/api/v1/teachers/{teacher.Id}?expectedVersion={reactivated.Version}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await teacherClient.GetAsync("/api/v1/auth/me")).StatusCode);

        var missing = await manager.GetAsync($"/api/v1/teachers/{teacher.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("TeacherNotFound", await ProblemCodeAsync(missing));

        var reusedDeletedCode = await manager.PostAsJsonAsync("/api/v1/teachers", CreateBody(
            teacher.TeacherCode,
            $"deleted-code-{marker}@example.test",
            "Deleted Code Reuse"));
        Assert.Equal(HttpStatusCode.Conflict, reusedDeletedCode.StatusCode);
        Assert.Equal("TeacherCodeAlreadyExists", await ProblemCodeAsync(reusedDeletedCode));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
        var retainedTeacher = await dbContext.Teachers.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(x => x.Id == teacher.Id);
        var deletedUser = await dbContext.Users.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(x => x.Id == teacher.UserId);
        Assert.Equal(4, retainedTeacher.Version);
        Assert.NotNull(deletedUser.DeletedAt);
    }

    [Fact]
    public async Task ConcurrentMutationsAndDuplicateCreateHaveOneWinnerWithoutPartialRows()
    {
        using var client = await CreateManagerClientAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var teacher = await CreateTeacherAsync(client, $"GV-R-{marker}", $"Race Teacher {marker}");

        var updateTask = client.PutAsJsonAsync($"/api/v1/teachers/{teacher.Id}", new
        {
            teacherCode = teacher.TeacherCode,
            fullName = $"Race Renamed {marker}",
            teacher.Email,
            teacher.PhoneNumber,
            teacher.Status,
            teacher.Note,
            expectedVersion = teacher.Version
        }, JsonOptions);
        var policyTask = client.PutAsJsonAsync($"/api/v1/teachers/{teacher.Id}/attendance-policy", new
        {
            attendanceEditWindowDays = 2,
            expectedVersion = teacher.Version
        });
        var mutationResponses = await Task.WhenAll(updateTask, policyTask);
        Assert.Single(mutationResponses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(mutationResponses, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("TeacherVersionConflict", await ProblemCodeAsync(
            mutationResponses.Single(x => x.StatusCode == HttpStatusCode.Conflict)));
        Assert.Equal(2, (await GetTeacherAsync(client, teacher.Id)).Version);

        var duplicateCode = $"GV-X-{marker}";
        var firstEmail = $"race-a-{marker}@example.test";
        var secondEmail = $"race-b-{marker}@example.test";
        var duplicateResponses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/v1/teachers", CreateBody(duplicateCode, firstEmail, "Race A")),
            client.PostAsJsonAsync("/api/v1/teachers", CreateBody(duplicateCode, secondEmail, "Race B")));
        Assert.Single(duplicateResponses, x => x.StatusCode == HttpStatusCode.Created);
        var conflict = Assert.Single(duplicateResponses, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("TeacherCodeAlreadyExists", await ProblemCodeAsync(conflict));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
        var normalizedFirstEmail = firstEmail.ToUpperInvariant();
        var normalizedSecondEmail = secondEmail.ToUpperInvariant();
        var normalizedDuplicateCode = duplicateCode.ToUpperInvariant();
        Assert.Equal(1, await dbContext.Teachers.CountAsync(x => x.TeacherCode == normalizedDuplicateCode));
        Assert.Equal(1, await dbContext.Users.CountAsync(x =>
            x.NormalizedEmail == normalizedFirstEmail ||
            x.NormalizedEmail == normalizedSecondEmail));
    }

    [Fact]
    public async Task ValidationAndUserListBoundaryReturnStableResponses()
    {
        using var client = await CreateManagerClientAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var teacher = await CreateTeacherAsync(client, $"GV-V-{marker}", $"Validation Teacher {marker}");

        var blankCode = await client.PostAsJsonAsync("/api/v1/teachers", CreateBody(
            "   ",
            $"blank-{marker}@example.test",
            "Blank Code"));
        Assert.Equal(HttpStatusCode.BadRequest, blankCode.StatusCode);
        Assert.Equal("ValidationFailed", await ProblemCodeAsync(blankCode));

        var longNote = await client.PutAsJsonAsync($"/api/v1/teachers/{teacher.Id}", new
        {
            teacher.TeacherCode,
            teacher.FullName,
            teacher.Email,
            teacher.PhoneNumber,
            teacher.Status,
            note = new string('x', 2001),
            expectedVersion = teacher.Version
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, longNote.StatusCode);
        Assert.Equal("ValidationFailed", await ProblemCodeAsync(longNote));

        var missingVersion = await client.PutAsJsonAsync($"/api/v1/teachers/{teacher.Id}", new
        {
            teacher.TeacherCode,
            teacher.FullName,
            teacher.Email,
            teacher.PhoneNumber,
            teacher.Status,
            teacher.Note
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, missingVersion.StatusCode);
        Assert.Equal("ValidationFailed", await ProblemCodeAsync(missingVersion));

        var adminOnlyUsers = await client.GetAsync("/api/v1/users?role=Teacher&page=1&pageSize=20");
        adminOnlyUsers.EnsureSuccessStatusCode();
        var page = await ReadAsync<PagedResponse<AdminPortal.Application.Users.UserResponse>>(adminOnlyUsers);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.Pagination.TotalItems);
    }

    private async Task<HttpClient> CreateManagerClientAsync()
    {
        var client = CreateClient();
        var auth = await LoginAsync(client, ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private static async Task<AccessTokenResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var setup = await client.PostAsJsonAsync("/api/v1/setup/super-admin", new
        {
            email = ApiFactory.SuperAdminEmail,
            fullName = "Integration SuperAdmin",
            password = ApiFactory.SuperAdminPassword
        });
        Assert.True(setup.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password), JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<AccessTokenResponse>(response);
    }

    private static async Task<TeacherDetailResponse> CreateTeacherAsync(
        HttpClient client,
        string teacherCode,
        string fullName,
        string? email = null,
        string? phoneNumber = null,
        string? note = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/teachers", CreateBody(
            teacherCode,
            email ?? $"{Guid.NewGuid():N}@example.test",
            fullName,
            phoneNumber,
            note));
        response.EnsureSuccessStatusCode();
        Assert.NotNull(response.Headers.Location);
        return await ReadAsync<TeacherDetailResponse>(response);
    }

    private static object CreateBody(
        string teacherCode,
        string email,
        string fullName,
        string? phoneNumber = null,
        string? note = null) => new
        {
            teacherCode,
            fullName,
            email,
            phoneNumber,
            status = "Active",
            password = TeacherPassword,
            note
        };

    private static async Task<TeacherDetailResponse> PutTeacherAsync(
        HttpClient client,
        TeacherDetailResponse teacher)
    {
        var response = await client.PutAsJsonAsync($"/api/v1/teachers/{teacher.Id}", new
        {
            teacher.TeacherCode,
            teacher.FullName,
            teacher.Email,
            teacher.PhoneNumber,
            teacher.Status,
            teacher.Note,
            expectedVersion = teacher.Version
        }, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<TeacherDetailResponse>(response);
    }

    private static async Task<TeacherDetailResponse> GetTeacherAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/v1/teachers/{id}");
        response.EnsureSuccessStatusCode();
        return await ReadAsync<TeacherDetailResponse>(response);
    }

    private static async Task<PagedResponse<TeacherListItemResponse>> ListAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/v1/teachers?{query}");
        response.EnsureSuccessStatusCode();
        return await ReadAsync<PagedResponse<TeacherListItemResponse>>(response);
    }

    private static async Task<StudentGroupResponse> CreateAssignedGroupAsync(
        HttpClient client,
        string code,
        Guid teacherId)
    {
        var create = await client.PostAsJsonAsync("/api/v1/student-groups", new
        {
            code,
            name = $"Group {code}",
            status = "Active"
        });
        create.EnsureSuccessStatusCode();
        var group = await ReadAsync<StudentGroupResponse>(create);
        var assign = await client.PutAsJsonAsync(
            $"/api/v1/student-groups/{group.Id}/responsible-teacher",
            new { teacherId });
        assign.EnsureSuccessStatusCode();
        return await ReadAsync<StudentGroupResponse>(assign);
    }

    private static async Task<StudentGroupResponse> GetGroupAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/v1/student-groups/{id}");
        response.EnsureSuccessStatusCode();
        return await ReadAsync<StudentGroupResponse>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(JsonOptions)
        ?? throw new InvalidOperationException($"Empty {typeof(T).Name} response.");

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task<int?> ProblemIntAsync(HttpResponseMessage response, string propertyName)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty(propertyName, out var value) ? value.GetInt32() : null;
    }
}
