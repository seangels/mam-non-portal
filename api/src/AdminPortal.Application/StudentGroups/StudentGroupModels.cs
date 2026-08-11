using System.ComponentModel.DataAnnotations;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.StudentGroups;

public sealed class StudentGroupListQuery
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(200)] public string? Search { get; init; }
    public GroupStatus? Status { get; init; }
    public bool? Unassigned { get; init; }
    [MaxLength(40)] public string SortBy { get; init; } = "createdAt";
    [RegularExpression("(?i)^(asc|desc)$")] public string SortOrder { get; init; } = "desc";
}

public sealed record StudentGroupResponse(
    Guid Id,
    string Code,
    string Name,
    GroupStatus Status,
    Guid? ResponsibleTeacherId,
    string? ResponsibleTeacherName,
    int StudentCount,
    int SnapshotVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateStudentGroupRequest(
    [param: Required, MaxLength(50)] string Code,
    [param: Required, MaxLength(200)] string Name,
    GroupStatus Status);

public sealed record UpdateStudentGroupRequest(
    [param: Required, MaxLength(50)] string Code,
    [param: Required, MaxLength(200)] string Name,
    GroupStatus Status);

public sealed record AssignResponsibleTeacherRequest(Guid? TeacherId);
