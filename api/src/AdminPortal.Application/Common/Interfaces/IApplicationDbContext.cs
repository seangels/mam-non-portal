using AdminPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdminPortal.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Student> Students { get; }
    DbSet<AuthSession> AuthSessions { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
