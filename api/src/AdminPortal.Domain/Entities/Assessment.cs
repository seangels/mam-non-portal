using AdminPortal.Domain.Enums;

namespace AdminPortal.Domain.Entities;
public sealed class Assessment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid GroupLv1Id { get; set; }
    public AssessmentGroup GroupLv1 { get; set; } = null!;
    public Guid GroupLv2Id { get; set; }
    public AssessmentGroup GroupLv2 { get; set; } = null!;
    public Guid GroupLv3Id { get; set; }
    public AssessmentGroup GroupLv3 { get; set; } = null!;
    public int? RowIndex { get; set; }
    public string? Note { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public User UpdatedByUser { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
