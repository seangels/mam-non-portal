using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdminPortal.Application.Attendance;
using AdminPortal.Application.Auth;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.StudentGroups;
using AdminPortal.Application.Students;
using AdminPortal.Application.Teachers;
using AdminPortal.Domain.Enums;
using AdminPortal.Domain.Entities;
using AdminPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdminPortal.IntegrationTests;

public sealed class AttendanceApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task MissingFirstSaveUpdateAndImmutableSnapshotFollowContract()
    {
        using var client = await CreateManagerClientAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var teacher = await CreateTeacherProfileAsync(client, $"Teacher {marker}");
        var group = await CreateGroupAsync(client, $"G{marker}", teacher.Id);
        var student = await CreateStudentAsync(client, $"S{marker}", $"Student {marker}");
        student = await AssignStudentAsync(client, student.Id, group.Id);
        var date = LocalToday();

        var missing = await GetDailyAsync(client, group.Id, date);
        Assert.Equal(AttendanceSheetState.Missing, missing.SheetState);
        Assert.True(missing.CanCreate);
        Assert.Single(missing.Items);
        Assert.Equal(AttendanceStatus.Present, missing.Items[0].Status);
        Assert.Null(missing.Items[0].EntryId);

        var createResponse = await client.PostAsJsonAsync("/api/v1/attendance/sheets", new
        {
            groupId = group.Id,
            date = Iso(date),
            expectedSnapshotVersion = missing.CurrentSnapshotVersion,
            records = missing.Items.Select(PresentRecord)
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.StartsWith("/api/v1/attendance/sheets/", createResponse.Headers.Location?.OriginalString, StringComparison.Ordinal);
        var saved = await ReadAsync<AttendanceDailyResponse>(createResponse);
        Assert.Equal(AttendanceSheetState.Saved, saved.SheetState);
        Assert.Equal(AttendanceSnapshotSource.CurrentSnapshot, saved.SnapshotSource);
        Assert.Equal(missing.CurrentSnapshotVersion, saved.SourceSnapshotVersion);
        Assert.Equal(1, saved.SheetVersion);
        Assert.NotNull(saved.Items[0].EntryId);

        var halfDay = saved.Items.Select(x => new
        {
            studentId = x.StudentId,
            status = "AbsentHalfDay",
            halfDayPart = "Morning",
            isExcused = true,
            durationMinutes = (int?)null,
            notes = "Phụ huynh đã báo"
        });
        var updateResponse = await client.PutAsJsonAsync($"/api/v1/attendance/sheets/{saved.SheetId}", new
        {
            expectedVersion = saved.SheetVersion,
            records = halfDay
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await ReadAsync<AttendanceDailyResponse>(updateResponse);
        Assert.Equal(2, updated.SheetVersion);
        Assert.Equal(AttendanceStatus.AbsentHalfDay, updated.Items[0].Status);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
            var audits = await dbContext.AuditLogs.AsNoTracking()
                .Where(x => x.EntityId == saved.SheetId).Select(x => x.NewValues).ToListAsync();
            Assert.DoesNotContain(audits, x => x?.Contains("Phụ huynh đã báo", StringComparison.Ordinal) == true);
        }

        var rosterMismatch = await client.PutAsJsonAsync($"/api/v1/attendance/sheets/{saved.SheetId}", new
        {
            expectedVersion = updated.SheetVersion,
            records = new object[]
            {
                PresentRecord(updated.Items[0]),
                new
                {
                    studentId = Guid.NewGuid(), status = "Present", halfDayPart = (string?)null,
                    isExcused = (bool?)null, durationMinutes = (int?)null, notes = (string?)null
                }
            }
        });
        Assert.Equal(HttpStatusCode.Conflict, rosterMismatch.StatusCode);
        Assert.Equal("AttendanceRosterMismatch", await ProblemCodeAsync(rosterMismatch));
        var afterMismatch = await GetDailyAsync(client, group.Id, date);
        Assert.Equal(updated.SheetVersion, afterMismatch.SheetVersion);
        Assert.Equal(AttendanceStatus.AbsentHalfDay, afterMismatch.Items[0].Status);

        var staleResponse = await client.PutAsJsonAsync($"/api/v1/attendance/sheets/{saved.SheetId}", new
        {
            expectedVersion = saved.SheetVersion,
            records = halfDay
        });
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        Assert.Equal("SheetVersionConflict", await ProblemCodeAsync(staleResponse));

        var renameResponse = await client.PutAsJsonAsync($"/api/v1/students/{student.Id}", new
        {
            studentCode = student.StudentCode,
            fullName = $"Renamed {marker}",
            nickName = student.NickName,
            dateOfBirth = student.DateOfBirth,
            gender = student.Gender,
            status = student.Status,
            guardianName = student.GuardianName,
            guardianPhone = student.GuardianPhone,
            note = student.Note
        }, JsonOptions);
        renameResponse.EnsureSuccessStatusCode();
        var historicalSnapshot = await GetDailyAsync(client, group.Id, date);
        Assert.Equal($"Student {marker}", historicalSnapshot.Items[0].FullName);
    }

    [Fact]
    public async Task TeacherOnlySeesAndReadsCurrentlyResponsibleGroup()
    {
        using var manager = await CreateManagerClientAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var teacherA = await CreateTeacherProfileAsync(manager, $"Teacher A {marker}");
        var teacherB = await CreateTeacherProfileAsync(manager, $"Teacher B {marker}");
        var groupA = await CreateGroupAsync(manager, $"A{marker}", teacherA.Id);
        var groupB = await CreateGroupAsync(manager, $"B{marker}", teacherB.Id);
        var student = await CreateStudentAsync(manager, $"TA{marker}", $"Teacher Window {marker}");
        _ = await AssignStudentAsync(manager, student.Id, groupA.Id);
        var policy = await manager.PutAsJsonAsync($"/api/v1/teachers/{teacherA.Id}/attendance-policy", new
        {
            attendanceEditWindowDays = 1,
            expectedVersion = teacherA.Version
        });
        policy.EnsureSuccessStatusCode();

        using var teacherClient = CreateClient();
        var login = await LoginAsync(teacherClient, teacherA.Email);
        teacherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var date = LocalToday();
        var contextResponse = await teacherClient.GetAsync($"/api/v1/attendance/context?date={Iso(date)}");
        contextResponse.EnsureSuccessStatusCode();
        var context = await ReadAsync<AttendanceContextResponse>(contextResponse);
        Assert.Single(context.Groups);
        Assert.Equal(groupA.Id, context.Groups[0].Id);

        var own = await teacherClient.GetAsync($"/api/v1/attendance/daily?date={Iso(date)}&groupId={groupA.Id}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var other = await teacherClient.GetAsync($"/api/v1/attendance/daily?date={Iso(date)}&groupId={groupB.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);

        var yesterday = date.AddDays(-1);
        var outsideWindow = await GetDailyAsync(teacherClient, groupA.Id, yesterday);
        Assert.False(outsideWindow.CanCreate);
        Assert.Equal(AttendanceReadOnlyReason.AttendanceEditWindowExceeded, outsideWindow.ReadOnlyReason);
        var forbiddenSave = await teacherClient.PostAsJsonAsync("/api/v1/attendance/sheets", new
        {
            groupId = groupA.Id,
            date = Iso(yesterday),
            expectedSnapshotVersion = outsideWindow.CurrentSnapshotVersion,
            records = outsideWindow.Items.Select(PresentRecord)
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenSave.StatusCode);
        Assert.Equal("AttendanceEditWindowExceeded", await ProblemCodeAsync(forbiddenSave));
        var forbiddenCandidates = await teacherClient.GetAsync(
            "/api/v1/attendance/historical-recovery/student-candidates?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCandidates.StatusCode);
    }

    [Fact]
    public async Task GroupAndStudentListProjectAssignedNavigationData()
    {
        using var client = await CreateManagerClientAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var teacherName = $"Projection Teacher {marker}";
        var teacher = await CreateTeacherProfileAsync(client, teacherName);
        var group = await CreateGroupAsync(client, $"Q{marker}", teacher.Id);
        Assert.Equal(teacher.Id, group.ResponsibleTeacherId);
        Assert.Equal(teacherName, group.ResponsibleTeacherName);
        Assert.Equal(0, group.StudentCount);

        var student = await CreateStudentAsync(client, $"Q{marker}", $"Projection Student {marker}");
        student = await AssignStudentAsync(client, student.Id, group.Id);

        var listResponse = await client.GetAsync(
            "/api/v1/student-groups?page=1&pageSize=20&sortBy=status&sortOrder=asc");
        listResponse.EnsureSuccessStatusCode();
        var groups = await ReadAsync<PagedResponse<StudentGroupResponse>>(listResponse);
        var listed = Assert.Single(groups.Items, item => item.Id == group.Id);
        Assert.Equal(teacher.Id, listed.ResponsibleTeacherId);
        Assert.Equal(teacherName, listed.ResponsibleTeacherName);
        Assert.Equal(1, listed.StudentCount);

        var detailResponse = await client.GetAsync($"/api/v1/student-groups/{group.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await ReadAsync<StudentGroupResponse>(detailResponse);
        Assert.Equal(teacher.Id, detail.ResponsibleTeacherId);
        Assert.Equal(teacherName, detail.ResponsibleTeacherName);
        Assert.Equal(1, detail.StudentCount);

        var studentListResponse = await client.GetAsync(
            $"/api/v1/students?page=1&pageSize=20&groupId={group.Id}&sortBy=status&sortOrder=asc");
        studentListResponse.EnsureSuccessStatusCode();
        var students = await ReadAsync<PagedResponse<StudentResponse>>(studentListResponse);
        var listedStudent = Assert.Single(students.Items);
        Assert.Equal(student.Id, listedStudent.Id);
        Assert.Equal(group.Id, listedStudent.GroupId);
        Assert.Equal(group.Code, listedStudent.GroupCode);
        Assert.Equal(group.Name, listedStudent.GroupName);
    }

    [Fact]
    public async Task StaleSnapshotAndHistoricalRecoveryUseStableProblemCodesAndProvenance()
    {
        using var client = await CreateManagerClientAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var teacher = await CreateTeacherProfileAsync(client, $"Recovery Teacher {marker}");
        var group = await CreateGroupAsync(client, $"R{marker}", teacher.Id);
        var first = await CreateStudentAsync(client, $"R1{marker}", $"First {marker}");
        first = await AssignStudentAsync(client, first.Id, group.Id);
        var today = LocalToday();
        var initial = await GetDailyAsync(client, group.Id, today);
        var second = await CreateStudentAsync(client, $"R2{marker}", $"Second {marker}");
        _ = await AssignStudentAsync(client, second.Id, group.Id);

        var stale = await client.PostAsJsonAsync("/api/v1/attendance/sheets", new
        {
            groupId = group.Id,
            date = Iso(today),
            expectedSnapshotVersion = initial.CurrentSnapshotVersion,
            records = initial.Items.Select(PresentRecord)
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("SnapshotChanged", await ProblemCodeAsync(stale));

        var yesterday = today.AddDays(-1);
        var missingHistory = await GetDailyAsync(client, group.Id, yesterday);
        Assert.False(missingHistory.CanCreate);
        Assert.True(missingHistory.CanRecover);
        Assert.Equal(AttendanceReadOnlyReason.HistoricalSnapshotUnavailable, missingHistory.ReadOnlyReason);

        var standard = await client.PostAsJsonAsync("/api/v1/attendance/sheets", new
        {
            groupId = group.Id,
            date = Iso(yesterday),
            expectedSnapshotVersion = missingHistory.CurrentSnapshotVersion,
            records = new[] { PresentRecord(first), PresentRecord(second) }
        });
        Assert.Equal(HttpStatusCode.Conflict, standard.StatusCode);
        Assert.Equal("HistoricalSnapshotUnavailable", await ProblemCodeAsync(standard));

        var recovery = await client.PostAsJsonAsync("/api/v1/attendance/sheets/historical-recovery", new
        {
            groupId = group.Id,
            date = Iso(yesterday),
            responsibleTeacherId = teacher.Id,
            records = new[] { PresentRecord(first), PresentRecord(second) },
            acknowledgeHistoricalSnapshot = true,
            recoveryReason = "Đối chiếu phiếu giấy"
        });
        Assert.Equal(HttpStatusCode.Created, recovery.StatusCode);
        var recovered = await ReadAsync<AttendanceDailyResponse>(recovery);
        Assert.Equal(AttendanceSnapshotSource.HistoricalRecovery, recovered.SnapshotSource);
        Assert.Null(recovered.SourceSnapshotVersion);
        Assert.Equal(2, recovered.Items.Count);
    }

    [Fact]
    public async Task ResponsibleTeacherAndRosterBlockLifecycleMutations()
    {
        using var client = await CreateManagerClientAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var teacher = await CreateTeacherProfileAsync(client, $"Lifecycle Teacher {marker}");
        var group = await CreateGroupAsync(client, $"L{marker}", teacher.Id);
        var student = await CreateStudentAsync(client, $"L{marker}", $"Lifecycle Student {marker}");
        _ = await AssignStudentAsync(client, student.Id, group.Id);

        var deleteTeacher = await client.DeleteAsync(
            $"/api/v1/teachers/{teacher.Id}?expectedVersion={teacher.Version}");
        Assert.Equal(HttpStatusCode.Conflict, deleteTeacher.StatusCode);
        Assert.Equal("TeacherHasResponsibleGroups", await ProblemCodeAsync(deleteTeacher));
        var deactivateGroup = await client.PutAsJsonAsync($"/api/v1/student-groups/{group.Id}", new
        {
            code = group.Code,
            name = group.Name,
            status = "Inactive"
        });
        Assert.Equal(HttpStatusCode.Conflict, deactivateGroup.StatusCode);
        var deleteStudent = await client.DeleteAsync($"/api/v1/students/{student.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteStudent.StatusCode);
        Assert.Equal("StudentHasCurrentGroup", await ProblemCodeAsync(deleteStudent));

        var inactive = await CreateStudentAsync(client, $"LI{marker}", $"Inactive Student {marker}");
        var makeInactive = await client.PutAsJsonAsync($"/api/v1/students/{inactive.Id}", new
        {
            studentCode = inactive.StudentCode,
            fullName = inactive.FullName,
            nickName = inactive.NickName,
            dateOfBirth = inactive.DateOfBirth,
            gender = inactive.Gender,
            status = "Inactive",
            guardianName = inactive.GuardianName,
            guardianPhone = inactive.GuardianPhone,
            note = inactive.Note
        }, JsonOptions);
        makeInactive.EnsureSuccessStatusCode();
        var assignInactive = await client.PutAsJsonAsync(
            $"/api/v1/students/{inactive.Id}/group", new { groupId = group.Id });
        Assert.Equal(HttpStatusCode.Conflict, assignInactive.StatusCode);
        Assert.Equal("StudentInactive", await ProblemCodeAsync(assignInactive));
    }

    [Fact]
    public async Task ConcurrentFirstSaveCreatesExactlyOneSheet()
    {
        using var client = await CreateManagerClientAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var teacher = await CreateTeacherProfileAsync(client, $"Race Teacher {marker}");
        var group = await CreateGroupAsync(client, $"C{marker}", teacher.Id);
        var student = await CreateStudentAsync(client, $"C{marker}", $"Race Student {marker}");
        _ = await AssignStudentAsync(client, student.Id, group.Id);
        var date = LocalToday();
        var daily = await GetDailyAsync(client, group.Id, date);
        var body = new
        {
            groupId = group.Id,
            date = Iso(date),
            expectedSnapshotVersion = daily.CurrentSnapshotVersion,
            records = daily.Items.Select(PresentRecord).ToArray()
        };

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/v1/attendance/sheets", body),
            client.PostAsJsonAsync("/api/v1/attendance/sheets", body));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("AttendanceSheetAlreadyExists",
            await ProblemCodeAsync(responses.Single(x => x.StatusCode == HttpStatusCode.Conflict)));
    }

    [Fact]
    public async Task ConcurrentAssignmentCannotExceedGroupCapacity()
    {
        using var client = await CreateManagerClientAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var teacher = await CreateTeacherProfileAsync(client, $"Capacity Teacher {marker}");
        var group = await CreateGroupAsync(client, $"P{marker}", teacher.Id);
        var now = DateTimeOffset.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
            for (var index = 0; index < 99; index++)
            {
                dbContext.Students.Add(new Student
                {
                    Id = Guid.NewGuid(), StudentCode = $"{marker}{index:D2}", FullName = $"Capacity {index:D2}",
                    NickName = $"C{index:D2}", DateOfBirth = new DateOnly(2021, 1, 2),
                    Status = StudentStatus.Active, GroupId = group.Id, GroupAssignedAt = now,
                    CreatedAt = now, UpdatedAt = now
                });
            }
            await dbContext.SaveChangesAsync();
        }
        var first = await CreateStudentAsync(client, $"PX{marker}", $"Candidate X {marker}");
        var second = await CreateStudentAsync(client, $"PY{marker}", $"Candidate Y {marker}");
        var responses = await Task.WhenAll(
            client.PutAsJsonAsync($"/api/v1/students/{first.Id}/group", new { groupId = group.Id }),
            client.PutAsJsonAsync($"/api/v1/students/{second.Id}/group", new { groupId = group.Id }));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("GroupCapacityExceeded",
            await ProblemCodeAsync(responses.Single(x => x.StatusCode == HttpStatusCode.Conflict)));
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
        Assert.Equal(100, await verifyDb.Students.CountAsync(x => x.GroupId == group.Id && x.Status == StudentStatus.Active));
    }

    private async Task<HttpClient> CreateManagerClientAsync()
    {
        var client = CreateClient();
        var auth = await LoginSuperAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
    });

    private static async Task<AccessTokenResponse> LoginSuperAdminAsync(HttpClient client)
    {
        _ = await client.PostAsJsonAsync("/api/v1/setup/super-admin", new
        {
            email = ApiFactory.SuperAdminEmail,
            fullName = "Integration SuperAdmin",
            password = ApiFactory.SuperAdminPassword
        });
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(ApiFactory.SuperAdminEmail, ApiFactory.SuperAdminPassword), JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<AccessTokenResponse>(response);
    }

    private static async Task<AccessTokenResponse> LoginAsync(HttpClient client, string teacherEmail)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(teacherEmail, "StrongTeacherPassword1!"), JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<AccessTokenResponse>(response);
    }

    private static async Task<TeacherDetailResponse> CreateTeacherProfileAsync(HttpClient client, string fullName)
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        var create = await client.PostAsJsonAsync("/api/v1/teachers", new
        {
            teacherCode = $"GV-{Guid.NewGuid():N}"[..20],
            email,
            fullName,
            phoneNumber = (string?)null,
            status = "Active",
            password = "StrongTeacherPassword1!",
            note = (string?)null
        });
        create.EnsureSuccessStatusCode();
        return await ReadAsync<TeacherDetailResponse>(create);
    }

    private static async Task<StudentGroupResponse> CreateGroupAsync(
        HttpClient client, string code, Guid responsibleTeacherId)
    {
        var create = await client.PostAsJsonAsync("/api/v1/student-groups", new
        {
            code, name = $"Group {code}", status = "Active"
        });
        create.EnsureSuccessStatusCode();
        var group = await ReadAsync<StudentGroupResponse>(create);
        var assign = await client.PutAsJsonAsync($"/api/v1/student-groups/{group.Id}/responsible-teacher", new
        {
            teacherId = responsibleTeacherId
        });
        assign.EnsureSuccessStatusCode();
        return await ReadAsync<StudentGroupResponse>(assign);
    }

    private static async Task<StudentResponse> CreateStudentAsync(HttpClient client, string code, string fullName)
    {
        var create = await client.PostAsJsonAsync("/api/v1/students", new
        {
            studentCode = code, fullName, nickName = $"Nick {code}", dateOfBirth = "2021-01-02",
            gender = "Female", status = "Active", guardianName = (string?)null,
            guardianPhone = (string?)null, note = (string?)null
        });
        create.EnsureSuccessStatusCode();
        return await ReadAsync<StudentResponse>(create);
    }

    private static async Task<StudentResponse> AssignStudentAsync(HttpClient client, Guid studentId, Guid groupId)
    {
        var response = await client.PutAsJsonAsync($"/api/v1/students/{studentId}/group", new { groupId });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<StudentResponse>(response);
    }

    private static async Task<AttendanceDailyResponse> GetDailyAsync(HttpClient client, Guid groupId, DateOnly date)
    {
        var response = await client.GetAsync($"/api/v1/attendance/daily?date={Iso(date)}&groupId={groupId}");
        response.EnsureSuccessStatusCode();
        return await ReadAsync<AttendanceDailyResponse>(response);
    }

    private static object PresentRecord(AttendanceItemResponse item) => new
    {
        studentId = item.StudentId, status = "Present", halfDayPart = (string?)null,
        isExcused = (bool?)null, durationMinutes = (int?)null, notes = (string?)null
    };

    private static object PresentRecord(StudentResponse student) => new
    {
        studentId = student.Id, status = "Present", halfDayPart = (string?)null,
        isExcused = (bool?)null, durationMinutes = (int?)null, notes = (string?)null
    };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(JsonOptions)
        ?? throw new InvalidOperationException($"Empty {typeof(T).Name} response.");

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static DateOnly LocalToday() =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
