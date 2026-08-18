using System.ComponentModel.DataAnnotations;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.AssessmentGroups;

public sealed class AssessmentGroupListQuery
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(200)] public string? Search { get; init; }
    public int? Level { get; init; }
    public Guid? ParentId { get; init; }
    [MaxLength(40)] public string SortBy { get; init; } = "displayorder";
    [RegularExpression("(?i)^(asc|desc)$")] public string SortOrder { get; init; } = "asc";
}

public sealed record AssessmentGroupListItemResponse(
    Guid Id,
    string Name,
    int? Level,
    int? DisplayOrder
    );
public sealed record AssessmentGroupDetailResponse(
    Guid Id,
    string Name,
    int? Level,
    int? DisplayOrder,
    Guid? ParentId
    );


