using AdminPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace AdminPortal.IntegrationTests;

public sealed class MigrationUpgradeTests : IAsyncLifetime
{
    private const string InitialMigration = "20260811000000_InitialCreate";
    private const string AttendanceMigration = "20260811130802_AddAttendanceFoundation";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("admin_portal_upgrade_tests")
        .WithUsername("admin_portal")
        .WithPassword("integration-test-password")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task ExistingTeacherAndStudentSurviveAttendanceMigrationWithProfileBackfill()
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

        await migrator.MigrateAsync(AttendanceMigration);
        dbContext.ChangeTracker.Clear();

        var profile = await dbContext.Teachers.AsNoTracking()
            .SingleAsync(x => x.UserId == teacherUserId);
        Assert.Equal(7, profile.AttendanceEditWindowDays);
        var legacyUser = await dbContext.Users.AsNoTracking().SingleAsync(x => x.Id == teacherUserId);
        Assert.Equal("Legacy Teacher", legacyUser.FullName);
        var legacyStudent = await dbContext.Students.AsNoTracking().SingleAsync(x => x.Id == studentId);
        Assert.Equal("LEGACY-001", legacyStudent.StudentCode);
        Assert.Equal("legacy note", legacyStudent.Note);
        Assert.Null(legacyStudent.GroupId);
        Assert.Contains(AttendanceMigration, await dbContext.Database.GetAppliedMigrationsAsync());
    }
}
