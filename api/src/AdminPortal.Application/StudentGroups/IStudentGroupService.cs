using AdminPortal.Application.Common.Models;

namespace AdminPortal.Application.StudentGroups;

public interface IStudentGroupService
{
    Task<PagedResponse<StudentGroupResponse>> ListAsync(StudentGroupListQuery query, CancellationToken cancellationToken);
    Task<StudentGroupResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<StudentGroupResponse> CreateAsync(CreateStudentGroupRequest request, CancellationToken cancellationToken);
    Task<StudentGroupResponse> UpdateAsync(Guid id, UpdateStudentGroupRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<StudentGroupResponse> AssignResponsibleTeacherAsync(
        Guid id,
        AssignResponsibleTeacherRequest request,
        CancellationToken cancellationToken);
}
