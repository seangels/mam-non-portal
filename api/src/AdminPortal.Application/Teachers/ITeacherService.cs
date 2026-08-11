using AdminPortal.Application.Common.Models;

namespace AdminPortal.Application.Teachers;

public interface ITeacherService
{
    Task<PagedResponse<TeacherResponse>> ListAsync(TeacherListQuery query, CancellationToken cancellationToken);
    Task<TeacherResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<TeacherResponse> UpdateAttendancePolicyAsync(
        Guid id,
        UpdateAttendancePolicyRequest request,
        CancellationToken cancellationToken);
}
