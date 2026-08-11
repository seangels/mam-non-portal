using AdminPortal.Application.Common.Models;

namespace AdminPortal.Application.Users;

public interface IUserService
{
    Task<PagedResponse<UserResponse>> ListAsync(UserListQuery query, CancellationToken cancellationToken);
    Task<UserResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);
    Task ChangePasswordAsync(Guid id, ChangePasswordRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
