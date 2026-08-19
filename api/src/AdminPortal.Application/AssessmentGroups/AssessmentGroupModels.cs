using System.ComponentModel.DataAnnotations;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.AssessmentGroups;

public sealed class AssessmentGroupListQuery
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 1000)] public int PageSize { get; init; } = 100;
    [MaxLength(200)] public string? Search { get; init; }
    public int? Level { get; init; }
    public string? ParentName { get; init; }
    public string? ParentParentName { get; init; }
    [MaxLength(40)] public string SortBy { get; init; } = "displayorder";
    [RegularExpression("(?i)^(asc|desc)$")] public string SortOrder { get; init; } = "asc";
}

public sealed class AssessmentGroupListItemResponse
{
    public string Name {get;set;} = string.Empty;
    public int? Level {get;set;}
    public int? DisplayOrder {get;set;}
    public string? ParentName {get;set;}
}
