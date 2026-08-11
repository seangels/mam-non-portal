using System.Text.Json;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdminPortal.Application.Students;

public sealed class StudentService(
    IApplicationDbContext dbContext,
    ICurrentActor currentActor,
    TimeProvider timeProvider) : IStudentService
{
    public async Task<PagedResponse<StudentResponse>> ListAsync(StudentListQuery query, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        var students = dbContext.Students.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // Parameterless ToLower is translated to PostgreSQL lower().
            students = students.Where(student =>
                student.StudentCode.ToLower().Contains(search) ||
                student.FullName.ToLower().Contains(search) ||
                student.NickName.ToLower().Contains(search) ||
                (student.GuardianName != null && student.GuardianName.ToLower().Contains(search)) ||
                (student.GuardianPhone != null && student.GuardianPhone.Contains(search)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        if (query.Status is not null) students = students.Where(student => student.Status == query.Status);
        if (query.Gender is not null) students = students.Where(student => student.Gender == query.Gender);
        if (query.DateOfBirthFrom is not null) students = students.Where(student => student.DateOfBirth >= query.DateOfBirthFrom);
        if (query.DateOfBirthTo is not null) students = students.Where(student => student.DateOfBirth <= query.DateOfBirthTo);

        var totalItems = await students.CountAsync(cancellationToken);
        var descending = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var items = await ApplySort(students, query.SortBy, descending)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(student => Map(student))
            .ToListAsync(cancellationToken);
        return new PagedResponse<StudentResponse>(
            items,
            new PaginationMetadata(query.Page, query.PageSize, totalItems, (int)Math.Ceiling(totalItems / (double)query.PageSize)));
    }

    public async Task<StudentResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        AuthorizationRules.EnsurePortalManager(currentActor.GetRequired());
        return Map(await FindRequiredAsync(id, false, cancellationToken));
    }

    public async Task<StudentResponse> CreateAsync(CreateStudentRequest request, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        ValidateRequired(request.StudentCode, "studentCode", "Mã học sinh là bắt buộc.");
        ValidateRequired(request.FullName, "fullName", "Họ tên là bắt buộc.");
        ValidateRequired(request.NickName, "nickName", "Tên thường gọi là bắt buộc.");
        ValidateDateOfBirth(request.DateOfBirth);

        var code = NormalizeCode(request.StudentCode);
        if (await dbContext.Students.AnyAsync(student => student.StudentCode == code, cancellationToken))
        {
            throw new ConflictException("Mã học sinh đã được sử dụng.");
        }

        var now = timeProvider.GetUtcNow();
        var student = new Student
        {
            Id = Guid.NewGuid(),
            StudentCode = code,
            FullName = request.FullName.Trim(),
            NickName = request.NickName.Trim(),
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Status = request.Status,
            GuardianName = NormalizeOptional(request.GuardianName),
            GuardianPhone = NormalizeOptional(request.GuardianPhone),
            Note = NormalizeOptional(request.Note),
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Students.Add(student);
        AddAudit(actor, "Student.Created", student.Id, null, Snapshot(student));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(student);
    }

    public async Task<StudentResponse> UpdateAsync(
        Guid id,
        UpdateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        var student = await FindRequiredAsync(id, true, cancellationToken);
        var oldValues = Snapshot(student);
        ValidateRequired(request.StudentCode, "studentCode", "Mã học sinh không được để trống.");
        ValidateRequired(request.FullName, "fullName", "Họ tên không được để trống.");
        ValidateRequired(request.NickName, "nickName", "Tên thường gọi không được để trống.");
        ValidateDateOfBirth(request.DateOfBirth);
        var code = NormalizeCode(request.StudentCode);
        if (code != student.StudentCode &&
            await dbContext.Students.AnyAsync(candidate => candidate.StudentCode == code && candidate.Id != id, cancellationToken))
        {
            throw new ConflictException("Mã học sinh đã được sử dụng.");
        }

        student.StudentCode = code;
        student.FullName = request.FullName.Trim();
        student.NickName = request.NickName.Trim();
        student.DateOfBirth = request.DateOfBirth;
        student.Gender = request.Gender;
        student.Status = request.Status;
        student.GuardianName = NormalizeOptional(request.GuardianName);
        student.GuardianPhone = NormalizeOptional(request.GuardianPhone);
        student.Note = NormalizeOptional(request.Note);
        student.UpdatedAt = timeProvider.GetUtcNow();
        AddAudit(actor, "Student.Updated", student.Id, oldValues, Snapshot(student));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(student);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        var student = await FindRequiredAsync(id, true, cancellationToken);
        var oldValues = Snapshot(student);
        var now = timeProvider.GetUtcNow();
        student.DeletedAt = now;
        student.UpdatedAt = now;
        AddAudit(actor, "Student.Deleted", student.Id, oldValues, null);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Student> FindRequiredAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? dbContext.Students.AsQueryable() : dbContext.Students.AsNoTracking();
        return await query.SingleOrDefaultAsync(student => student.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy học sinh.");
    }

    private void AddAudit(ActorContext actor, string action, Guid entityId, object? oldValues, object? newValues) =>
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor.UserId,
            Action = action,
            EntityType = "Student",
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = actor.IpAddress,
            CreatedAt = timeProvider.GetUtcNow()
        });

    private static object Snapshot(Student student) => new
    {
        student.StudentCode,
        student.FullName,
        student.NickName,
        student.DateOfBirth,
        Gender = student.Gender?.ToString(),
        Status = student.Status.ToString(),
        student.GuardianName,
        student.GuardianPhone,
        student.Note,
        student.DeletedAt
    };

    private static IOrderedQueryable<Student> ApplySort(IQueryable<Student> query, string sortBy, bool descending) =>
        (sortBy.ToLowerInvariant(), descending) switch
        {
            ("studentcode", false) => query.OrderBy(student => student.StudentCode).ThenBy(student => student.Id),
            ("studentcode", true) => query.OrderByDescending(student => student.StudentCode).ThenByDescending(student => student.Id),
            ("fullname", false) => query.OrderBy(student => student.FullName).ThenBy(student => student.Id),
            ("fullname", true) => query.OrderByDescending(student => student.FullName).ThenByDescending(student => student.Id),
            ("nickname", false) => query.OrderBy(student => student.NickName).ThenBy(student => student.Id),
            ("nickname", true) => query.OrderByDescending(student => student.NickName).ThenByDescending(student => student.Id),
            ("dateofbirth", false) => query.OrderBy(student => student.DateOfBirth).ThenBy(student => student.Id),
            ("dateofbirth", true) => query.OrderByDescending(student => student.DateOfBirth).ThenByDescending(student => student.Id),
            ("gender", false) => query.OrderBy(student => student.Gender).ThenBy(student => student.Id),
            ("gender", true) => query.OrderByDescending(student => student.Gender).ThenByDescending(student => student.Id),
            ("status", false) => query.OrderBy(student => student.Status).ThenBy(student => student.Id),
            ("status", true) => query.OrderByDescending(student => student.Status).ThenByDescending(student => student.Id),
            ("createdat", false) => query.OrderBy(student => student.CreatedAt).ThenBy(student => student.Id),
            ("createdat", true) => query.OrderByDescending(student => student.CreatedAt).ThenByDescending(student => student.Id),
            _ => throw new AppValidationException("Trường sắp xếp không hợp lệ.", new Dictionary<string, string[]>
            {
                ["sortBy"] = ["Chỉ hỗ trợ studentCode, fullName, nickName, dateOfBirth, gender, status hoặc createdAt."]
            })
        };

    private static StudentResponse Map(Student student) => new(
        student.Id,
        student.StudentCode,
        student.FullName,
        student.NickName,
        student.DateOfBirth,
        student.Gender,
        student.Status,
        student.GuardianName,
        student.GuardianPhone,
        student.Note,
        student.CreatedAt,
        student.UpdatedAt);

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ValidateDateOfBirth(DateOnly value)
        => StudentRules.ValidateDateOfBirth(value, DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));

    private static void ValidateRequired(string value, string field, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AppValidationException(message, new Dictionary<string, string[]> { [field] = [message] });
        }
    }
}
