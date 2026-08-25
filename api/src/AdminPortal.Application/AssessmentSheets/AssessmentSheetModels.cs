using System.ComponentModel.DataAnnotations;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.AssessmentSheets;

public sealed class AssessmentSheetListQuery
{
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(200)] public string? Search { get; init; }
    public Guid? StudentId { get; init; }
    public DateTimeOffset? DateFrom {get; init; }
    public DateTimeOffset? DateTo {get; init; }
    public AssessmentSheetStatus? Status { get; init; }
    [MaxLength(40)] public string SortBy { get; init; } = "updatedAt";
    [RegularExpression("(?i)^(asc|desc)$")] public string SortOrder { get; init; } = "desc";
}


public sealed record AssessmentSheetListItemResponse(
    Guid Id,
    AssessmentSheetStatus Status,
    Guid StudentId,
    string? StudentCode,
    string? StudentFullName,
    Guid? ResponsibleTeacherId,
    string? ResponsibleTeacherFullName,
    DateTimeOffset? StartDate,
    DateTimeOffset? DueDate,
    DateTimeOffset? DoneDate,
    DateTimeOffset? SubmissionDate,
    string? AssessmentSheetSpreadsheetId,
    string? PlanFileLinkPdf,
    string? ResultFileLinkPdf,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AssessmentSheetDetailResponse(
    Guid Id,
    AssessmentSheetStatus Status,
    Guid StudentId,
    AssessmentSheetStudentSnapshotResponse StudentSnapshot,
    Guid? ResponsibleTeacherId,
    string? ResponsibleTeacherFullName,
    string? Note,
    DateTimeOffset? StartDate,
    DateTimeOffset? DueDate,
    DateTimeOffset? DoneDate,
    DateTimeOffset? SubmissionDate,
    string? Feedback,
    string? AssessmentSheetSpreadsheetId,
    string? PlanFileLinkPdf,
    string? ResultFileLinkPdf,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AssessmentSheetRecordResponse> Records);

public sealed record AssessmentSheetStudentSnapshotResponse(
    string? StudentCode,
    string? FullName,
    string? NickName,
    DateOnly? DateOfBirth,
    Gender? Gender);

public sealed record AssessmentSheetRecordResponse(
    Guid Id,
    Guid AssessmentSheetId,
    int? AssessmentRowIndex,
    AssessmentSnapshotResponse Assessment,
    AssessmentGrade? PlanGrade,
    string? PlanNote,
    AssessmentGrade? FinalGrade,
    string? FinalNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AssessmentSnapshotResponse(
    string Code,
    string Name,
    string? GroupLv1Name,
    string? GroupLv2Name,
    string? GroupLv3Name,
    int? RowIndex);

public sealed record AssessmentPlanCandidateResponse(
    Guid Id,
    string Code,
    string Name,
    string? GroupLv1Name,
    string? GroupLv2Name,
    string? GroupLv3Name,
    int? RowIndex,
    AssessmentGrade? LatestGrade);

public sealed record CreateAssessmentSheetRequest(
    Guid StudentId,
    Guid? ResponsibleTeacherId,
    [param: MaxLength(2000)] string? Note,
    DateTimeOffset? StartDate,
    DateTimeOffset? DueDate,
    [param: Required, MinLength(1), MaxLength(5000)] IReadOnlyList<CreateAssessmentSheetRecordRequest> Records);

public sealed record CreateAssessmentSheetRecordRequest(
    Guid AssessmentId,
    AssessmentGrade? LatestGrade,
    [param: MaxLength(2000)] string? Note);

public sealed record UpdateAssessmentSheetRequest(
    Guid? ResponsibleTeacherId,
    [param: MaxLength(2000)] string? Note,
    DateTimeOffset? StartDate,
    DateTimeOffset? DueDate,
    [param: MaxLength(2000)] string? Feedback);

public sealed record ReplaceAssessmentSheetRecordsRequest(
    [param: Required, MinLength(1), MaxLength(5000)] IReadOnlyList<AssessmentSheetRecordRequest> Records);

public sealed record AssessmentSheetRecordRequest(
    Guid AssessmentId,
    AssessmentGrade? PlanGrade,
    [param: MaxLength(2000)] string? PlanNote,
    AssessmentGrade? FinalGrade,
    [param: MaxLength(2000)] string? FinalNote);

public sealed record UpdateAssessmentSheetStatusRequest(AssessmentSheetStatus Status);
