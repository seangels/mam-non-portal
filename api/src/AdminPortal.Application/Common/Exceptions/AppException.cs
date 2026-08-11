namespace AdminPortal.Application.Common.Exceptions;

public abstract class AppException(
    string message,
    string? code = null,
    IReadOnlyDictionary<string, object?>? extensions = null) : Exception(message)
{
    public string? Code { get; } = code;
    public IReadOnlyDictionary<string, object?> Extensions { get; } =
        extensions ?? new Dictionary<string, object?>();
}

public sealed class NotFoundException(string message) : AppException(message);

public sealed class ConflictException(
    string message,
    string? code = null,
    IReadOnlyDictionary<string, object?>? extensions = null) : AppException(message, code, extensions);

public sealed class ForbiddenException(
    string message,
    string? code = null,
    IReadOnlyDictionary<string, object?>? extensions = null) : AppException(message, code, extensions);

public sealed class UnauthorizedException(string message) : AppException(message);

public sealed class AppValidationException(
    string message,
    IReadOnlyDictionary<string, string[]> errors) : AppException(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
