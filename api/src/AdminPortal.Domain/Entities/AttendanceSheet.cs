using AdminPortal.Domain.Enums;

namespace AdminPortal.Domain.Entities;

public sealed class AttendanceSheet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public StudentGroup Group { get; set; } = null!;
    public DateOnly AttendanceDate { get; set; }
    public required string GroupCodeSnapshot { get; set; }
    public required string GroupNameSnapshot { get; set; }
    public Guid ResponsibleTeacherIdSnapshot { get; set; }
    public Teacher ResponsibleTeacherSnapshot { get; set; } = null!;
    public required string ResponsibleTeacherNameSnapshot { get; set; }
    public AttendanceSnapshotSource SnapshotSource { get; set; }
    public int? SourceSnapshotVersion { get; set; }
    public string? HistoricalRecoveryReason { get; set; }
    public int Version { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public Guid UpdatedByUserId { get; set; }
    public User UpdatedByUser { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<AttendanceRecord> Records { get; } = [];
}
