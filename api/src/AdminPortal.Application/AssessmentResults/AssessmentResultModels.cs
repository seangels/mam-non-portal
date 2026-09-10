using System.ComponentModel.DataAnnotations;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.AssessmentResults;

public sealed record AssessmentResultsStudentResponse(
    Guid Id,
    string StudentCode,
    string FullName,
    string NickName,
    DateOnly DateOfBirth,
    StudentStatus Status);

public sealed record AssessmentResultItemResponse(
    Guid AssessmentId,
    string Code,
    string Name,
    string? GroupLv1Name,
    string? GroupLv2Name,
    string? GroupLv3Name,
    int? RowIndex,
    AssessmentGrade? Grade,
    string? Note,
    string Version);

public sealed record AssessmentResultsResponse(
    AssessmentResultsStudentResponse Student,
    IReadOnlyList<AssessmentResultItemResponse> Items);

public sealed record UpdateAssessmentResultsRequest(
    [param: Required, MaxLength(5000)] IReadOnlyList<UpdateAssessmentResultItemRequest> Items);

public sealed record UpdateAssessmentResultItemRequest(
    Guid AssessmentId,
    [param: Required, MinLength(1), MaxLength(200)] string ExpectedVersion,
    AssessmentGrade? Grade,
    [param: MaxLength(2000)] string? Note);

public sealed record AssessmentResultSourceTarget(
    Guid AssessmentId,
    string Code,
    string Name);

public sealed record AssessmentResultSourceValue(
    Guid AssessmentId,
    AssessmentGrade? Grade,
    string? Note,
    string Version);

public sealed record AssessmentResultSourceUpdate(
    Guid AssessmentId,
    string ExpectedVersion,
    AssessmentGrade? Grade,
    string? Note);

public sealed record AssessmentResultSourceCellChange(
    Guid AssessmentId,
    string AssessmentCode,
    string AssessmentName,
    string Cell,
    string Kind,
    string? CurrentValue,
    string? NewValue);

public sealed record AssessmentResultSourceWriteResult(
    IReadOnlyList<AssessmentResultSourceCellChange> Changes);
