using AdminPortal.Domain.Enums;
using AdminPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace AdminPortal.IntegrationTests;

public sealed class MigrationUpgradeTests : IAsyncLifetime
{
    private const string InitialMigration = "20260811000000_InitialCreate";
    private const string AttendanceMigration = "20260811130802_AddAttendanceFoundation";
    private const string TeacherManagementMigration = "20260811150730_AddTeacherManagement";
    private const string ScheduleMigration = "20260811172348_AddStudentStudySchedule";
    private const string AttendanceUiMigration = "20260811201427_AddAttendanceUnmarkedStatus";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("admin_portal_upgrade_tests")
        .WithUsername("admin_portal")
        .WithPassword("integration-test-password")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task ExistingTeacherStudentAndGroupSurviveFullUpgradeWithScheduleBackfill()
    {
        var options = new DbContextOptionsBuilder<AdminPortalDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), npgsql =>
                npgsql.MigrationsAssembly(typeof(AdminPortalDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var dbContext = new AdminPortalDbContext(options);
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync(InitialMigration);

        var teacherUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var deletedStudentId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO users (
                id, email, normalized_email, password_hash, full_name, phone_number,
                role, status, failed_login_count, lockout_end, created_at, updated_at, deleted_at)
            VALUES (
                {teacherUserId}, {"legacy-teacher@example.test"}, {"LEGACY-TEACHER@EXAMPLE.TEST"},
                {"legacy-password-hash"}, {"Legacy Teacher"}, NULL,
                {"Teacher"}, {"Active"}, {0}, NULL, {now}, {now}, NULL)
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO students (
                id, student_code, full_name, nick_name, date_of_birth, gender, status,
                guardian_name, guardian_phone, note, created_at, updated_at, deleted_at)
            VALUES (
                {studentId}, {"LEGACY-001"}, {"Legacy Student"}, {"Legacy"},
                {new DateOnly(2021, 1, 2)}, NULL, {"Active"}, NULL, NULL,
                {"legacy note"}, {now}, {now}, NULL)
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO students (
                id, student_code, full_name, nick_name, date_of_birth, gender, status,
                guardian_name, guardian_phone, note, created_at, updated_at, deleted_at)
            VALUES (
                {deletedStudentId}, {"LEGACY-DELETED"}, {"Deleted Legacy Student"}, {"Deleted"},
                {new DateOnly(2020, 2, 3)}, NULL, {"Inactive"}, NULL, NULL,
                NULL, {now}, {now}, {now})
            """);

        await migrator.MigrateAsync(AttendanceMigration);
        await migrator.MigrateAsync(TeacherManagementMigration);
        dbContext.ChangeTracker.Clear();

        var profile = await dbContext.Teachers.AsNoTracking()
            .SingleAsync(x => x.UserId == teacherUserId);
        var groupId = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO student_groups (
                id, code, name, status, responsible_teacher_id, snapshot_version,
                snapshot_changed_at, created_at, updated_at, deleted_at)
            VALUES (
                {groupId}, {"LEGACY-GROUP"}, {"Legacy Group"}, {"Active"}, {profile.Id}, {4},
                {now}, {now}, {now}, NULL)
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE students SET group_id = {groupId} WHERE id = {studentId}
            """);
        await migrator.MigrateAsync(ScheduleMigration);
        var sheetId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        var attendanceDate = new DateOnly(2026, 8, 10);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO attendance_sheets (
                id, group_id, attendance_date, group_code_snapshot, group_name_snapshot,
                responsible_teacher_id, responsible_teacher_name_snapshot, snapshot_source,
                source_snapshot_version, recovery_reason, version, created_by_user_id,
                updated_by_user_id, created_at, updated_at)
            VALUES (
                {sheetId}, {groupId}, {attendanceDate}, {"LEGACY-GROUP"}, {"Legacy Group"},
                {profile.Id}, {"Legacy Teacher"}, {"CurrentSnapshot"}, {5}, NULL, {1},
                {teacherUserId}, {teacherUserId}, {now}, {now})
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO attendance_records (
                id, sheet_id, attendance_date, student_id, student_code_snapshot,
                full_name_snapshot, nick_name_snapshot, status, half_day_part,
                is_excused, duration_minutes, notes, updated_by_user_id, created_at, updated_at)
            VALUES (
                {recordId}, {sheetId}, {attendanceDate}, {studentId}, {"LEGACY-001"},
                {"Legacy Student"}, {"Legacy"}, {"AbsentHalfDay"}, {"Morning"},
                TRUE, NULL, {"legacy attendance note"}, {teacherUserId}, {now}, {now})
            """);
        await migrator.MigrateAsync(AttendanceUiMigration);
        dbContext.ChangeTracker.Clear();

        Assert.Equal(7, profile.AttendanceEditWindowDays);
        Assert.Equal($"GV-MIG-{profile.Id:N}".ToUpperInvariant(), profile.TeacherCode);
        Assert.Null(profile.Note);
        Assert.Equal(1, profile.Version);
        var legacyUser = await dbContext.Users.AsNoTracking().SingleAsync(x => x.Id == teacherUserId);
        Assert.Equal("Legacy Teacher", legacyUser.FullName);
        var legacyStudent = await dbContext.Students.AsNoTracking().SingleAsync(x => x.Id == studentId);
        Assert.Equal("LEGACY-001", legacyStudent.StudentCode);
        Assert.Equal("legacy note", legacyStudent.Note);
        Assert.Equal(groupId, legacyStudent.GroupId);
        Assert.Equal(StudyMode.FullDay, legacyStudent.StudyMode);
        Assert.Equal(63, legacyStudent.StudyWeekdayMask);
        Assert.Equal(1, legacyStudent.Version);
        var deletedStudent = await dbContext.Students.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(x => x.Id == deletedStudentId);
        Assert.Equal(StudyMode.FullDay, deletedStudent.StudyMode);
        Assert.Equal(63, deletedStudent.StudyWeekdayMask);
        Assert.Equal(1, deletedStudent.Version);
        var legacyGroup = await dbContext.StudentGroups.AsNoTracking().SingleAsync(x => x.Id == groupId);
        Assert.Equal(5, legacyGroup.SnapshotVersion);
        Assert.True(legacyGroup.SnapshotChangedAt > now);
        Assert.Contains(AttendanceMigration, await dbContext.Database.GetAppliedMigrationsAsync());
        Assert.Contains(TeacherManagementMigration, await dbContext.Database.GetAppliedMigrationsAsync());
        Assert.Contains(ScheduleMigration, await dbContext.Database.GetAppliedMigrationsAsync());
        Assert.Contains(AttendanceUiMigration, await dbContext.Database.GetAppliedMigrationsAsync());
        var legacyRecord = await dbContext.AttendanceRecords.AsNoTracking().SingleAsync(x => x.Id == recordId);
        Assert.Equal(AttendanceStatus.AbsentHalfDay, legacyRecord.Status);
        Assert.Equal(HalfDayPart.Morning, legacyRecord.HalfDayPart);
        Assert.Equal("legacy attendance note", legacyRecord.Notes);

        var invalidUnmarked = await Assert.ThrowsAsync<PostgresException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE attendance_records SET status = {"Unmarked"} WHERE id = {recordId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalidUnmarked.SqlState);

        var invalidMask = await Assert.ThrowsAsync<PostgresException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE students SET study_weekday_mask = {0} WHERE id = {studentId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalidMask.SqlState);
        var invalidMode = await Assert.ThrowsAsync<PostgresException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE students SET study_mode = {"Invalid"} WHERE id = {studentId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalidMode.SqlState);
        var invalidVersion = await Assert.ThrowsAsync<PostgresException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE students SET version = {0} WHERE id = {studentId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalidVersion.SqlState);
    }
}
