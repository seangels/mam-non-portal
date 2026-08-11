namespace AdminPortal.Domain.Enums;

public enum AttendanceReadOnlyReason
{
    FutureDate,
    AttendanceEditWindowExceeded,
    HistoricalSnapshotUnavailable,
    GroupInactive,
    ResponsibleTeacherRequired,
    NoScheduledStudents
}
