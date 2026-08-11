namespace AdminPortal.Application.Common.Models;

public sealed record PaginationMetadata(int Page, int PageSize, int TotalItems, int TotalPages);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, PaginationMetadata Pagination);
