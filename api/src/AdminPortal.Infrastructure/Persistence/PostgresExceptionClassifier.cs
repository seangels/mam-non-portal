using AdminPortal.Application.Common.Interfaces;
using Npgsql;

namespace AdminPortal.Infrastructure.Persistence;

public sealed class PostgresExceptionClassifier : IDatabaseExceptionClassifier
{
    public bool IsUniqueViolation(Exception exception, string constraintName)
    {
        var current = exception;
        while (current is not null)
        {
            if (current is PostgresException postgresException &&
                postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
                string.Equals(postgresException.ConstraintName, constraintName, StringComparison.Ordinal))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}
