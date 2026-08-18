using AdminPortal.Domain.Enums;

namespace AdminPortal.Domain.Entities;
public sealed class AssessmentGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required int Level { get; set; }
    public int? DisplayOrder { get; set; }
    public Guid? ParentId { get; set; } = null!;
    public Guid UpdatedByUserId { get; set; }
    public User UpdatedByUser { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<Assessment> AssessmentsLv1 { get; } = [];
    public ICollection<Assessment> AssessmentsLv2 { get; } = [];
    public ICollection<Assessment> AssessmentsLv3 { get; } = [];
}
