using AdminPortal.Application.Common.Mediator;

namespace AdminPortal.Application.Users.Queries;

public sealed record GetUserQuery(Guid Id) : IAppQuery<UserResponse>;

public sealed class GetUserQueryHandler(IUserService userService)
    : IAppRequestHandler<GetUserQuery, UserResponse>
{
    public Task<UserResponse> Handle(GetUserQuery request, CancellationToken cancellationToken) =>
        userService.GetAsync(request.Id, cancellationToken);
}
