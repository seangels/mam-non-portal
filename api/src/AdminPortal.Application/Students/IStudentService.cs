using AdminPortal.Application.Common.Models;

namespace AdminPortal.Application.Students;

public interface IStudentService
{
    Task<PagedResponse<StudentResponse>> ListAsync(StudentListQuery query, CancellationToken cancellationToken);
    Task<StudentResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<StudentResponse> CreateAsync(CreateStudentRequest request, CancellationToken cancellationToken);
    Task<StudentResponse> UpdateAsync(Guid id, UpdateStudentRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
