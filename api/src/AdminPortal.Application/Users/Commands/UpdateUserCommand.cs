using AdminPortal.Application.Common.Mediator;

namespace AdminPortal.Application.Users.Commands;

public sealed record UpdateUserCommand(Guid Id, UpdateUserRequest Request) : IAppCommand<UserResponse>;

public sealed class UpdateUserCommandHandler(IUserService userService)
    : IAppRequestHandler<UpdateUserCommand, UserResponse>
{
    public Task<UserResponse> Handle(UpdateUserCommand request, CancellationToken cancellationToken) =>
        userService.UpdateAsync(request.Id, request.Request, cancellationToken);
}
