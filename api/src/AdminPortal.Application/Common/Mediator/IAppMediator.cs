namespace AdminPortal.Application.Common.Mediator;

public interface IAppMediator
{
    Task<TResponse> Send<TResponse>(
        IAppRequest<TResponse> request,
        CancellationToken cancellationToken);
}
