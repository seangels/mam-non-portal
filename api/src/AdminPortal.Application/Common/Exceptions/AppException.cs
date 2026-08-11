namespace AdminPortal.Application.Common.Exceptions;

public abstract class AppException(string message) : Exception(message);

public sealed class NotFoundException(string message) : AppException(message);

public sealed class ConflictException(string message) : AppException(message);

public sealed class ForbiddenException(string message) : AppException(message);

public sealed class UnauthorizedException(string message) : AppException(message);

public sealed class AppValidationException(
    string message,
    IReadOnlyDictionary<string, string[]> errors) : AppException(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
