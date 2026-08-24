using System.ComponentModel.DataAnnotations;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.Students;

public sealed class StudentListQuery
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(200)] public string? Search { get; init; }
    public StudentStatus? Status { get; init; }
    public Gender? Gender { get; init; }
    public DateOnly? DateOfBirthFrom { get; init; }
    public DateOnly? DateOfBirthTo { get; init; }
    public Guid? GroupId { get; init; }
    public bool? Unassigned { get; init; }
    public StudyMode? StudyMode { get; init; }
    public StudyWeekday? StudyWeekday { get; init; }
    [MaxLength(30)] public string SortBy { get; init; } = "createdAt";
    [RegularExpression("(?i)^(asc|desc)$")] public string SortOrder { get; init; } = "desc";
}

public sealed record StudentResponse(
    Guid Id,
    string StudentCode,
    string FullName,
    string NickName,
    DateOnly DateOfBirth,
    Gender? Gender,
    StudentStatus Status,
    string? GuardianName,
    string? GuardianPhone,
    string? Note,
    string? DriveFolderId,
    Guid? GroupId,
    string? GroupCode,
    string? GroupName,
    string? ResponsibleTeacherName,
    StudyScheduleResponse StudySchedule,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record StudyScheduleResponse(
    StudyMode Mode,
    IReadOnlyList<StudyWeekday> Weekdays);

public sealed record StudyScheduleRequest(
    StudyMode Mode,
    [param: Required, MinLength(1), MaxLength(6)] IReadOnlyList<StudyWeekday> Weekdays);

public sealed record AssignStudentGroupRequest(
    Guid? GroupId,
    [param: Range(1, int.MaxValue)] int ExpectedVersion);

public sealed record CreateStudentRequest(
    [param: Required, MaxLength(50)] string StudentCode,
    [param: Required, MaxLength(200)] string FullName,
    [param: Required, MaxLength(200)] string NickName,
    DateOnly DateOfBirth,
    Gender? Gender,
    StudentStatus Status,
    [param: MaxLength(200)] string? GuardianName,
    [param: MaxLength(30)] string? GuardianPhone,
    [param: MaxLength(2000)] string? Note,
    [param: MaxLength(200)] string? DriveFolderId,
    [param: Required] StudyScheduleRequest StudySchedule);

public sealed record UpdateStudentRequest(
    [param: Required, MaxLength(50)] string StudentCode,
    [param: Required, MaxLength(200)] string FullName,
    [param: Required, MaxLength(200)] string NickName,
    DateOnly DateOfBirth,
    Gender? Gender,
    StudentStatus Status,
    [param: MaxLength(200)] string? GuardianName,
    [param: MaxLength(30)] string? GuardianPhone,
    [param: MaxLength(2000)] string? Note,
    [param: MaxLength(200)] string? DriveFolderId,
    [param: Required] StudyScheduleRequest StudySchedule,
    [param: Range(1, int.MaxValue)] int ExpectedVersion);
