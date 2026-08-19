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

}

public sealed partial class AssessmentService(
    IApplicationDbContext dbContext,
    ICurrentActor currentActor,
    ILogger<AssessmentService> logger) : IAssessmentService
{
    private static void EnsureAssessmentRole(ActorContext actor)
    {
        if (actor.Role is not (UserRole.SuperAdmin or UserRole.Admin or UserRole.Teacher))
            throw new ForbiddenException("Không đủ quyền.");
    }
    public async Task<PagedResponse<AssessmentListItemResponse>> ListAsync(
        AssessmentListQuery query,
        CancellationToken cancellationToken)
    {
        EnsureAssessmentRole(currentActor.GetRequired());

        var assessments = QueryCurrent();
        if (query.GroupLv3Name is not null)
            assessments = assessments.Where(x => x.GroupLv3Name == query.GroupLv3Name);
        if (query.GroupLv2Name is not null)
            assessments = assessments.Where(x => x.GroupLv2Name == query.GroupLv2Name);
        if (query.GroupLv1Name is not null)
            assessments = assessments.Where(x => x.GroupLv1Name == query.GroupLv1Name);

        var descending = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var ordered = ApplySort(assessments, query.SortBy, descending);
        if (string.IsNullOrWhiteSpace(query.Search))
        {
            var totalItems = await assessments.CountAsync(cancellationToken);
            var items = await ProjectList(ordered)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
            return CreatePage(items, query, totalItems);
        }

        var startedAt = Stopwatch.GetTimestamp();
        var candidates = await ProjectList(ordered).ToListAsync(cancellationToken);
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
        VietnameseSearchNormalizer.Fold(item.GroupLv1Name).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Fold(item.GroupLv2Name).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Fold(item.GroupLv3Name).Contains(foldedSearch, StringComparison.Ordinal)
        ;

    private static PagedResponse<AssessmentListItemResponse> CreatePage(
        IReadOnlyList<AssessmentListItemResponse> items,
        AssessmentListQuery query,
        int totalItems) =>
        new(items, new PaginationMetadata(
            query.Page,
            query.PageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)query.PageSize)));

    private static IQueryable<AssessmentListItemResponse> ProjectList(IQueryable<Assessment> query) =>
        query.Select(x => new AssessmentListItemResponse(
            x.Id,
            x.Code,
            x.Name,
            x.Note,
            x.RowIndex,
            x.GroupLv1Name,
            x.GroupLv2Name,
            x.GroupLv3Name
            ));
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

