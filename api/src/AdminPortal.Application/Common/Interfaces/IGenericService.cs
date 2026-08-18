using AdminPortal.Application.Common.Models;

namespace AdminPortal.Application.Common.Interfaces;


public interface IQueryService<TEntity, TListQuery, TListItemResponse, TDetailResponse>
    where TEntity : class
    where TListQuery : class
    where TListItemResponse : class
    where TDetailResponse : class
{
    Task<PagedResponse<TListItemResponse>> ListAsync(TListQuery query, CancellationToken cancellationToken);
    Task<TDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken);
}

public interface IGenericService<TEntity, TCreateRequest, TUpdateRequest, TListQuery, TListItemResponse, TDetailResponse> : IQueryService<TEntity, TListQuery, TListItemResponse, TDetailResponse>
    where TEntity : class
    where TCreateRequest : class
    where TUpdateRequest : class
    where TListQuery : class
    where TListItemResponse : class
    where TDetailResponse : class
{
    Task<TDetailResponse> CreateAsync(TCreateRequest request, CancellationToken cancellationToken);
    Task<TDetailResponse> UpdateAsync(Guid id, TUpdateRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, int expectedVersion, CancellationToken cancellationToken);
}
