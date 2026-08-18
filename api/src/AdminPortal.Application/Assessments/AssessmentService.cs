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

namespace AdminPortal.Application.Assessments;

public interface IAssessmentService : IQueryService<Assessment, AssessmentListQuery, AssessmentListItemResponse, AssessmentDetailResponse>
{
}

public sealed partial class AssessmentService(
    IApplicationDbContext dbContext,
    ICurrentActor currentActor,
    ILogger<AssessmentService> logger) : IAssessmentService
{
    public async Task<PagedResponse<AssessmentListItemResponse>> ListAsync(
        AssessmentListQuery query,
        CancellationToken cancellationToken)
    {
        AuthorizationRules.EnsurePortalManager(currentActor.GetRequired());

        var assessments = QueryCurrent();
        if (query.GroupLv1Id is not null)
            assessments = assessments.Where(x => x.GroupLv1Id == query.GroupLv1Id);
        if (query.GroupLv2Id is not null)
            assessments = assessments.Where(x => x.GroupLv2Id == query.GroupLv2Id);
        if (query.GroupLv3Id is not null)
            assessments = assessments.Where(x => x.GroupLv3Id == query.GroupLv3Id);

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

    public async Task<AssessmentDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        AuthorizationRules.EnsurePortalManager(currentActor.GetRequired());
        return await ProjectDetail(QueryCurrent().Where(x => x.Id == id))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw AssessmentNotFound();
    }

    private IQueryable<Assessment> QueryCurrent() => dbContext.Assessments.AsNoTracking()
        .Include(x => x.GroupLv1)
        .Include(x => x.GroupLv2)
        .Include(x => x.GroupLv3);

    private static bool Matches(
        AssessmentListItemResponse item,
        string foldedSearch,
        string searchDigits) =>
        VietnameseSearchNormalizer.Fold(item.Code).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Fold(item.Name).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Fold(item.Note).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Digits(item.Code).Contains(searchDigits, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Digits(item.Name).Contains(searchDigits, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Digits(item.Note).Contains(searchDigits, StringComparison.Ordinal);

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
            x.GroupLv1.Name,
            x.GroupLv2.Name,
            x.GroupLv3.Name))
            ;
    private static IQueryable<AssessmentDetailResponse> ProjectDetail(IQueryable<Assessment> query) =>
        query.Select(x => new AssessmentDetailResponse(
            x.Id,
            x.Code,
            x.Name,
            x.Note,
            x.RowIndex,
            x.GroupLv1.Name,
            x.GroupLv2.Name,
            x.GroupLv3.Name))
            ;

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
            _ => throw new AppValidationException(
                "Trường sắp xếp không hợp lệ.",
                new Dictionary<string, string[]>
                {
                    ["sortBy"] =
                    [
                        "Chỉ hỗ trợ code, name, rowindex."
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

