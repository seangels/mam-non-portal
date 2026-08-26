using AdminPortal.Application.Common.Mediator;

namespace AdminPortal.Application.Users.Commands;

public sealed record ChangeUserPasswordCommand(Guid Id, ChangePasswordRequest Request) : IAppCommand;

public sealed class ChangeUserPasswordCommandHandler(IUserService userService)
    : IAppRequestHandler<ChangeUserPasswordCommand, Unit>
{
    public async Task<Unit> Handle(ChangeUserPasswordCommand request, CancellationToken cancellationToken)
    {
        await userService.ChangePasswordAsync(request.Id, request.Request, cancellationToken);
        return Unit.Value;
    }
}
