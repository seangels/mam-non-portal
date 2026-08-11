namespace AdminPortal.Application.Common.Interfaces;

public interface IDatabaseExceptionClassifier
{
    bool IsUniqueViolation(Exception exception, string constraintName);
}
