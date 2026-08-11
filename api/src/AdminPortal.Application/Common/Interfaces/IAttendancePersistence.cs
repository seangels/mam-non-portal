namespace AdminPortal.Application.Common.Interfaces;

public interface IAttendancePersistence
{
    Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    Task LockGroupsAsync(IEnumerable<Guid> groupIds, CancellationToken cancellationToken);
    Task LockStudentAsync(Guid studentId, CancellationToken cancellationToken);
    Task LockSheetAsync(Guid sheetId, CancellationToken cancellationToken);
}
