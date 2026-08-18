using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.Users;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AdminPortal.Application.AssessmentGroups;

public interface IAssessmentGroupService : IQueryService<AssessmentGroup, AssessmentGroupListQuery, AssessmentGroupListItemResponse, AssessmentGroupDetailResponse>
{
}

public sealed partial class AssessmentGroupService(
    IApplicationDbContext dbContext,
    ICurrentActor currentActor,
    ILogger<AssessmentGroupService> logger) : IAssessmentGroupService
{
    public async Task<PagedResponse<AssessmentGroupListItemResponse>> ListAsync(
        AssessmentGroupListQuery query,
        CancellationToken cancellationToken)
    {
        AuthorizationRules.EnsurePortalManager(currentActor.GetRequired());

        var assessmentGroups = QueryCurrent();
        if (query.Level is not null)
            assessmentGroups = assessmentGroups.Where(x => x.Level == query.Level);
        if (query.ParentId is not null)
            assessmentGroups = assessmentGroups.Where(x => x.ParentId == query.ParentId);

        var descending = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var ordered = ApplySort(assessmentGroups, query.SortBy, descending);
        if (string.IsNullOrWhiteSpace(query.Search))
        {
            var totalItems = await assessmentGroups.CountAsync(cancellationToken);
            var items = await ProjectList(ordered)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
            return CreatePage(items, query, totalItems);
        }

        var startedAt = Stopwatch.GetTimestamp();
        var candidates = await ProjectList(ordered).ToListAsync(cancellationToken);
        var foldedSearch = VietnameseSearchNormalizer.Fold(query.Search);
        var searchDigits = VietnameseSearchNormalizer.Digits(query.Search);
        var matches = candidates.Where(candidate => Matches(candidate, foldedSearch, searchDigits)).ToList();
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

    public async Task<AssessmentGroupDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        AuthorizationRules.EnsurePortalManager(currentActor.GetRequired());
        return await ProjectDetail(QueryCurrent().Where(x => x.Id == id))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw AssessmentGroupNotFound();
    }

    private IQueryable<AssessmentGroup> QueryCurrent() => dbContext.AssessmentGroups.AsNoTracking();

    private static bool Matches(
        AssessmentGroupListItemResponse item,
        string foldedSearch,
        string searchDigits) =>
        VietnameseSearchNormalizer.Fold(item.Name).Contains(foldedSearch, StringComparison.Ordinal)
        || VietnameseSearchNormalizer.Digits(item.Name).Contains(searchDigits, StringComparison.Ordinal)
        ;

    private static PagedResponse<AssessmentGroupListItemResponse> CreatePage(
        IReadOnlyList<AssessmentGroupListItemResponse> items,
        AssessmentGroupListQuery query,
        int totalItems) =>
        new(items, new PaginationMetadata(
            query.Page,
            query.PageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)query.PageSize)));

    private static IQueryable<AssessmentGroupListItemResponse> ProjectList(IQueryable<AssessmentGroup> query) =>
        query.Select(x => new AssessmentGroupListItemResponse(
            x.Id,
            x.Name,
            x.Level,
            x.DisplayOrder))
            ;
    private static IQueryable<AssessmentGroupDetailResponse> ProjectDetail(IQueryable<AssessmentGroup> query) =>
        query.Select(x => new AssessmentGroupDetailResponse(
            x.Id,
            x.Name,
            x.Level,
            x.DisplayOrder,
            x.ParentId))
            ;

    private static IOrderedQueryable<AssessmentGroup> ApplySort(
        IQueryable<AssessmentGroup> query,
        string sortBy,
        bool descending) => (sortBy.ToLowerInvariant(), descending) switch
        {
            ("name", false) => query.OrderBy(x => x.Name).ThenBy(x => x.Id),
            ("name", true) => query.OrderByDescending(x => x.Name).ThenByDescending(x => x.Id),
            ("level", false) => query.OrderBy(x => x.Level).ThenBy(x => x.Id),
            ("level", true) => query.OrderByDescending(x => x.Level).ThenByDescending(x => x.Id),
            ("displayorder", false) => query.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id),
            ("displayorder", true) => query.OrderByDescending(x => x.DisplayOrder).ThenByDescending(x => x.Id),
            _ => throw new AppValidationException(
                "Trường sắp xếp không hợp lệ.",
                new Dictionary<string, string[]>
                {
                    ["sortBy"] =
                    [
                        "Chỉ hỗ trợ name, level, displayorder."
                    ]
                })
        };

    private static NotFoundException AssessmentGroupNotFound() =>
        new("Không tìm thấy nhóm đánh giá.", ProblemCodes.AssessmentGroupNotFound);

    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Information,
        Message = "Assessment group accent search evaluated {CandidateCount} candidates, matched {MatchCount}, duration {DurationMs} ms")]
    private static partial void LogAccentSearch(
        ILogger logger,
        int candidateCount,
        int matchCount,
        double durationMs);
}

