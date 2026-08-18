using AdminPortal.Domain.Enums;

namespace AdminPortal.Domain.Entities;
public sealed class Assessment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public AssessmentGroup Group { get; set; } = null!;
    public int? RowIndex { get; set; }
    public string? Notes { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public User UpdatedByUser { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
