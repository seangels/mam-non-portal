using System.Text.Json;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AdminPortal.Application.Users;

public sealed class UserService(
    IApplicationDbContext dbContext,
    UserAccountCoordinator userAccountCoordinator,
    ICurrentActor currentActor,
    IDatabaseExceptionClassifier databaseExceptionClassifier,
    TimeProvider timeProvider) : IUserService
{
    private const string UserEmailIndex = "ix_users_normalized_email";

    public async Task<PagedResponse<UserResponse>> ListAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsureSuperAdmin(actor);
        var users = dbContext.Users.AsNoTracking().Where(user => user.Role == UserRole.Admin);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862
            users = users.Where(user =>
                user.Email.ToLower().Contains(search) ||
                user.FullName.ToLower().Contains(search) ||
                (user.PhoneNumber != null && user.PhoneNumber.Contains(search)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        if (query.Status is not null) users = users.Where(user => user.Status == query.Status);
        if (query.Role is not null) users = users.Where(user => user.Role == query.Role);
        if (query.CreatedFrom is not null)
        {
            var from = ToUtcStart(query.CreatedFrom.Value);
            users = users.Where(user => user.CreatedAt >= from);
        }

        if (query.CreatedTo is not null)
        {
            var toExclusive = ToUtcStart(query.CreatedTo.Value.AddDays(1));
            users = users.Where(user => user.CreatedAt < toExclusive);
        }

        var totalItems = await users.CountAsync(cancellationToken);
        var descending = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var items = await ApplySort(users, query.SortBy, descending)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(user => Map(user))
            .ToListAsync(cancellationToken);
        return new PagedResponse<UserResponse>(items,
            new PaginationMetadata(
                query.Page,
                query.PageSize,
                totalItems,
                (int)Math.Ceiling(totalItems / (double)query.PageSize)));
    }

    public async Task<UserResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        var user = await FindRequiredAsync(id, false, cancellationToken);
        EnsureAdminMutation(user.Role);
        AuthorizationRules.EnsureCanManageUser(actor, user.Role);
        return Map(user);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        EnsureAdminMutation(request.Role);
        AuthorizationRules.EnsureCanManageUser(actor, request.Role);
        var now = timeProvider.GetUtcNow();
        var user = await userAccountCoordinator.CreateAsync(
            request.Email,
            request.FullName,
            request.PhoneNumber,
            UserRole.Admin,
            request.Status,
            request.Password,
            now,
            cancellationToken);
        AddAudit(actor, "User.Created", user.Id, null, Snapshot(user));
        await SaveUserChangesAsync(cancellationToken);
        return Map(user);
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        var user = await FindRequiredAsync(id, true, cancellationToken);
        EnsureAdminMutation(user.Role);
        EnsureAdminMutation(request.Role);
        AuthorizationRules.EnsureCanManageUser(actor, user.Role);
        if (actor.UserId == user.Id)
        {
            throw new ForbiddenException("Không thể tự thay đổi tài khoản quản trị hiện tại.");
        }

        var oldValues = Snapshot(user);
        var now = timeProvider.GetUtcNow();
        _ = await userAccountCoordinator.UpdateAsync(
            user,
            request.Email,
            request.FullName,
            request.PhoneNumber,
            request.Status,
            now,
            cancellationToken);
        AddAudit(actor, "User.Updated", user.Id, oldValues, Snapshot(user));
        await SaveUserChangesAsync(cancellationToken);
        return Map(user);
    }

    public async Task ChangePasswordAsync(Guid id, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        var user = await FindRequiredAsync(id, true, cancellationToken);
        AuthorizationRules.EnsureCanManageUser(actor, user.Role);
        var now = timeProvider.GetUtcNow();
        await userAccountCoordinator.ChangePasswordAsync(user, request.Password, now, cancellationToken);
        AddAudit(actor, "User.PasswordChanged", user.Id, null, null);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        var user = await FindRequiredAsync(id, true, cancellationToken);
        EnsureAdminMutation(user.Role);
        AuthorizationRules.EnsureCanManageUser(actor, user.Role);
        if (actor.UserId == user.Id)
        {
            throw new ForbiddenException("Không thể tự xóa tài khoản hiện tại.");
        }

        var oldValues = Snapshot(user);
        var now = timeProvider.GetUtcNow();
        await userAccountCoordinator.SoftDeleteAsync(user, now, cancellationToken);
        AddAudit(actor, "User.Deleted", user.Id, oldValues, null);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> FindRequiredAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? dbContext.Users.AsQueryable() : dbContext.Users.AsNoTracking();
        return await query.SingleOrDefaultAsync(user => user.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản.");
    }

    private async Task SaveUserChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsUniqueViolation(exception, UserEmailIndex))
        {
            throw new ConflictException("Email đã được sử dụng.", ProblemCodes.EmailAlreadyExists);
        }
    }

    private static void EnsureAdminMutation(UserRole role)
    {
        if (role == UserRole.Teacher)
        {
            throw new ConflictException(
                "Giáo viên phải được quản lý qua API giáo viên.",
                ProblemCodes.TeacherMustBeManagedViaTeachers);
        }

        if (role != UserRole.Admin)
        {
            throw new ForbiddenException("Chỉ có thể quản lý tài khoản quản trị viên qua API này.");
        }
    }

    private void AddAudit(
        ActorContext actor,
        string action,
        Guid entityId,
        object? oldValues,
        object? newValues) =>
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor.UserId,
            Action = action,
            EntityType = "User",
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = actor.IpAddress,
            CreatedAt = timeProvider.GetUtcNow()
        });

    private static object Snapshot(User user) => new
    {
        user.Email,
        user.FullName,
        user.PhoneNumber,
        Role = user.Role.ToString(),
        Status = user.Status.ToString(),
        user.DeletedAt
    };

    private static IOrderedQueryable<User> ApplySort(
        IQueryable<User> query,
        string sortBy,
        bool descending) => (sortBy.ToLowerInvariant(), descending) switch
        {
            ("email", false) => query.OrderBy(user => user.Email).ThenBy(user => user.Id),
            ("email", true) => query.OrderByDescending(user => user.Email).ThenByDescending(user => user.Id),
            ("fullname", false) => query.OrderBy(user => user.FullName).ThenBy(user => user.Id),
            ("fullname", true) => query.OrderByDescending(user => user.FullName).ThenByDescending(user => user.Id),
            ("role", false) => query.OrderBy(user => user.Role).ThenBy(user => user.Id),
            ("role", true) => query.OrderByDescending(user => user.Role).ThenByDescending(user => user.Id),
            ("status", false) => query.OrderBy(user => user.Status).ThenBy(user => user.Id),
            ("status", true) => query.OrderByDescending(user => user.Status).ThenByDescending(user => user.Id),
            ("createdat", false) => query.OrderBy(user => user.CreatedAt).ThenBy(user => user.Id),
            ("createdat", true) => query.OrderByDescending(user => user.CreatedAt).ThenByDescending(user => user.Id),
            _ => throw new AppValidationException(
                "Trường sắp xếp không hợp lệ.",
                new Dictionary<string, string[]>
                {
                    ["sortBy"] = ["Chỉ hỗ trợ email, fullName, role, status hoặc createdAt."]
                })
        };

    private static UserResponse Map(User user) =>
        new(user.Id, user.Email, user.FullName, user.PhoneNumber, user.Role, user.Status, user.CreatedAt, user.UpdatedAt);

    private static DateTimeOffset ToUtcStart(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
}
