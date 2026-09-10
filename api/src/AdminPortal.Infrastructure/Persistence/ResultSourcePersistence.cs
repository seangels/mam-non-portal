using System.Data;
using AdminPortal.Application.AssessmentResults;
using AdminPortal.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AdminPortal.Infrastructure.Persistence;

public sealed class ResultSourcePersistence(AdminPortalDbContext dbContext) : IResultSourcePersistence
{
    private const int CatalogLockNamespace = 1095981138;
    private const int StudentLockNamespace = 1095981139;
    private const int CatalogLockKey = 1;

    public async Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new AppTransaction(await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken));

    public Task LockCatalogSharedAsync(CancellationToken cancellationToken) =>
        ExecuteLockAsync(
            "SELECT pg_advisory_xact_lock_shared(@namespace, @key)",
            CatalogLockNamespace,
            CatalogLockKey,
            cancellationToken);

    public Task LockCatalogExclusiveAsync(CancellationToken cancellationToken) =>
        ExecuteLockAsync(
            "SELECT pg_advisory_xact_lock(@namespace, @key)",
            CatalogLockNamespace,
            CatalogLockKey,
            cancellationToken);

    public Task LockStudentAsync(Guid studentId, CancellationToken cancellationToken) =>
        ExecuteLockAsync(
            "SELECT pg_advisory_xact_lock(@namespace, @key)",
            StudentLockNamespace,
            BitConverter.ToInt32(studentId.ToByteArray(), 0),
            cancellationToken);

    private async Task ExecuteLockAsync(string sql, int lockNamespace, int key, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = sql;
        command.Parameters.Add(new NpgsqlParameter<int>("namespace", lockNamespace));
        command.Parameters.Add(new NpgsqlParameter<int>("key", key));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed class AppTransaction(IDbContextTransaction transaction) : IAppTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
