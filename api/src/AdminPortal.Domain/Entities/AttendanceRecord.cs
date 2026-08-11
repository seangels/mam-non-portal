using AdminPortal.Domain.Enums;

namespace AdminPortal.Domain.Entities;

public sealed class AttendanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SheetId { get; set; }
    public AttendanceSheet Sheet { get; set; } = null!;
    public DateOnly AttendanceDate { get; set; }
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public required string StudentCodeSnapshot { get; set; }
    public required string StudentFullNameSnapshot { get; set; }
    public required string StudentNickNameSnapshot { get; set; }
    public AttendanceStatus Status { get; set; }
    public HalfDayPart? HalfDayPart { get; set; }
    public bool? IsExcused { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Notes { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public User UpdatedByUser { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
