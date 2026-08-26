using AdminPortal.Application.Common.Mediator;

namespace AdminPortal.Application.Users.Commands;

public sealed record DeleteUserCommand(Guid Id) : IAppCommand;

public sealed class DeleteUserCommandHandler(IUserService userService)
    : IAppRequestHandler<DeleteUserCommand, Unit>
{
    public async Task<Unit> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        await userService.DeleteAsync(request.Id, cancellationToken);
        return Unit.Value;
    }
}
