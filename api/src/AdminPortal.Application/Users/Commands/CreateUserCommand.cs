using AdminPortal.Application.Common.Mediator;

namespace AdminPortal.Application.Users.Commands;

public sealed record CreateUserCommand(CreateUserRequest Request) : IAppCommand<UserResponse>;

public sealed class CreateUserCommandHandler(IUserService userService)
    : IAppRequestHandler<CreateUserCommand, UserResponse>
{
    public Task<UserResponse> Handle(CreateUserCommand request, CancellationToken cancellationToken) =>
        userService.CreateAsync(request.Request, cancellationToken);
}
