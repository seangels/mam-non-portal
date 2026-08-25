using System.ComponentModel.DataAnnotations;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.Assessments;

public sealed class AssessmentListQuery
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 5000)] public int PageSize { get; init; } = 5000;
    [MaxLength(200)] public string? Search { get; init; }
    public string? GroupLv1Name { get; init; }
    public string? GroupLv2Name { get; init; }
    public string? GroupLv3Name { get; init; }
    public Guid? StudentId { get; init; }
    [MaxLength(40)] public string SortBy { get; init; } = "rowindex";
    [RegularExpression("(?i)^(asc|desc)$")] public string SortOrder { get; init; } = "asc";
}

public sealed record AssessmentListItemResponse(
    Guid Id,
    string Code,
    string Name,
    string? Note,
    int? RowIndex,
    string? GroupLv1Name,
    string? GroupLv2Name,
    string? GroupLv3Name,
    AssessmentGrade? LatestGrade,
    string? LatestNote
    );

public sealed record AssessmentDetailResponse(
    Guid Id,
    string Code,
    string Name,
    string? Note,
    int? RowIndex,
    string? GroupLv1Name,
    string? GroupLv2Name,
    string? GroupLv3Name
    );


