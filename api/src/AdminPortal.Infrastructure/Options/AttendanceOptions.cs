namespace AdminPortal.Infrastructure.Options;

public sealed class AttendanceOptions
{
    public const string SectionName = "Attendance";
    public string BusinessTimeZone { get; init; } = "Asia/Ho_Chi_Minh";
}
