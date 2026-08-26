namespace AdminPortal.Application.Common.Mediator;

public interface IAppRequestHandler<in TRequest, TResponse>
    where TRequest : IAppRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
