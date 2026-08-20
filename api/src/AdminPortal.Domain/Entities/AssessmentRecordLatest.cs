using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Domain.Entities;

public sealed class AssessmentRecordLatest
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey(nameof(AssessmentSheetLatestId))]
    public required Guid AssessmentSheetLatestId {get;set;}
    public required AssessmentSheetLatest AssessmentSheetLatest {get;set;}
    public int? AssessmentRowIndex {get;set;}

    [Column(TypeName = "jsonb")]
    public required AssessmentSnapshot AssessmentSnapshot { get; set; }
    [ForeignKey(nameof(AssessmentId))]
    public required Guid AssessmentId {get;set;}
    public required Assessment Assessment {get;set;}

    public AssessmentGrade? LatestGrade  { get; set; }
    [StringLength(2000)]
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
