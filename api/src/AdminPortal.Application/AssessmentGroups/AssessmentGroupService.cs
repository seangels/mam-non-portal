using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
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

public interface IAssessmentGroupService
{
    Task<PagedResponse<AssessmentGroupListItemResponse>> ListAsync(AssessmentGroupListQuery query, CancellationToken cancellationToken);
    Task<AssessmentGroupListItemResponse> GetAsync(string Name, CancellationToken cancellationToken);
}

public sealed partial class AssessmentGroupService(
    IApplicationDbContext dbContext,
    ICurrentActor currentActor,
    ILogger<AssessmentGroupService> logger) : IAssessmentGroupService
{
    public async Task<AssessmentGroupListItemResponse> GetAsync(string Name, CancellationToken cancellationToken)
    {
        var all = await ListAsync(new AssessmentGroupListQuery
        {
            PageSize = 1000,
        }, cancellationToken);
        return all.Items.SingleOrDefault(x => x.Name == Name) ?? throw AssessmentGroupNotFound();
    }
    public async Task<PagedResponse<AssessmentGroupListItemResponse>> ListAsync(
        AssessmentGroupListQuery query,
        CancellationToken cancellationToken)
    {
        AuthorizationRules.EnsurePortalManager(currentActor.GetRequired());

        var assessmentGroupsQuery = dbContext.Assessments.AsNoTracking()
            .GroupBy(x => new { x.GroupLv1Name, x.GroupLv2Name, x.GroupLv3Name })
            .Select(x => new
            {
                x.Key.GroupLv1Name,
                x.Key.GroupLv2Name,
                x.Key.GroupLv3Name,
                DisplayOrder = x.Max(y => y.RowIndex),
                Root = 0,
            }).ToList()
            ;
        var assessmentGroupsTree = assessmentGroupsQuery
            .GroupBy(x => x.Root)
            .Select(x => new
            {
                ChildGroups = x
                    .GroupBy(x1 => x1.GroupLv1Name)
                    .Select(x1 => new
                    {
                        Name = x1.Key!,
                        Level = 1,
                        DisplayOrder = x1.Max(y => y.DisplayOrder),
                        ChildGroups = x1.GroupBy(x2 => x2.GroupLv2Name)
                            .Select(x2 => new
                            {
                                Name = x2.Key!,
                                Level = 2,
                                DisplayOrder = x2.Max(y => y.DisplayOrder),
                                ParentName = x1.Key!,
                                ChildGroups = x2
                                    .GroupBy(x3 => x3.GroupLv3Name)
                                    .Select(x3 => new
                                    {
                                        Name = x3.Key!,
                                        Level = 3,
                                        DisplayOrder = x3.Max(z => z.DisplayOrder),
                                        ParentName = x2.Key!,
                                    }).ToList()
                            }).ToList()
                    })
                    .ToList()
            }).ToList()
            ;
        var assessmentGroupsAll = new List<AssessmentGroupListItemResponse>();
        foreach (var x1 in assessmentGroupsTree.First().ChildGroups)
        {
            var itemLv1 = new AssessmentGroupListItemResponse
            {
                Name = x1.Name,
                Level = 1,
                ParentName = null,
                DisplayOrder = x1.DisplayOrder,
            };
            assessmentGroupsAll.Add(itemLv1);
            foreach (var x2 in x1.ChildGroups)
            {
                var itemLv2 = new AssessmentGroupListItemResponse
                {
                    Name = x2.Name,
                    Level = 2,
                    ParentName = x1.Name,
                    DisplayOrder = x2.DisplayOrder,
                };
                assessmentGroupsAll.Add(itemLv2);
                foreach (var x3 in x2.ChildGroups)
                {
                    var itemLv3 = new AssessmentGroupListItemResponse
                    {
                        Name = x3.Name,
                        Level = 3,
                        ParentName = x2.Name,
                        DisplayOrder = x3.DisplayOrder,
                    };
                    assessmentGroupsAll.Add(itemLv3);
                }
            }
        }
        var assessmentGroupsLv1 = assessmentGroupsAll.Where(x => x.Level == 1).ToList();
        var assessmentGroupsLv2 = assessmentGroupsAll.Where(x => x.Level == 2).ToList();
        var assessmentGroupsLv3 = assessmentGroupsAll.Where(x => x.Level == 3).ToList();
        var assessmentGroups = assessmentGroupsAll;
        if (query.Level is not null)
            assessmentGroups = assessmentGroups.Where(x => x.Level == query.Level).ToList();
        if (query.ParentName is not null)
            assessmentGroups = assessmentGroups.Where(x => x.ParentName == query.ParentName).ToList();
        if (query.ParentParentName is not null && query.Level == 3)
        {
            assessmentGroups = assessmentGroupsTree.First().ChildGroups
                .Where(x => x.Name == query.ParentParentName)
                .SelectMany(x=>x.ChildGroups)
                .Where(x=>string.IsNullOrWhiteSpace(query.ParentName) || x.Name == query.ParentName)
                .SelectMany(x=>x.ChildGroups)
                .Select(x=> new AssessmentGroupListItemResponse
                {
                    Name = x.Name,
                    Level = x.Level,
                    ParentName = x.ParentName,
                    DisplayOrder = x.DisplayOrder,
                })
                .ToList();
        }

        assessmentGroups = assessmentGroups.GroupBy(x => x.Name).Select(x => x.First()).ToList();

        var descending = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var ordered = ApplySort(assessmentGroups, query.SortBy, descending);
        if (string.IsNullOrWhiteSpace(query.Search))
        {
            var totalItems = assessmentGroups.Count;
            var items = ordered
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();
            return CreatePage(items, query, totalItems);
        }

        var startedAt = Stopwatch.GetTimestamp();
        var candidates = ordered.ToList();
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


    private static IOrderedEnumerable<AssessmentGroupListItemResponse> ApplySort(
        ICollection<AssessmentGroupListItemResponse> query,
        string sortBy,
        bool descending) => (sortBy.ToLowerInvariant(), descending) switch
        {
            ("name", false) => query.OrderBy(x => x.Name),
            ("name", true) => query.OrderByDescending(x => x.Name),
            ("level", false) => query.OrderBy(x => x.Level),
            ("level", true) => query.OrderByDescending(x => x.Level),
            ("displayorder", false) => query.OrderBy(x => x.DisplayOrder),
            ("displayorder", true) => query.OrderByDescending(x => x.DisplayOrder),
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

