using AdminPortal.Application.Common.Interfaces;

namespace AdminPortal.Application.AssessmentResults;

public interface IResultSourcePersistence
{
    Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    Task LockCatalogSharedAsync(CancellationToken cancellationToken);
    Task LockCatalogExclusiveAsync(CancellationToken cancellationToken);
    Task LockStudentAsync(Guid studentId, CancellationToken cancellationToken);
}
