using AdminPortal.Domain.Enums;

namespace AdminPortal.Domain.Entities;

public sealed class StudentGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public GroupStatus Status { get; set; } = GroupStatus.Active;
    public Guid? ResponsibleTeacherId { get; set; }
    public Teacher? ResponsibleTeacher { get; set; }
    public int SnapshotVersion { get; set; } = 1;
    public DateTimeOffset SnapshotChangedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public ICollection<Student> Students { get; } = [];
    public ICollection<AttendanceSheet> AttendanceSheets { get; } = [];
}
