using System.ComponentModel.DataAnnotations;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.Users;

public sealed class UserListQuery
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(200)] public string? Search { get; init; }
    public UserStatus? Status { get; init; }
    public UserRole? Role { get; init; }
    public DateOnly? CreatedFrom { get; init; }
    public DateOnly? CreatedTo { get; init; }
    [MaxLength(30)] public string SortBy { get; init; } = "createdAt";
    [RegularExpression("(?i)^(asc|desc)$")] public string SortOrder { get; init; } = "desc";
}

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FullName,
    string? PhoneNumber,
    UserRole Role,
    UserStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateUserRequest(
    [param: Required, EmailAddress, MaxLength(255)] string Email,
    [param: Required, MaxLength(200)] string FullName,
    [param: MaxLength(30)] string? PhoneNumber,
    UserRole Role,
    UserStatus Status,
    [param: Required, MaxLength(128)] string Password);

public sealed record UpdateUserRequest(
    [param: Required, EmailAddress, MaxLength(255)] string Email,
    [param: Required, MaxLength(200)] string FullName,
    [param: MaxLength(30)] string? PhoneNumber,
    UserRole Role,
    UserStatus Status);

public sealed record ChangePasswordRequest(
    [param: Required, MaxLength(128)] string Password);
