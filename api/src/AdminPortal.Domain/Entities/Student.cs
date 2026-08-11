using AdminPortal.Domain.Enums;

namespace AdminPortal.Domain.Entities;

public sealed class Student
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string StudentCode { get; set; }
    public required string FullName { get; set; }
    public required string NickName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public StudentStatus Status { get; set; } = StudentStatus.Active;
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? Note { get; set; }
    public Guid? GroupId { get; set; }
    public StudentGroup? Group { get; set; }
    public DateTimeOffset? GroupAssignedAt { get; set; }
    public Guid? GroupAssignedByUserId { get; set; }
    public User? GroupAssignedByUser { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public ICollection<AttendanceRecord> AttendanceRecords { get; } = [];
}
