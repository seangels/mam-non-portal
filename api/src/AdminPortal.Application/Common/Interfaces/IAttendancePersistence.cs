namespace AdminPortal.Application.Common.Interfaces;

public interface IAttendancePersistence
{
    Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    Task LockTeachersAsync(IEnumerable<Guid> teacherIds, CancellationToken cancellationToken);
    Task LockUsersAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);
    Task LockGroupsAsync(IEnumerable<Guid> groupIds, CancellationToken cancellationToken);
    Task LockStudentAsync(Guid studentId, CancellationToken cancellationToken);
    Task LockSheetAsync(Guid sheetId, CancellationToken cancellationToken);
}
