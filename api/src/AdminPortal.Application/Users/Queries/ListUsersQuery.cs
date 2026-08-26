using AdminPortal.Application.Common.Mediator;
using AdminPortal.Application.Common.Models;

namespace AdminPortal.Application.Users.Queries;

public sealed record ListUsersQuery(UserListQuery Query) : IAppQuery<PagedResponse<UserResponse>>;

public sealed class ListUsersQueryHandler(IUserService userService)
    : IAppRequestHandler<ListUsersQuery, PagedResponse<UserResponse>>
{
    public Task<PagedResponse<UserResponse>> Handle(ListUsersQuery request, CancellationToken cancellationToken) =>
        userService.ListAsync(request.Query, cancellationToken);
}
