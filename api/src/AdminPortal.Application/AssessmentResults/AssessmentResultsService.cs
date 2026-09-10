using System.Text.Json;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.GoogleSheets;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AdminPortal.Application.AssessmentResults;

public sealed class AssessmentResultsService(
    IApplicationDbContext dbContext,
    ICurrentActor currentActor,
    TimeProvider timeProvider,
    IGoogleSheetsService googleSheetsService,
    IResultSourcePersistence resultSourcePersistence,
    IAssessmentLatestMirrorUpdater latestMirrorUpdater) : IAssessmentResultsService
{
    public async Task<AssessmentResultsResponse> GetAsync(Guid studentId, CancellationToken cancellationToken)
    {
        EnsureManager(currentActor.GetRequired());
        var student = await FindStudentAsync(studentId, cancellationToken);
        var assessments = await LoadAssessmentsAsync(cancellationToken);
        var databaseValues = await LoadDatabaseValuesAsync(student.Id, cancellationToken);
        return BuildResponse(student, assessments, databaseValues);
    }

    public async Task<AssessmentResultsResponse> UpdateAsync(
        Guid studentId,
        UpdateAssessmentResultsRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        EnsureManager(actor);
        ValidateRequest(request);

        await using var transaction = await resultSourcePersistence.BeginTransactionAsync(cancellationToken);
        await resultSourcePersistence.LockCatalogSharedAsync(cancellationToken);
        await resultSourcePersistence.LockStudentAsync(studentId, cancellationToken);

        var student = await FindStudentAsync(studentId, cancellationToken);
        var assessments = await LoadAssessmentsAsync(cancellationToken);
        var assessmentById = assessments.ToDictionary(x => x.Id);
        var missingIds = request.Items.Select(x => x.AssessmentId)
            .Where(x => !assessmentById.ContainsKey(x))
            .Distinct()
            .ToArray();
        if (missingIds.Length > 0)
            throw new NotFoundException("Không tìm thấy mục đánh giá.", ProblemCodes.AssessmentNotFound);

        var databaseValues = await LoadDatabaseValuesAsync(student.Id, cancellationToken);
        var databaseValueByAssessmentId = databaseValues.ToDictionary(x => x.AssessmentId);
        var versionConflicts = request.Items
            .Where(x => !string.Equals(
                x.ExpectedVersion,
                CreateDatabaseVersion(assessmentById[x.AssessmentId], databaseValueByAssessmentId.GetValueOrDefault(x.AssessmentId)),
                StringComparison.Ordinal))
            .Select(x =>
            {
                var current = databaseValueByAssessmentId.GetValueOrDefault(x.AssessmentId);
                return new
                {
                    x.AssessmentId,
                    CurrentVersion = CreateDatabaseVersion(assessmentById[x.AssessmentId], current),
                    CurrentGrade = current?.Grade,
                    CurrentNote = current?.Note
                };
            })
            .ToList();
        if (versionConflicts.Count > 0)
        {
            throw new ConflictException(
                "Kết quả trong dữ liệu portal đã thay đổi. Vui lòng tải lại dữ liệu.",
                ProblemCodes.AssessmentResultsVersionConflict,
                new Dictionary<string, object?> { ["conflicts"] = versionConflicts });
        }

        var normalizedUpdates = request.Items.Select(x =>
        {
            var current = databaseValueByAssessmentId.GetValueOrDefault(x.AssessmentId);
            return new AssessmentResultSourceUpdate(
                x.AssessmentId,
                current?.Grade,
                current?.Note,
                x.Grade,
                AssessmentResultRules.NormalizeNote(x.Note));
        }).ToList();
        var requestedTargets = request.Items
            .Select(x => ToSourceTarget(assessmentById[x.AssessmentId]))
            .ToList();

        var writeResult = await googleSheetsService.UpdateAssessmentResultsInSourceAsync(
            student.StudentCode,
            requestedTargets,
            normalizedUpdates,
            cancellationToken);

        IReadOnlyList<DatabaseResultValue> canonicalValues;
        try
        {
            await latestMirrorUpdater.ApplyAsync(
                student.Id,
                normalizedUpdates.Select(x => new AssessmentLatestMirrorValue(x.AssessmentId, x.Grade, x.Note)).ToList(),
                actor,
                cancellationToken);
            AddAudits(student, writeResult.Changes, actor);
            await dbContext.SaveChangesAsync(cancellationToken);
            canonicalValues = await LoadDatabaseValuesAsync(student.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (writeResult.Changes.Count > 0)
        {
            throw new NormalException(
                "Google Sheet đã được cập nhật nhưng không thể cập nhật dữ liệu gần nhất trong portal.",
                ProblemCodes.AssessmentResultsPostWriteFailed,
                new Dictionary<string, object?>
                {
                    ["googleWriteSucceeded"] = true,
                    ["failureType"] = ex.GetType().Name
                });
        }

        return BuildResponse(student, assessments, canonicalValues);
    }

    private void AddAudits(
        Student student,
        IReadOnlyList<AssessmentResultSourceCellChange> changes,
        Common.Models.ActorContext actor)
    {
        var now = timeProvider.GetUtcNow();
        AddAudit(actor, "AssessmentResults.Updated", student.Id, null, new
        {
            student.Id,
            student.StudentCode,
            ChangedCellCount = changes.Count,
            ChangedAssessmentCount = changes.Select(x => x.AssessmentId).Distinct().Count()
        }, now);
        foreach (var change in changes)
        {
            AddAudit(actor, "AssessmentResults.SourceCellUpdated", student.Id,
                new
                {
                    change.AssessmentId,
                    change.AssessmentCode,
                    change.AssessmentName,
                    change.Cell,
                    change.Kind,
                    Value = change.CurrentValue
                },
                new
                {
                    change.AssessmentId,
                    change.AssessmentCode,
                    change.AssessmentName,
                    change.Cell,
                    change.Kind,
                    Value = change.NewValue
                }, now);
        }
    }

    private void AddAudit(
        Common.Models.ActorContext actor,
        string action,
        Guid entityId,
        object? oldValues,
        object? newValues,
        DateTimeOffset now) => dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor.UserId,
            Action = action,
            EntityType = "AssessmentResults",
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = actor.IpAddress,
            CreatedAt = now
        });

    private async Task<Student> FindStudentAsync(Guid studentId, CancellationToken cancellationToken) =>
        await dbContext.Students.SingleOrDefaultAsync(x => x.Id == studentId, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy học sinh.", ProblemCodes.StudentNotFound);

    private async Task<List<Assessment>> LoadAssessmentsAsync(CancellationToken cancellationToken) =>
        await dbContext.Assessments.AsNoTracking()
            .OrderBy(x => x.GroupLv1Name)
            .ThenBy(x => x.GroupLv2Name)
            .ThenBy(x => x.RowIndex)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);

    private async Task<List<DatabaseResultValue>> LoadDatabaseValuesAsync(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var latestId = await dbContext.AssessmentSheetLatests.AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (latestId is null)
            return [];

        return await dbContext.AssessmentRecordLatests.AsNoTracking()
            .Where(x => x.AssessmentSheetLatestId == latestId.Value)
            .Select(x => new DatabaseResultValue(x.AssessmentId, x.Id, x.LatestGrade, x.Note, x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    private static AssessmentResultSourceTarget ToSourceTarget(Assessment assessment) =>
        new(assessment.Id, assessment.Code, assessment.Name);

    private static AssessmentResultsResponse BuildResponse(
        Student student,
        IReadOnlyList<Assessment> assessments,
        IReadOnlyList<DatabaseResultValue> databaseValues)
    {
        var valueByAssessmentId = databaseValues.ToDictionary(x => x.AssessmentId);
        return new AssessmentResultsResponse(
            new AssessmentResultsStudentResponse(
                student.Id,
                student.StudentCode,
                student.FullName,
                student.NickName,
                student.DateOfBirth,
                student.Status),
            assessments.Select(assessment =>
            {
                var value = valueByAssessmentId.GetValueOrDefault(assessment.Id);
                return new AssessmentResultItemResponse(
                    assessment.Id,
                    assessment.Code,
                    assessment.Name,
                    assessment.GroupLv1Name,
                    assessment.GroupLv2Name,
                    assessment.GroupLv3Name,
                    assessment.RowIndex,
                    value?.Grade,
                    value?.Note,
                    CreateDatabaseVersion(assessment, value));
            }).ToList());
    }

    private static string CreateDatabaseVersion(Assessment assessment, DatabaseResultValue? value) =>
        AssessmentResultRules.CreateDatabaseVersion(
            assessment,
            value?.RecordId,
            value?.Grade,
            value?.Note,
            value?.UpdatedAt);

    private static void ValidateRequest(UpdateAssessmentResultsRequest request)
    {
        if (request.Items.Count == 0 ||
            request.Items.Any(x => x.AssessmentId == Guid.Empty) ||
            request.Items.Select(x => x.AssessmentId).Distinct().Count() != request.Items.Count)
        {
            throw new AppValidationException(
                "Danh sách kết quả không hợp lệ.",
                new Dictionary<string, string[]>
                {
                    ["items"] = ["Phải có ít nhất một mục hợp lệ và không được trùng assessmentId."]
                });
        }
    }

    private static void EnsureManager(Common.Models.ActorContext actor)
    {
        if (actor.Role is not (UserRole.SuperAdmin or UserRole.Admin))
            throw new ForbiddenException("Không đủ quyền cập nhật kết quả trực tiếp.");
    }

    private sealed record DatabaseResultValue(
        Guid AssessmentId,
        Guid RecordId,
        AssessmentGrade? Grade,
        string? Note,
        DateTimeOffset UpdatedAt);
}
