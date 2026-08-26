namespace AdminPortal.Application.Common.Mediator;

public interface IAppRequest<out TResponse>;

public interface IAppQuery<out TResponse> : IAppRequest<TResponse>;

public interface IAppCommand<out TResponse> : IAppRequest<TResponse>;

public interface IAppCommand : IAppRequest<Unit>;
