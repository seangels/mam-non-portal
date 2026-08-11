using System.ComponentModel.DataAnnotations;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.Attendance;

public sealed record AttendanceContextResponse(
    DateOnly Date,
    DateOnly ServerDate,
    IReadOnlyList<AttendanceContextGroupResponse> Groups,
    int? AttendanceEditWindowDays,
    bool CanEdit,
    AttendanceReadOnlyReason? ReadOnlyReason);

public sealed record AttendanceContextGroupResponse(Guid Id, string Code, string Name, int StudentCount);
public sealed record AttendanceGroupResponse(Guid Id, string Code, string Name);
public sealed record AttendanceSummaryResponse(
    int RosterTotal,
    int Present,
    int Absent,
    int OneToOne,
    int Unmarked);

public sealed record AttendanceItemResponse(
    Guid? EntryId,
    Guid StudentId,
    string StudentCode,
    string FullName,
    string NickName,
    AttendanceStatus Status,
    HalfDayPart? HalfDayPart,
    bool? IsExcused,
    int? DurationMinutes,
    string? Notes,
    DateTimeOffset? UpdatedAt);

public sealed record AttendanceDailyResponse(
    DateOnly Date,
    DateOnly ServerDate,
    AttendanceGroupResponse Group,
    AttendanceSheetState SheetState,
    Guid? SheetId,
    int? SheetVersion,
    AttendanceSnapshotSource? SnapshotSource,
    int? CurrentSnapshotVersion,
    int? SourceSnapshotVersion,
    bool CanCreate,
    bool CanEdit,
    bool CanRecover,
    AttendanceReadOnlyReason? ReadOnlyReason,
    AttendanceSummaryResponse Summary,
    IReadOnlyList<AttendanceItemResponse> Items);

public sealed record AttendanceRecordRequest(
    Guid StudentId,
    AttendanceStatus Status,
    HalfDayPart? HalfDayPart,
    bool? IsExcused,
    int? DurationMinutes,
    [param: MaxLength(2000)] string? Notes);

public sealed record CreateAttendanceSheetRequest(
    Guid GroupId,
    DateOnly Date,
    [param: Range(1, int.MaxValue)] int ExpectedSnapshotVersion,
    [param: Required, MaxLength(100)] IReadOnlyList<AttendanceRecordRequest> Records);

public sealed record UpdateAttendanceSheetRequest(
    [param: Range(1, int.MaxValue)] int ExpectedVersion,
    [param: Required, MinLength(1), MaxLength(100)] IReadOnlyList<AttendanceRecordRequest> Records);

public sealed record HistoricalRecoveryRequest(
    Guid GroupId,
    DateOnly Date,
    Guid ResponsibleTeacherId,
    [param: Required, MinLength(1), MaxLength(100)] IReadOnlyList<AttendanceRecordRequest> Records,
    bool AcknowledgeHistoricalSnapshot,
    [param: Required, MaxLength(500)] string RecoveryReason);

public sealed class CandidateListQuery
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(200)] public string? Search { get; init; }
}

public sealed record HistoricalGroupCandidateResponse(
    Guid Id, string Code, string Name, GroupStatus Status, bool IsDeleted);
public sealed record HistoricalStudentCandidateResponse(
    Guid Id, string StudentCode, string FullName, string NickName, StudentStatus Status,
    bool IsDeleted, Guid? CurrentGroupId);
public sealed record HistoricalTeacherCandidateResponse(
    Guid Id, Guid UserId, string FullName, UserStatus Status, bool IsDeleted, bool IsCurrentTeacherRole);
