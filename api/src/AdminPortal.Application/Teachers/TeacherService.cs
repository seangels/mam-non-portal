using System.Text.Json;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AdminPortal.Application.Teachers;

public sealed class TeacherService(
    IApplicationDbContext dbContext,
    ICurrentActor currentActor,
    TimeProvider timeProvider) : ITeacherService
{
    public async Task<PagedResponse<TeacherResponse>> ListAsync(
        TeacherListQuery query,
        CancellationToken cancellationToken)
    {
        AuthorizationRules.EnsurePortalManager(currentActor.GetRequired());
        var teachers = dbContext.Teachers.AsNoTracking()
            .Where(x => x.User.Role == UserRole.Teacher);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862
            teachers = teachers.Where(x => x.User.FullName.ToLower().Contains(search));
#pragma warning restore CA1304, CA1311, CA1862
        }

        if (query.Status is not null) teachers = teachers.Where(x => x.User.Status == query.Status);
        if (query.Unassigned == true) teachers = teachers.Where(x => !x.ResponsibleGroups.Any());

        var total = await teachers.CountAsync(cancellationToken);
        var descending = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var items = await ApplySort(teachers, query.SortBy, descending)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TeacherResponse(
                x.Id,
                x.UserId,
                x.User.FullName,
                x.User.Status,
                x.AttendanceEditWindowDays,
                x.ResponsibleGroups.Count))
            .ToListAsync(cancellationToken);
        return new PagedResponse<TeacherResponse>(items,
            new PaginationMetadata(query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)));
    }

    public async Task<TeacherResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        AuthorizationRules.EnsurePortalManager(currentActor.GetRequired());
        return await QueryCurrent().Where(x => x.Id == id)
            .Select(x => new TeacherResponse(
                x.Id, x.UserId, x.User.FullName, x.User.Status,
                x.AttendanceEditWindowDays, x.ResponsibleGroups.Count))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy giáo viên.");
    }

    public async Task<TeacherResponse> UpdateAttendancePolicyAsync(
        Guid id,
        UpdateAttendancePolicyRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        if (request.AttendanceEditWindowDays is < 1 or > 7)
        {
            throw new AppValidationException("Cửa sổ sửa điểm danh không hợp lệ.", new Dictionary<string, string[]>
            {
                ["attendanceEditWindowDays"] = ["Giá trị phải từ 1 đến 7 ngày."]
            });
        }

        var teacher = await dbContext.Teachers.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == id && x.User.Role == UserRole.Teacher, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy giáo viên.");
        var oldValue = teacher.AttendanceEditWindowDays;
        teacher.AttendanceEditWindowDays = checked((short)request.AttendanceEditWindowDays);
        teacher.UpdatedAt = timeProvider.GetUtcNow();
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor.UserId,
            Action = "Teacher.AttendancePolicyUpdated",
            EntityType = "Teacher",
            EntityId = teacher.Id,
            OldValues = JsonSerializer.Serialize(new { attendanceEditWindowDays = oldValue }),
            NewValues = JsonSerializer.Serialize(new { teacher.AttendanceEditWindowDays }),
            IpAddress = actor.IpAddress,
            CreatedAt = teacher.UpdatedAt
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private IQueryable<Teacher> QueryCurrent() => dbContext.Teachers.AsNoTracking()
        .Where(x => x.User.Role == UserRole.Teacher);

    private static IOrderedQueryable<Teacher> ApplySort(
        IQueryable<Teacher> query,
        string sortBy,
        bool descending) => (sortBy.ToLowerInvariant(), descending) switch
        {
            ("fullname", false) => query.OrderBy(x => x.User.FullName).ThenBy(x => x.Id),
            ("fullname", true) => query.OrderByDescending(x => x.User.FullName).ThenByDescending(x => x.Id),
            ("status", false) => query.OrderBy(x => x.User.Status).ThenBy(x => x.Id),
            ("status", true) => query.OrderByDescending(x => x.User.Status).ThenByDescending(x => x.Id),
            ("attendanceeditwindowdays", false) => query.OrderBy(x => x.AttendanceEditWindowDays).ThenBy(x => x.Id),
            ("attendanceeditwindowdays", true) => query.OrderByDescending(x => x.AttendanceEditWindowDays).ThenByDescending(x => x.Id),
            ("responsiblegroupcount", false) => query.OrderBy(x => x.ResponsibleGroups.Count).ThenBy(x => x.Id),
            ("responsiblegroupcount", true) => query.OrderByDescending(x => x.ResponsibleGroups.Count).ThenByDescending(x => x.Id),
            _ => throw new AppValidationException("Trường sắp xếp không hợp lệ.", new Dictionary<string, string[]>
            {
                ["sortBy"] = ["Chỉ hỗ trợ fullName, status, attendanceEditWindowDays hoặc responsibleGroupCount."]
            })
        };
}
