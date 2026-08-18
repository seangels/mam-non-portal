using System.ComponentModel.DataAnnotations;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.Assessments;

public sealed class AssessmentListQuery
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(200)] public string? Search { get; init; }
    public Guid? GroupLv1Id { get; init; }
    public Guid? GroupLv2Id { get; init; }
    public Guid? GroupLv3Id { get; init; }
    [MaxLength(40)] public string SortBy { get; init; } = "rowIndex";
    [RegularExpression("(?i)^(asc|desc)$")] public string SortOrder { get; init; } = "asc";
}

public sealed record AssessmentListItemResponse(
    Guid Id,
    string Code,
    string Name,
    string? Note,
    int? RowIndex,
    string GroupLv1Name,
    string GroupLv2Name,
    string GroupLv3Name);

public sealed record AssessmentGroupResponse(
    Guid Id,
    string Name,
    int Level);

public sealed record AssessmentDetailResponse(
    Guid Id,
    string Code,
    string Name,
    string? Note,
    int? RowIndex,
    string GroupLv1Name,
    string GroupLv2Name,
    string GroupLv3Name);


