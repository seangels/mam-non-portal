using System.Text.Json;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AdminPortal.Application.StudentGroups;

public sealed class StudentGroupService(
    IApplicationDbContext dbContext,
    ICurrentActor currentActor,
    IAttendancePersistence attendancePersistence,
    TimeProvider timeProvider) : IStudentGroupService
{
    public async Task<PagedResponse<StudentGroupResponse>> ListAsync(
        StudentGroupListQuery query,
        CancellationToken cancellationToken)
    {
        AuthorizationRules.EnsurePortalManager(currentActor.GetRequired());
        var groups = dbContext.StudentGroups.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862
            groups = groups.Where(x => x.Code.ToLower().Contains(search) || x.Name.ToLower().Contains(search));
#pragma warning restore CA1304, CA1311, CA1862
        }
        if (query.Status is not null) groups = groups.Where(x => x.Status == query.Status);
        if (query.Unassigned == true) groups = groups.Where(x => x.ResponsibleTeacherId == null);
        var total = await groups.CountAsync(cancellationToken);
        var descending = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var page = ApplySort(groups, query.SortBy, descending)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize);
        var items = await Project(page)
            .ToListAsync(cancellationToken);
        return new PagedResponse<StudentGroupResponse>(items,
            new PaginationMetadata(query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)));
    }

    public async Task<StudentGroupResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        AuthorizationRules.EnsurePortalManager(currentActor.GetRequired());
        return await Project(dbContext.StudentGroups.AsNoTracking().Where(x => x.Id == id))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhóm học sinh.");
    }

    public async Task<StudentGroupResponse> CreateAsync(
        CreateStudentGroupRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        var code = NormalizeRequired(request.Code, "code", "Mã nhóm là bắt buộc.").ToUpperInvariant();
        var name = NormalizeRequired(request.Name, "name", "Tên nhóm là bắt buộc.");
        if (await dbContext.StudentGroups.AnyAsync(x => x.Code == code, cancellationToken))
            throw new ConflictException("Mã nhóm đã được sử dụng.");
        var now = timeProvider.GetUtcNow();
        var group = new StudentGroup
        {
            Id = Guid.NewGuid(), Code = code, Name = name, Status = request.Status,
            SnapshotVersion = 1, SnapshotChangedAt = now, CreatedAt = now, UpdatedAt = now
        };
        dbContext.StudentGroups.Add(group);
        AddAudit(actor, "Group.Created", group.Id, null, Snapshot(group));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(group.Id, cancellationToken);
    }

    public async Task<StudentGroupResponse> UpdateAsync(
        Guid id,
        UpdateStudentGroupRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        await using var transaction = await attendancePersistence.BeginTransactionAsync(cancellationToken);
        await attendancePersistence.LockGroupsAsync([id], cancellationToken);
        var group = await FindRequiredAsync(id, cancellationToken);
        var code = NormalizeRequired(request.Code, "code", "Mã nhóm là bắt buộc.").ToUpperInvariant();
        var name = NormalizeRequired(request.Name, "name", "Tên nhóm là bắt buộc.");
        if (code != group.Code && await dbContext.StudentGroups.AnyAsync(x => x.Code == code && x.Id != id, cancellationToken))
            throw new ConflictException("Mã nhóm đã được sử dụng.");
        if (request.Status == GroupStatus.Inactive && group.Status != GroupStatus.Inactive)
            await EnsureCanDeactivateAsync(group, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var old = Snapshot(group);
        if (code != group.Code || name != group.Name)
        {
            group.SnapshotVersion++;
            group.SnapshotChangedAt = now;
        }
        group.Code = code;
        group.Name = name;
        group.Status = request.Status;
        group.UpdatedAt = now;
        AddAudit(actor, "Group.Updated", group.Id, old, Snapshot(group));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(group.Id, cancellationToken);
    }

    public async Task<StudentGroupResponse> AssignResponsibleTeacherAsync(
        Guid id,
        AssignResponsibleTeacherRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        await using var transaction = await attendancePersistence.BeginTransactionAsync(cancellationToken);
        if (request.TeacherId is not null)
        {
            await attendancePersistence.LockTeachersAsync([request.TeacherId.Value], cancellationToken);
        }
        await attendancePersistence.LockGroupsAsync([id], cancellationToken);
        var group = await FindRequiredAsync(id, cancellationToken);
        Teacher? teacher = null;
        if (request.TeacherId is not null)
        {
            teacher = await dbContext.Teachers.Include(x => x.User)
                .SingleOrDefaultAsync(x => x.Id == request.TeacherId && x.User.Role == UserRole.Teacher, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy giáo viên.");
            if (teacher.User.Status != UserStatus.Active)
                throw new ConflictException("Chỉ có thể phân công giáo viên đang hoạt động.");
        }
        if (group.ResponsibleTeacherId != teacher?.Id)
        {
            var old = new { group.ResponsibleTeacherId };
            group.ResponsibleTeacherId = teacher?.Id;
            group.ResponsibleTeacher = teacher;
            group.SnapshotVersion++;
            group.SnapshotChangedAt = timeProvider.GetUtcNow();
            group.UpdatedAt = group.SnapshotChangedAt;
            var action = teacher is null ? "Group.ResponsibleTeacherRemoved" : "Group.ResponsibleTeacherAssigned";
            AddAudit(actor, action, group.Id, old,
                new { group.ResponsibleTeacherId, group.SnapshotVersion });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(group.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        await using var transaction = await attendancePersistence.BeginTransactionAsync(cancellationToken);
        await attendancePersistence.LockGroupsAsync([id], cancellationToken);
        var group = await FindRequiredAsync(id, cancellationToken);
        await EnsureCanDeactivateAsync(group, cancellationToken);
        var old = Snapshot(group);
        group.DeletedAt = timeProvider.GetUtcNow();
        group.UpdatedAt = group.DeletedAt.Value;
        AddAudit(actor, "Group.Deleted", group.Id, old, null);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsureCanDeactivateAsync(StudentGroup group, CancellationToken cancellationToken)
    {
        if (group.ResponsibleTeacherId is not null)
            throw new ConflictException("Nhóm vẫn còn giáo viên phụ trách.", ProblemCodes.GroupHasResponsibleTeacher);
        if (await dbContext.Students.AnyAsync(x => x.GroupId == group.Id, cancellationToken))
            throw new ConflictException("Nhóm vẫn còn học sinh.", ProblemCodes.GroupHasStudents);
    }

    private async Task<StudentGroup> FindRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.StudentGroups.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy nhóm học sinh.");

    private void AddAudit(ActorContext actor, string action, Guid entityId, object? oldValue, object? newValue) =>
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor.UserId, Action = action, EntityType = "Group", EntityId = entityId,
            OldValues = oldValue is null ? null : JsonSerializer.Serialize(oldValue),
            NewValues = newValue is null ? null : JsonSerializer.Serialize(newValue),
            IpAddress = actor.IpAddress, CreatedAt = timeProvider.GetUtcNow()
        });

    private static object Snapshot(StudentGroup group) => new
    {
        group.Code, group.Name, Status = group.Status.ToString(), group.ResponsibleTeacherId,
        group.SnapshotVersion, group.DeletedAt
    };

    private static IQueryable<StudentGroupResponse> Project(IQueryable<StudentGroup> query) =>
        query.Select(x => new StudentGroupResponse(
            x.Id,
            x.Code,
            x.Name,
            x.Status,
            x.ResponsibleTeacherId,
            x.ResponsibleTeacher == null ? null : x.ResponsibleTeacher.User.FullName,
            x.Students.Count(student => student.Status == StudentStatus.Active),
            x.SnapshotVersion,
            x.CreatedAt,
            x.UpdatedAt));

    private static IOrderedQueryable<StudentGroup> ApplySort(
        IQueryable<StudentGroup> query, string sortBy, bool descending) =>
        (sortBy.ToLowerInvariant(), descending) switch
        {
            ("code", false) => query.OrderBy(x => x.Code).ThenBy(x => x.Id),
            ("code", true) => query.OrderByDescending(x => x.Code).ThenByDescending(x => x.Id),
            ("name", false) => query.OrderBy(x => x.Name).ThenBy(x => x.Id),
            ("name", true) => query.OrderByDescending(x => x.Name).ThenByDescending(x => x.Id),
            ("status", false) => query.OrderBy(x => x.Status).ThenBy(x => x.Id),
            ("status", true) => query.OrderByDescending(x => x.Status).ThenByDescending(x => x.Id),
            ("studentcount", false) => query.OrderBy(x => x.Students.Count(s => s.Status == StudentStatus.Active)).ThenBy(x => x.Id),
            ("studentcount", true) => query.OrderByDescending(x => x.Students.Count(s => s.Status == StudentStatus.Active)).ThenByDescending(x => x.Id),
            ("snapshotversion", false) => query.OrderBy(x => x.SnapshotVersion).ThenBy(x => x.Id),
            ("snapshotversion", true) => query.OrderByDescending(x => x.SnapshotVersion).ThenByDescending(x => x.Id),
            ("createdat", false) => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            ("createdat", true) => query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id),
            _ => throw new AppValidationException("Trường sắp xếp không hợp lệ.", new Dictionary<string, string[]>
            { ["sortBy"] = ["Chỉ hỗ trợ code, name, status, studentCount, snapshotVersion hoặc createdAt."] })
        };

    private static string NormalizeRequired(string value, string field, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new AppValidationException(message, new Dictionary<string, string[]> { [field] = [message] });
        return value.Trim();
    }
}
