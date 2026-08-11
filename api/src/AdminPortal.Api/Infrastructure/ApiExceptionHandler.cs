using AdminPortal.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AdminPortal.Api.Infrastructure;

public sealed partial class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            AppValidationException => (StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Chưa xác thực"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Không đủ quyền"),
            NotFoundException => (StatusCodes.Status404NotFound, "Không tìm thấy dữ liệu"),
            ConflictException => (StatusCodes.Status409Conflict, "Xung đột dữ liệu"),
            _ => (StatusCodes.Status500InternalServerError, "Lỗi hệ thống")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, exception, httpContext.Request.Method, httpContext.Request.Path.Value);
        }

        var problem = exception is AppValidationException validationException
            ? new HttpValidationProblemDetails(validationException.Errors)
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = title;
        problem.Detail = status == StatusCodes.Status500InternalServerError
            ? "Đã xảy ra lỗi không mong muốn."
            : exception.Message;
        problem.Type = $"https://httpstatuses.com/{status}";

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string method, string? path);
}
