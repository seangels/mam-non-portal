using AdminPortal.Application.Common.Models;

namespace AdminPortal.Application.Teachers;

public interface ITeacherService
{
    Task<PagedResponse<TeacherListItemResponse>> ListAsync(TeacherListQuery query, CancellationToken cancellationToken);
    Task<TeacherDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<TeacherDetailResponse> CreateAsync(CreateTeacherRequest request, CancellationToken cancellationToken);
    Task<TeacherDetailResponse> UpdateAsync(Guid id, UpdateTeacherRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, int expectedVersion, CancellationToken cancellationToken);
    Task<TeacherDetailResponse> UpdateAttendancePolicyAsync(
        Guid id,
        UpdateAttendancePolicyRequest request,
        CancellationToken cancellationToken);
}
