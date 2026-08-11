using System.Data;
using AdminPortal.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AdminPortal.Infrastructure.Persistence;

public sealed class AttendancePersistence(AdminPortalDbContext dbContext) : IAttendancePersistence
{
    public async Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new AppTransaction(await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken));

    public Task LockGroupsAsync(IEnumerable<Guid> groupIds, CancellationToken cancellationToken) =>
        LockManyAsync("student_groups", groupIds.Distinct().Order(), cancellationToken);

    public Task LockStudentAsync(Guid studentId, CancellationToken cancellationToken) =>
        LockManyAsync("students", [studentId], cancellationToken);

    public Task LockSheetAsync(Guid sheetId, CancellationToken cancellationToken) =>
        LockManyAsync("attendance_sheets", [sheetId], cancellationToken);

    private async Task LockManyAsync(string table, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var values = ids.ToArray();
        if (values.Length == 0) return;
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"SELECT id FROM {table} WHERE id = ANY(@ids) ORDER BY id FOR UPDATE";
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("ids", values));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) { }
    }

    private sealed class AppTransaction(IDbContextTransaction transaction) : IAppTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
