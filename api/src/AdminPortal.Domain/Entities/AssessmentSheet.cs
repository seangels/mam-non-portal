using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Domain.Entities;

public sealed class AssessmentSheet
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required]
    [StringLength(300)]
    public string Name { get; set; } = string.Empty;
    public required AssessmentSheetStatus AssessmentSheetStatus { get; set; } = AssessmentSheetStatus.Open;
    public Guid StudentId { get; set; }
    [ForeignKey(nameof(StudentId))]
    public Student Student { get; set; } = null!;
    [Column(TypeName = "jsonb")]
    public required StudentSnapshot StudentSnapshot { get; set; }
    public Guid? ResponsibleTeacherId { get; set; }
    [ForeignKey(nameof(ResponsibleTeacherId))]
    public Teacher? ResponsibleTeacher { get; set; }

    [StringLength(500)]
    public string? ResponsibleTeacherFullNameSnapshot { get; set; }

    

    [StringLength(2000)]
    public string? Note { get; set; }

    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public DateTimeOffset? DoneDate { get; set; }
    public DateTimeOffset? SubmissionDate { get; set; }
    [StringLength(2000)]
    public string? Feedback { get; set; }
    public string? PlanFileLinkPdf {get;set;}
    public string? ResultFileLinkPdf {get;set;}
    public string? AssessmentSheetSpreadsheetId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    [ForeignKey(nameof(UpdatedByUserId))]
    public User? UpdatedByUser { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
