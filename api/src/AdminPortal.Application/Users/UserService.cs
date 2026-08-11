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
    IPasswordService passwordService,
    ICurrentActor currentActor,
    TimeProvider timeProvider) : IUserService
{
    public async Task<PagedResponse<UserResponse>> ListAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        if (query.Role is not null)
        {
            AuthorizationRules.EnsureCanManageUser(actor, query.Role.Value);
        }

        var users = dbContext.Users.AsNoTracking().Where(user => user.Role != UserRole.SuperAdmin);
        if (actor.Role == UserRole.Admin)
        {
            users = users.Where(user => user.Role == UserRole.Teacher);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // Parameterless ToLower is translated to PostgreSQL lower().
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
        var ordered = ApplySort(users, query.SortBy, descending);
        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(user => Map(user))
            .ToListAsync(cancellationToken);

        return new PagedResponse<UserResponse>(
            items,
            new PaginationMetadata(query.Page, query.PageSize, totalItems, (int)Math.Ceiling(totalItems / (double)query.PageSize)));
    }

    public async Task<UserResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        var user = await FindRequiredAsync(id, false, cancellationToken);
        AuthorizationRules.EnsureCanManageUser(actor, user.Role);
        return Map(user);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsureCanManageUser(actor, request.Role);
        PasswordPolicy.Validate(request.Password);
        EnsureText(request.FullName, "fullName", "Họ tên là bắt buộc.");

        var normalizedEmail = EmailNormalizer.Normalize(request.Email);
        if (await dbContext.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            throw new ConflictException("Email đã được sử dụng.");
        }

        var now = timeProvider.GetUtcNow();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = string.Empty,
            FullName = request.FullName.Trim(),
            PhoneNumber = NormalizeOptional(request.PhoneNumber),
            Role = request.Role,
            Status = request.Status,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordService.Hash(user, request.Password);
        dbContext.Users.Add(user);
        AddAudit(actor, "User.Created", user.Id, null, Snapshot(user));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        var user = await FindRequiredAsync(id, true, cancellationToken);
        AuthorizationRules.EnsureCanManageUser(actor, user.Role);
        if (actor.UserId == user.Id)
        {
            throw new ForbiddenException("Không thể tự thay đổi tài khoản quản trị hiện tại.");
        }

        var oldValues = Snapshot(user);
        EnsureText(request.Email, "email", "Email không được để trống.");
        EnsureText(request.FullName, "fullName", "Họ tên không được để trống.");
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);
        if (normalizedEmail != user.NormalizedEmail &&
            await dbContext.Users.AnyAsync(candidate => candidate.NormalizedEmail == normalizedEmail && candidate.Id != id, cancellationToken))
        {
            throw new ConflictException("Email đã được sử dụng.");
        }

        var revokeSessions = request.Role != user.Role || request.Status != user.Status;
        if (request.Role != user.Role)
        {
            AuthorizationRules.EnsureCanManageUser(actor, request.Role);
        }

        user.Email = request.Email.Trim();
        user.NormalizedEmail = normalizedEmail;
        user.FullName = request.FullName.Trim();
        user.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        user.Role = request.Role;
        user.Status = request.Status;
        user.UpdatedAt = timeProvider.GetUtcNow();
        if (revokeSessions) await RevokeSessionsAsync(user.Id, user.UpdatedAt, cancellationToken);
        AddAudit(actor, "User.Updated", user.Id, oldValues, Snapshot(user));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    public async Task ChangePasswordAsync(Guid id, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        var user = await FindRequiredAsync(id, true, cancellationToken);
        AuthorizationRules.EnsureCanManageUser(actor, user.Role);
        PasswordPolicy.Validate(request.Password);
        var now = timeProvider.GetUtcNow();
        user.PasswordHash = passwordService.Hash(user, request.Password);
        user.UpdatedAt = now;
        await RevokeSessionsAsync(user.Id, now, cancellationToken);
        AddAudit(actor, "User.PasswordChanged", user.Id, null, null);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        var user = await FindRequiredAsync(id, true, cancellationToken);
        AuthorizationRules.EnsureCanManageUser(actor, user.Role);
        if (actor.UserId == user.Id)
        {
            throw new ForbiddenException("Không thể tự xóa tài khoản hiện tại.");
        }

        var now = timeProvider.GetUtcNow();
        var oldValues = Snapshot(user);
        user.DeletedAt = now;
        user.UpdatedAt = now;
        await RevokeSessionsAsync(user.Id, now, cancellationToken);
        AddAudit(actor, "User.Deleted", user.Id, oldValues, null);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> FindRequiredAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? dbContext.Users.AsQueryable() : dbContext.Users.AsNoTracking();
        return await query.SingleOrDefaultAsync(user => user.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản.");
    }

    private Task<int> RevokeSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.AuthSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, now), cancellationToken);

    private void AddAudit(ActorContext actor, string action, Guid entityId, object? oldValues, object? newValues) =>
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

    private static IOrderedQueryable<User> ApplySort(IQueryable<User> query, string sortBy, bool descending) =>
        (sortBy.ToLowerInvariant(), descending) switch
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
            _ => throw new AppValidationException("Trường sắp xếp không hợp lệ.", new Dictionary<string, string[]>
            {
                ["sortBy"] = ["Chỉ hỗ trợ email, fullName, role, status hoặc createdAt."]
            })
        };

    private static UserResponse Map(User user) =>
        new(user.Id, user.Email, user.FullName, user.PhoneNumber, user.Role, user.Status, user.CreatedAt, user.UpdatedAt);

    private static DateTimeOffset ToUtcStart(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsureText(string value, string field, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AppValidationException(message, new Dictionary<string, string[]> { [field] = [message] });
        }
    }
}
