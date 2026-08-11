using System.ComponentModel.DataAnnotations;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.Teachers;

public sealed class TeacherListQuery
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(200)] public string? Search { get; init; }
    public UserStatus? Status { get; init; }
    public Guid? GroupId { get; init; }
    public bool? Unassigned { get; init; }
    [MaxLength(40)] public string SortBy { get; init; } = "fullName";
    [RegularExpression("(?i)^(asc|desc)$")] public string SortOrder { get; init; } = "asc";
}

public sealed record TeacherListItemResponse(
    Guid Id,
    Guid UserId,
    string TeacherCode,
    string FullName,
    string Email,
    string? PhoneNumber,
    UserStatus Status,
    int AttendanceEditWindowDays,
    int ResponsibleGroupCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);

public sealed record TeacherGroupSummaryResponse(
    Guid Id,
    string Code,
    string Name,
    GroupStatus Status,
    int StudentCount);

public sealed record TeacherDetailResponse(
    Guid Id,
    Guid UserId,
    string TeacherCode,
    string FullName,
    string Email,
    string? PhoneNumber,
    UserStatus Status,
    int AttendanceEditWindowDays,
    int ResponsibleGroupCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version,
    string? Note,
    IReadOnlyList<TeacherGroupSummaryResponse> ResponsibleGroups);

public sealed record CreateTeacherRequest(
    [param: Required, MaxLength(50)] string TeacherCode,
    [param: Required, MaxLength(200)] string FullName,
    [param: Required, EmailAddress, MaxLength(255)] string Email,
    [param: MaxLength(30)] string? PhoneNumber,
    UserStatus Status,
    [param: Required, MaxLength(128)] string Password,
    [param: MaxLength(2000)] string? Note);

public sealed record UpdateTeacherRequest(
    [param: Required, MaxLength(50)] string TeacherCode,
    [param: Required, MaxLength(200)] string FullName,
    [param: Required, EmailAddress, MaxLength(255)] string Email,
    [param: MaxLength(30)] string? PhoneNumber,
    UserStatus Status,
    [param: MaxLength(2000)] string? Note,
    [param: Range(1, int.MaxValue)] int ExpectedVersion);

public sealed record UpdateAttendancePolicyRequest(
    int AttendanceEditWindowDays,
    [param: Range(1, int.MaxValue)] int ExpectedVersion);
