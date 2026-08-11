using AdminPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AdminPortal.Maintenance;

public sealed partial class RetentionCleanup(
    AdminPortalDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<RetentionCleanup> logger)
{
    private const int BatchSize = 1000;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var auditCutoff = now.AddDays(-90);
        var sessionCutoff = now.AddDays(-30);
        var auditDeleted = await DeleteAuditLogsAsync(auditCutoff, cancellationToken);
        var sessionsDeleted = await DeleteAuthSessionsAsync(sessionCutoff, cancellationToken);
        LogCompleted(logger, auditDeleted, sessionsDeleted);
    }

    private async Task<int> DeleteAuditLogsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var total = 0;
        int affected;
        do
        {
            affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                WITH doomed AS (
                    SELECT id FROM audit_logs
                    WHERE created_at < {cutoff}
                    ORDER BY created_at
                    LIMIT {BatchSize}
                )
                DELETE FROM audit_logs target USING doomed
                WHERE target.id = doomed.id
                """, cancellationToken);
            total += affected;
        } while (affected == BatchSize);

        return total;
    }

    private async Task<int> DeleteAuthSessionsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var total = 0;
        int affected;
        do
        {
            affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                WITH doomed AS (
                    SELECT id FROM auth_sessions
                    WHERE COALESCE(revoked_at, refresh_token_expires_at) < {cutoff}
                    ORDER BY COALESCE(revoked_at, refresh_token_expires_at)
                    LIMIT {BatchSize}
                )
                DELETE FROM auth_sessions target USING doomed
                WHERE target.id = doomed.id
                """, cancellationToken);
            total += affected;
        } while (affected == BatchSize);

        return total;
    }

    [LoggerMessage(
        EventId = 100,
        Level = LogLevel.Information,
        Message = "Retention cleanup completed: AuditLogs={AuditLogsDeleted}, AuthSessions={AuthSessionsDeleted}")]
    private static partial void LogCompleted(ILogger logger, int auditLogsDeleted, int authSessionsDeleted);
}
