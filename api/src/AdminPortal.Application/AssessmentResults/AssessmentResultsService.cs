using System.Text.Json;
using AdminPortal.Application.AssessmentSheets;
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
    IResultSourcePersistence resultSourcePersistence) : IAssessmentResultsService
{
    public async Task<AssessmentResultsResponse> GetAsync(Guid studentId, CancellationToken cancellationToken)
    {
        EnsureManager(currentActor.GetRequired());
        var student = await FindStudentAsync(studentId, cancellationToken);
        var assessments = await LoadAssessmentsAsync(cancellationToken);
        var sourceValues = await googleSheetsService.ReadAssessmentResultsFromSourceAsync(
            student.StudentCode,
            assessments.Select(ToSourceTarget).ToList(),
            cancellationToken);
        return BuildResponse(student, assessments, sourceValues);
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
        {
            throw new NotFoundException(
                "Không tìm thấy mục đánh giá.",
                ProblemCodes.AssessmentNotFound);
        }

        var normalizedUpdates = request.Items.Select(x => new AssessmentResultSourceUpdate(
            x.AssessmentId,
            x.ExpectedVersion,
            x.Grade,
            AssessmentResultRules.NormalizeNote(x.Note))).ToList();
        var requestedTargets = request.Items
            .Select(x => ToSourceTarget(assessmentById[x.AssessmentId]))
            .ToList();

        var writeResult = await googleSheetsService.UpdateAssessmentResultsInSourceAsync(
            student.StudentCode,
            requestedTargets,
            normalizedUpdates,
            cancellationToken);

        IReadOnlyList<AssessmentResultSourceValue> canonicalValues;
        try
        {
            canonicalValues = await googleSheetsService.ReadAssessmentResultsFromSourceAsync(
                student.StudentCode,
                assessments.Select(ToSourceTarget).ToList(),
                cancellationToken);
            await ReplaceLatestMirrorAsync(student, canonicalValues, actor, cancellationToken);
            AddAudits(student, writeResult.Changes, actor);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (AppException ex) when (ex.Code == ProblemCodes.AssessmentResultsPostWriteFailed)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new NormalException(
                "Google Sheet đã được cập nhật nhưng không thể xác nhận hoặc cập nhật dữ liệu gần nhất trong portal.",
                ProblemCodes.AssessmentResultsPostWriteFailed,
                new Dictionary<string, object?>
                {
                    ["googleWriteSucceeded"] = true,
                    ["failureType"] = ex.GetType().Name
                });
        }

        return BuildResponse(student, assessments, canonicalValues);
    }

    private async Task ReplaceLatestMirrorAsync(
        Student student,
        IReadOnlyList<AssessmentResultSourceValue> values,
        Common.Models.ActorContext actor,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var latest = await dbContext.AssessmentSheetLatests
            .SingleOrDefaultAsync(x => x.StudentId == student.Id, cancellationToken);
        if (latest is null)
        {
            latest = new AssessmentSheetLatest
            {
                Id = Guid.NewGuid(),
                Name = "Kết quả gần nhất",
                AssessmentSheetStatus = AssessmentSheetStatus.Open,
                StudentId = student.Id,
                Student = student,
                StudentSnapshot = BuildStudentSnapshot(student),
                UpdatedByUserId = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.AssessmentSheetLatests.Add(latest);
        }
        else
        {
            await dbContext.AssessmentRecordLatests
                .Where(x => x.AssessmentSheetLatestId == latest.Id)
                .ExecuteDeleteAsync(cancellationToken);
            latest.StudentSnapshot = BuildStudentSnapshot(student);
            latest.UpdatedByUserId = actor.UserId;
            latest.UpdatedAt = now;
        }

        var records = values
            .Where(x => x.Grade is not null || x.Note is not null)
            .Select(x => new AssessmentRecordLatest
            {
                Id = Guid.NewGuid(),
                AssessmentSheetLatestId = latest.Id,
                AssessmentSheetLatest = latest,
                AssessmentId = x.AssessmentId,
                // FK is sufficient; do not attach the AsNoTracking catalog entity as Added through the graph.
                Assessment = null!,
                LatestGrade = x.Grade,
                Note = x.Note,
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToList();
        await dbContext.AssessmentRecordLatests.AddRangeAsync(records, cancellationToken);
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

    private static AssessmentResultSourceTarget ToSourceTarget(Assessment assessment) =>
        new(assessment.Id, assessment.Code, assessment.Name);

    private static StudentSnapshot BuildStudentSnapshot(Student student) => new()
    {
        StudentCode = student.StudentCode,
        FullName = student.FullName,
        NickName = student.NickName,
        DateOfBirth = student.DateOfBirth,
        Gender = student.Gender
    };

    private static AssessmentResultsResponse BuildResponse(
        Student student,
        IReadOnlyList<Assessment> assessments,
        IReadOnlyList<AssessmentResultSourceValue> sourceValues)
    {
        var valueByAssessmentId = sourceValues.ToDictionary(x => x.AssessmentId);
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
                var value = valueByAssessmentId[assessment.Id];
                return new AssessmentResultItemResponse(
                    assessment.Id,
                    assessment.Code,
                    assessment.Name,
                    assessment.GroupLv1Name,
                    assessment.GroupLv2Name,
                    assessment.GroupLv3Name,
                    assessment.RowIndex,
                    value.Grade,
                    value.Note,
                    value.Version);
            }).ToList());
    }

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
}
