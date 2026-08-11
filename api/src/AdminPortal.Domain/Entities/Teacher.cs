namespace AdminPortal.Domain.Entities;

public sealed class Teacher
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public short AttendanceEditWindowDays { get; set; } = 7;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<StudentGroup> ResponsibleGroups { get; } = [];
}
