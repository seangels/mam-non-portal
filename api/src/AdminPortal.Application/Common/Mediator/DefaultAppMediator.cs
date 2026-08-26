namespace AdminPortal.Application.Common.Mediator;

public sealed class DefaultAppMediator(IServiceProvider serviceProvider) : IAppMediator
{
    public async Task<TResponse> Send<TResponse>(
        IAppRequest<TResponse> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var handlerType = typeof(IAppRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
        var handler = serviceProvider.GetService(handlerType)
            ?? throw new InvalidOperationException($"No handler is registered for request '{request.GetType().Name}'.");

        var handleMethod = handlerType.GetMethod(nameof(IAppRequestHandler<IAppRequest<TResponse>, TResponse>.Handle))
            ?? throw new InvalidOperationException($"Handler '{handler.GetType().Name}' does not expose a Handle method.");

        var responseTask = (Task<TResponse>?)handleMethod.Invoke(handler, [request, cancellationToken])
            ?? throw new InvalidOperationException($"Handler '{handler.GetType().Name}' returned no response task.");

        return await responseTask.ConfigureAwait(false);
    }
}
