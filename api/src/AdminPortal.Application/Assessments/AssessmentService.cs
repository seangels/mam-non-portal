using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.GoogleSheets;
using AdminPortal.Application.Users;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AdminPortal.Application.Assessments;

public interface IAssessmentService : IQueryService<Assessment, AssessmentListQuery, AssessmentListItemResponse, AssessmentDetailResponse>
{
    Task<UpdateAssessmentGroupResponse> UpdateGroupAsync(
        UpdateAssessmentGroupRequest request,
        CancellationToken cancellationToken);
}

public sealed partial class AssessmentService(
    IApplicationDbContext dbContext,
    ICurrentActor currentActor,
    TimeProvider timeProvider,
    ILogger<AssessmentService> logger) : IAssessmentService
{
    private static void EnsureAssessmentRole(ActorContext actor)
    {
        if (actor.Role is not (UserRole.SuperAdmin or UserRole.Admin or UserRole.Teacher))
            throw new ForbiddenException("Không đủ quyền.");
    }

    public async Task<UpdateAssessmentGroupResponse> UpdateGroupAsync(
        UpdateAssessmentGroupRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        if (actor.Role is not (UserRole.SuperAdmin or UserRole.Admin))
            throw new ForbiddenException("Chỉ quản trị viên được cập nhật nhóm mục đánh giá gốc.");
        if (request.Level is not (2 or 3))
        {
            throw new AppValidationException("Cấp nhóm không hợp lệ.", new Dictionary<string, string[]>
            {
                ["level"] = ["Chỉ hỗ trợ cấp nhóm 2 hoặc 3."]
            });
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new AppValidationException("Tên nhóm không hợp lệ.", new Dictionary<string, string[]>
            {
                ["name"] = ["Tên nhóm không được để trống."]
            });
        }

        var codes = request.AssessmentCodes
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (codes.Length == 0)
        {
            throw new AppValidationException("Danh sách mục đánh giá không hợp lệ.", new Dictionary<string, string[]>
            {
                ["assessmentCodes"] = ["Cần ít nhất một mã mục đánh giá hợp lệ."]
            });
        }

        var assessments = await dbContext.Assessments
            .Where(x => codes.Contains(x.Code))
            .ToListAsync(cancellationToken);
        var byCode = assessments.GroupBy(x => x.Code, StringComparer.Ordinal).ToArray();
        if (byCode.Any(x => x.Count() != 1))
            throw new ConflictException("Mã mục đánh giá không duy nhất, không thể cập nhật nhóm gốc.", ProblemCodes.SnapshotChanged);
        if (byCode.Length != codes.Length)
            throw new NotFoundException("Không tìm thấy đủ mục đánh giá theo mã.", ProblemCodes.AssessmentNotFound);

        var now = timeProvider.GetUtcNow();
        var oldGroups = assessments
            .Select(x => new { x.Code, Name = request.Level == 2 ? x.GroupLv2Name : x.GroupLv3Name })
            .ToArray();
        foreach (var assessment in assessments)
        {
            if (request.Level == 2)
                assessment.GroupLv2Name = name;
            else
                assessment.GroupLv3Name = name;
            assessment.UpdatedByUserId = actor.UserId;
            assessment.UpdatedAt = now;
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor.UserId,
            Action = "Assessment.GroupUpdated",
            EntityType = "Assessment",
            EntityId = Guid.Empty,
            OldValues = JsonSerializer.Serialize(new { request.Level, Groups = oldGroups }),
            NewValues = JsonSerializer.Serialize(new
            {
                request.Level,
                GroupName = name,
                AssessmentCount = assessments.Count,
                AssessmentCodes = codes
            }),
            IpAddress = actor.IpAddress,
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new UpdateAssessmentGroupResponse(assessments.Count);
    }
    public async Task<PagedResponse<AssessmentListItemResponse>> ListAsync(
        AssessmentListQuery query,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        EnsureAssessmentRole(actor);
        if (query.StudentId is Guid studentId)
            await EnsureStudentLatestScopeAsync(actor, studentId, cancellationToken);

        var assessments = QueryCurrent();
        if (query.GroupLv3Name is not null)
            assessments = assessments.Where(x => x.GroupLv3Name == query.GroupLv3Name);
        if (query.GroupLv2Name is not null)
            assessments = assessments.Where(x => x.GroupLv2Name == query.GroupLv2Name);
        if (query.GroupLv1Name is not null)
            assessments = assessments.Where(x => x.GroupLv1Name == query.GroupLv1Name);

        var descending = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var ordered = ApplySort(assessments, query.SortBy, descending);
        var projected = ProjectList(ordered, query.StudentId);
        if (string.IsNullOrWhiteSpace(query.Search))
        {
            var totalItems = await assessments.CountAsync(cancellationToken);
            var items = await projected
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
            return CreatePage(items, query, totalItems);
        }

        var startedAt = Stopwatch.GetTimestamp();
        var candidates = await projected.ToListAsync(cancellationToken);
        var foldedSearch = VietnameseSearchNormalizer.Fold(query.Search);
        var matches = candidates.Where(candidate => Matches(candidate, foldedSearch)).ToList();
        var pageItems = matches
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();
        if (logger.IsEnabled(LogLevel.Information))
        {
            var durationMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            LogAccentSearch(
                logger,
                candidates.Count,
                matches.Count,
                durationMs);
        }
        return CreatePage(pageItems, query, matches.Count);
    }

    public async Task<AssessmentDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureAssessmentRole(currentActor.GetRequired());
        return await ProjectDetail(QueryCurrent().Where(x => x.Id == id))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw AssessmentNotFound();
    }

    private IQueryable<Assessment> QueryCurrent() => dbContext.Assessments.AsNoTracking()
        ;

    private static bool Matches(
        AssessmentListItemResponse item,
        string foldedSearch) =>
        VietnameseSearchNormalizer.Fold(item.Code).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Fold(item.Name).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Fold(item.Note).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Fold(item.LatestNote).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Fold(item.GroupLv1Name).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Fold(item.GroupLv2Name).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Fold(item.GroupLv3Name).Contains(foldedSearch, StringComparison.Ordinal)
        ;

    private async Task EnsureStudentLatestScopeAsync(
        ActorContext actor,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var students = dbContext.Students.AsNoTracking()
            .Where(student => student.Id == studentId);

        if (actor.Role == UserRole.Teacher)
        {
            students = students.Where(student =>
                student.Group != null &&
                student.Group.ResponsibleTeacher != null &&
                student.Group.ResponsibleTeacher.UserId == actor.UserId);
        }

        if (!await students.AnyAsync(cancellationToken))
            throw new NotFoundException("Không tìm thấy học sinh.", ProblemCodes.StudentNotFound);
    }

    private static PagedResponse<AssessmentListItemResponse> CreatePage(
        IReadOnlyList<AssessmentListItemResponse> items,
        AssessmentListQuery query,
        int totalItems) =>
        new(items, new PaginationMetadata(
            query.Page,
            query.PageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)query.PageSize)));

    private IQueryable<AssessmentListItemResponse> ProjectList(IQueryable<Assessment> query, Guid? studentId)
    {
        if (studentId is null)
        {
            return query.Select(x => new AssessmentListItemResponse(
                x.Id,
                x.Code,
                x.Name,
                x.Note,
                x.RowIndex,
                x.GroupLv1Name,
                x.GroupLv2Name,
                x.GroupLv3Name,
                null,
                null
                ));
        }

        var latestRecords =
            from latestSheet in dbContext.AssessmentSheetLatests.AsNoTracking()
            where latestSheet.StudentId == studentId.Value
            join latestRecord in dbContext.AssessmentRecordLatests.AsNoTracking()
                on latestSheet.Id equals latestRecord.AssessmentSheetLatestId
            select new
            {
                latestRecord.AssessmentId,
                latestRecord.LatestGrade,
                LatestNote = latestRecord.Note
            };

        return
            from assessment in query
            join latestRecord in latestRecords
                on assessment.Id equals latestRecord.AssessmentId into latestRecordGroup
            from latestRecord in latestRecordGroup.DefaultIfEmpty()
            select new AssessmentListItemResponse(
                assessment.Id,
                assessment.Code,
                assessment.Name,
                assessment.Note,
                assessment.RowIndex,
                assessment.GroupLv1Name,
                assessment.GroupLv2Name,
                assessment.GroupLv3Name,
                latestRecord == null ? null : latestRecord.LatestGrade,
                latestRecord == null ? null : latestRecord.LatestNote
                );
    }

    private static IQueryable<AssessmentDetailResponse> ProjectDetail(IQueryable<Assessment> query) =>
        query.Select(x => new AssessmentDetailResponse(
            x.Id,
            x.Code,
            x.Name,
            x.Note,
            x.RowIndex,
            x.GroupLv1Name,
            x.GroupLv2Name,
            x.GroupLv3Name
            ));

    private static IOrderedQueryable<Assessment> ApplySort(
        IQueryable<Assessment> query,
        string sortBy,
        bool descending) => (sortBy.ToLowerInvariant(), descending) switch
        {
            ("code", false) => query.OrderBy(x => x.Code).ThenBy(x => x.Id),
            ("code", true) => query.OrderByDescending(x => x.Code).ThenByDescending(x => x.Id),
            ("name", false) => query.OrderBy(x => x.Name).ThenBy(x => x.Id),
            ("name", true) => query.OrderByDescending(x => x.Name).ThenByDescending(x => x.Id),
            ("rowindex", false) => query.OrderBy(x => x.RowIndex).ThenBy(x => x.Id),
            ("rowindex", true) => query.OrderByDescending(x => x.RowIndex).ThenByDescending(x => x.Id),
            ("grouplv1name", false) => query.OrderBy(x => x.GroupLv1Name).ThenBy(x => x.Id),
            ("grouplv1name", true) => query.OrderByDescending(x => x.GroupLv1Name).ThenByDescending(x => x.Id),
            ("grouplv2name", false) => query.OrderBy(x => x.GroupLv2Name).ThenBy(x => x.Id),
            ("grouplv2name", true) => query.OrderByDescending(x => x.GroupLv2Name).ThenByDescending(x => x.Id),
            ("grouplv3name", false) => query.OrderBy(x => x.GroupLv3Name).ThenBy(x => x.Id),
            ("grouplv3name", true) => query.OrderByDescending(x => x.GroupLv3Name).ThenByDescending(x => x.Id),
            _ => throw new AppValidationException(
                "Trường sắp xếp không hợp lệ.",
                new Dictionary<string, string[]>
                {
                    ["sortBy"] =
                    [
                        "Chỉ hỗ trợ code, name, rowindex, grouplv1name, grouplv2name, grouplv3name."
                    ]
                })
        };

    private static NotFoundException AssessmentNotFound() =>
        new("Không tìm thấy đánh giá.", ProblemCodes.AssessmentNotFound);

    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Information,
        Message = "Assessment accent search evaluated {CandidateCount} candidates, matched {MatchCount}, duration {DurationMs} ms")]
    private static partial void LogAccentSearch(
        ILogger logger,
        int candidateCount,
        int matchCount,
        double durationMs);


}

