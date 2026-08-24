using System.ComponentModel.DataAnnotations.Schema;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Domain.Entities;

[ComplexType]
public sealed class StudentSnapshot
{
    public string? StudentCode { get; set; }
    public string? FullName { get; set; }
    public string? NickName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
}
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
    /// <summary>Id thư mục Google Drive riêng của học sinh (lazy, tạo lần đầu khi cần file [F01]/PDF của AssessmentSheet).</summary>
    public string? DriveFolderId { get; set; }
    public StudyMode StudyMode { get; set; }
    public short StudyWeekdayMask { get; set; }
    public int Version { get; set; } = 1;
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
