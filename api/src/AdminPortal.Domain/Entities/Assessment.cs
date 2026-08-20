using System.ComponentModel.DataAnnotations.Schema;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Domain.Entities;
[ComplexType]
public sealed class AssessmentSnapshot
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? GroupLv1Name { get; set; }
    public string? GroupLv2Name { get; set; }
    public string? GroupLv3Name { get; set; }
    public int? RowIndex { get; set; }
}
public sealed class Assessment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? GroupLv1Name { get; set; }
    public string? GroupLv2Name { get; set; }
    public string? GroupLv3Name { get; set; }
    public int? RowIndex { get; set; }
    public string? Note { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public User UpdatedByUser { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
