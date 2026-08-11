namespace AdminPortal.Application.Common.Interfaces;

public interface IAppTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
