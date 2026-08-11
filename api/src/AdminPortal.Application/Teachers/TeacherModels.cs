using System.ComponentModel.DataAnnotations;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.Teachers;

public sealed class TeacherListQuery
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(200)] public string? Search { get; init; }
    public UserStatus? Status { get; init; }
    public bool? Unassigned { get; init; }
    [MaxLength(40)] public string SortBy { get; init; } = "fullName";
    [RegularExpression("(?i)^(asc|desc)$")] public string SortOrder { get; init; } = "asc";
}

public sealed record TeacherResponse(
    Guid Id,
    Guid UserId,
    string FullName,
    UserStatus Status,
    int AttendanceEditWindowDays,
    int ResponsibleGroupCount);

public sealed record UpdateAttendancePolicyRequest(
    [param: Range(1, 7)] int AttendanceEditWindowDays);
