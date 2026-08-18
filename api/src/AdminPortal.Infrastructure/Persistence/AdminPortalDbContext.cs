using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdminPortal.Infrastructure.Persistence;

public sealed class AdminPortalDbContext(DbContextOptions<AdminPortalDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<StudentGroup> StudentGroups => Set<StudentGroup>();
    public DbSet<AttendanceSheet> AttendanceSheets => Set<AttendanceSheet>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentGroup> AssessmentGroups => Set<AssessmentGroup>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdminPortalDbContext).Assembly);
    }
}
