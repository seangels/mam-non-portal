using System.Text.Json;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AdminPortal.Application.Students;

public sealed class StudentService(
    IApplicationDbContext dbContext,
    ICurrentActor currentActor,
    IAttendancePersistence attendancePersistence,
    IBusinessDateProvider businessDateProvider,
    TimeProvider timeProvider) : IStudentService
{
    public async Task<PagedResponse<StudentResponse>> ListAsync(StudentListQuery query, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        EnsureStudentReadRole(actor);
        var students = BuildReadableStudentsQuery(actor);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862
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
        if (query.GroupId is not null && query.Unassigned == true)
        {
            throw new AppValidationException("Bộ lọc nhóm không hợp lệ.", new Dictionary<string, string[]>
            {
                ["unassigned"] = ["Không thể dùng groupId cùng unassigned=true."]
            });
        }

        if (query.GroupId is not null) students = students.Where(student => student.GroupId == query.GroupId);
        if (query.Unassigned == true) students = students.Where(student => student.GroupId == null);
        if (query.StudyMode is not null) students = students.Where(student => student.StudyMode == query.StudyMode);
        if (query.StudyWeekday is not null)
        {
            var weekdayMask = StudentScheduleRules.ToMask(query.StudyWeekday.Value);
            students = students.Where(student => (student.StudyWeekdayMask & weekdayMask) != 0);
        }

        var totalItems = await students.CountAsync(cancellationToken);
        var descending = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var page = ApplySort(students, query.SortBy, descending)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize);
        var rows = await ProjectRows(page).ToListAsync(cancellationToken);
        return new PagedResponse<StudentResponse>(
            rows.Select(Map).ToList(),
            new PaginationMetadata(query.Page, query.PageSize, totalItems,
                (int)Math.Ceiling(totalItems / (double)query.PageSize)));
    }

    public async Task<StudentResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        EnsureStudentReadRole(actor);
        var students = BuildReadableStudentsQuery(actor);
        var row = await ProjectRows(students.Where(student => student.Id == id))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw StudentNotFound();
        return Map(row);
    }

    public async Task<StudentResponse> CreateAsync(CreateStudentRequest request, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        ValidateRequired(request.StudentCode, "studentCode", "Mã học sinh là bắt buộc.");
        ValidateRequired(request.FullName, "fullName", "Họ tên là bắt buộc.");
        ValidateRequired(request.NickName, "nickName", "Tên thường gọi là bắt buộc.");
        ValidateDateOfBirth(request.DateOfBirth);
        var weekdayMask = StudentScheduleRules.Encode(request.StudySchedule);
        var driveFolderId = NormalizeDriveFolderId(request.DriveFolderId);

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
            DriveFolderId = driveFolderId,
            StudyMode = request.StudySchedule.Mode,
            StudyWeekdayMask = weekdayMask,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Students.Add(student);
        AddAudit(actor, "Student.Created", student.Id, null,
            AuditState(student, StudentEditableFields, false));
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
        await using var transaction = await attendancePersistence.BeginTransactionAsync(cancellationToken);
        await attendancePersistence.LockStudentAsync(id, cancellationToken);
        var student = await FindRequiredAsync(id, true, cancellationToken);
        EnsureVersion(student, request.ExpectedVersion);
        if (student.GroupId is not null) await attendancePersistence.LockGroupsAsync([student.GroupId.Value], cancellationToken);

        ValidateRequired(request.StudentCode, "studentCode", "Mã học sinh không được để trống.");
        ValidateRequired(request.FullName, "fullName", "Họ tên không được để trống.");
        ValidateRequired(request.NickName, "nickName", "Tên thường gọi không được để trống.");
        ValidateDateOfBirth(request.DateOfBirth);
        var weekdayMask = StudentScheduleRules.Encode(request.StudySchedule);
        var driveFolderId = NormalizeDriveFolderId(request.DriveFolderId);
        var code = NormalizeCode(request.StudentCode);
        if (code != student.StudentCode &&
            await dbContext.Students.AnyAsync(candidate => candidate.StudentCode == code && candidate.Id != id, cancellationToken))
        {
            throw new ConflictException("Mã học sinh đã được sử dụng.");
        }

        if (student.GroupId is not null && request.Status != StudentStatus.Active)
        {
            throw new ConflictException("Không thể ngừng hoạt động học sinh đang thuộc nhóm.", ProblemCodes.StudentHasCurrentGroup);
        }

        var changedFields = ChangedFields(student, request, code, driveFolderId, weekdayMask);
        var scheduleChanged = student.StudyMode != request.StudySchedule.Mode ||
            student.StudyWeekdayMask != weekdayMask;
        var snapshotChanged = student.GroupId is not null &&
            (code != student.StudentCode || request.FullName.Trim() != student.FullName ||
             request.NickName.Trim() != student.NickName || scheduleChanged);
        var oldAudit = AuditState(student, changedFields, changedFields.Contains("note", StringComparer.Ordinal));

        student.StudentCode = code;
        student.FullName = request.FullName.Trim();
        student.NickName = request.NickName.Trim();
        student.DateOfBirth = request.DateOfBirth;
        student.Gender = request.Gender;
        student.Status = request.Status;
        student.GuardianName = NormalizeOptional(request.GuardianName);
        student.GuardianPhone = NormalizeOptional(request.GuardianPhone);
        student.Note = NormalizeOptional(request.Note);
        student.DriveFolderId = driveFolderId;
        student.StudyMode = request.StudySchedule.Mode;
        student.StudyWeekdayMask = weekdayMask;
        student.Version++;
        student.UpdatedAt = timeProvider.GetUtcNow();
        if (snapshotChanged)
        {
            var group = await dbContext.StudentGroups.SingleAsync(x => x.Id == student.GroupId, cancellationToken);
            group.SnapshotVersion++;
            group.SnapshotChangedAt = student.UpdatedAt;
            group.UpdatedAt = student.UpdatedAt;
        }

        AddAudit(actor, "Student.Updated", student.Id, oldAudit,
            AuditState(student, changedFields, changedFields.Contains("note", StringComparer.Ordinal)));
        await SaveWithVersionGuardAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(student);
    }

    public async Task DeleteAsync(Guid id, int expectedVersion, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        await using var transaction = await attendancePersistence.BeginTransactionAsync(cancellationToken);
        await attendancePersistence.LockStudentAsync(id, cancellationToken);
        var student = await FindRequiredAsync(id, true, cancellationToken);
        EnsureVersion(student, expectedVersion);
        if (student.GroupId is not null)
            throw new ConflictException("Không thể xóa học sinh đang thuộc nhóm.", ProblemCodes.StudentHasCurrentGroup);
        var oldAudit = AuditState(student, ["deletedAt"], false);
        var now = timeProvider.GetUtcNow();
        student.DeletedAt = now;
        student.UpdatedAt = now;
        student.Version++;
        AddAudit(actor, "Student.Deleted", student.Id, oldAudit,
            AuditState(student, ["deletedAt"], false));
        await SaveWithVersionGuardAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<StudentResponse> AssignGroupAsync(
        Guid id,
        AssignStudentGroupRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        await using var transaction = await attendancePersistence.BeginTransactionAsync(cancellationToken);
        await attendancePersistence.LockStudentAsync(id, cancellationToken);
        var student = await FindRequiredAsync(id, true, cancellationToken);
        EnsureVersion(student, request.ExpectedVersion);
        if (student.GroupId == request.GroupId)
        {
            await transaction.CommitAsync(cancellationToken);
            return Map(student);
        }

        if (await dbContext.AttendanceRecords.AnyAsync(
            x => x.StudentId == id && x.AttendanceDate == businessDateProvider.Today,
            cancellationToken))
        {
            throw new ConflictException(
                "Không thể đổi nhóm sau khi học sinh đã có điểm danh hôm nay.",
                ProblemCodes.StudentAlreadyRecordedToday);
        }

        var groupIds = new[] { student.GroupId, request.GroupId }.Where(x => x is not null).Select(x => x!.Value);
        await attendancePersistence.LockGroupsAsync(groupIds, cancellationToken);
        StudentGroup? group = null;
        if (request.GroupId is not null)
        {
            if (student.Status != StudentStatus.Active)
                throw new ConflictException("Chỉ có thể phân nhóm học sinh đang hoạt động.", ProblemCodes.StudentInactive);
            group = await dbContext.StudentGroups.SingleOrDefaultAsync(x => x.Id == request.GroupId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy nhóm học sinh.");
            if (group.Status != GroupStatus.Active)
                throw new ConflictException("Nhóm không hoạt động.", ProblemCodes.GroupInactive);
            var activeCount = await dbContext.Students.CountAsync(
                x => x.GroupId == group.Id && x.Status == StudentStatus.Active && x.Id != id,
                cancellationToken);
            if (activeCount >= 100)
                throw new ConflictException("Nhóm đã đủ tối đa 100 học sinh.", ProblemCodes.GroupCapacityExceeded);
        }

        var oldGroupId = student.GroupId;
        var versionBefore = student.Version;
        var now = timeProvider.GetUtcNow();
        student.GroupId = group?.Id;
        student.Group = group;
        student.GroupAssignedAt = group is null ? null : now;
        student.GroupAssignedByUserId = group is null ? null : actor.UserId;
        student.Version++;
        student.UpdatedAt = now;
        var affectedGroups = await dbContext.StudentGroups
            .Where(x => x.Id == oldGroupId || x.Id == student.GroupId)
            .ToListAsync(cancellationToken);
        foreach (var affected in affectedGroups)
        {
            affected.SnapshotVersion++;
            affected.SnapshotChangedAt = now;
            affected.UpdatedAt = now;
        }

        var action = oldGroupId is null
            ? "Student.GroupAssigned"
            : student.GroupId is null
                ? "Student.GroupRemoved"
                : "Student.GroupMoved";
        AddAudit(actor, action, student.Id,
            new { groupId = oldGroupId, version = versionBefore },
            new { student.GroupId, student.Version });
        await SaveWithVersionGuardAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(student);
    }

    private async Task<Student> FindRequiredAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = (tracked ? dbContext.Students.AsQueryable() : dbContext.Students.AsNoTracking()).Include(x => x.Group);
        return await query.SingleOrDefaultAsync(student => student.Id == id, cancellationToken) ?? throw StudentNotFound();
    }

    private async Task SaveWithVersionGuardAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Phiên bản học sinh đã thay đổi.", ProblemCodes.StudentVersionConflict);
        }
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

    private static object AuditState(Student student, IReadOnlyCollection<string> changedFields, bool noteChanged) => new
    {
        student.Id,
        student.StudentCode,
        Status = student.Status.ToString(),
        StudyMode = student.StudyMode.ToString(),
        student.StudyWeekdayMask,
        student.Version,
        ChangedFields = changedFields,
        NoteChanged = noteChanged,
        IsDeleted = student.DeletedAt is not null
    };

    private static string[] ChangedFields(
        Student student,
        UpdateStudentRequest request,
        string normalizedCode,
        string? normalizedDriveFolderId,
        short weekdayMask)
    {
        var fields = new List<string>();
        if (student.StudentCode != normalizedCode) fields.Add("studentCode");
        if (student.FullName != request.FullName.Trim()) fields.Add("fullName");
        if (student.NickName != request.NickName.Trim()) fields.Add("nickName");
        if (student.DateOfBirth != request.DateOfBirth) fields.Add("dateOfBirth");
        if (student.Gender != request.Gender) fields.Add("gender");
        if (student.Status != request.Status) fields.Add("status");
        if (student.GuardianName != NormalizeOptional(request.GuardianName)) fields.Add("guardianName");
        if (student.GuardianPhone != NormalizeOptional(request.GuardianPhone)) fields.Add("guardianPhone");
        if (student.Note != NormalizeOptional(request.Note)) fields.Add("note");
        if (student.DriveFolderId != normalizedDriveFolderId) fields.Add("driveFolderId");
        if (student.StudyMode != request.StudySchedule.Mode) fields.Add("studySchedule.mode");
        if (student.StudyWeekdayMask != weekdayMask) fields.Add("studySchedule.weekdays");
        return fields.ToArray();
    }

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
            ("studymode", false) => query.OrderBy(student => student.StudyMode).ThenBy(student => student.Id),
            ("studymode", true) => query.OrderByDescending(student => student.StudyMode).ThenByDescending(student => student.Id),
            ("createdat", false) => query.OrderBy(student => student.CreatedAt).ThenBy(student => student.Id),
            ("createdat", true) => query.OrderByDescending(student => student.CreatedAt).ThenByDescending(student => student.Id),
            _ => throw new AppValidationException("Trường sắp xếp không hợp lệ.", new Dictionary<string, string[]>
            {
                ["sortBy"] = ["Chỉ hỗ trợ studentCode, fullName, nickName, dateOfBirth, gender, status, studyMode hoặc createdAt."]
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
        student.DriveFolderId,
        student.GroupId,
        student.Group?.Code,
        student.Group?.Name,
        student.Group?.ResponsibleTeacher?.User?.FullName,
        new StudyScheduleResponse(student.StudyMode, StudentScheduleRules.Decode(student.StudyWeekdayMask)),
        student.Version, student.CreatedAt, student.UpdatedAt);

    private static StudentResponse Map(StudentRow row) => new(
        row.Id, 
        row.StudentCode, 
        row.FullName, 
        row.NickName, 
        row.DateOfBirth,
        row.Gender, 
        row.Status, 
        row.GuardianName,
        row.GuardianPhone,
        row.Note,
        row.DriveFolderId,
        row.GroupId,
        row.GroupCode,
        row.GroupName,
        row.ResponsibleTeacherName,
        new StudyScheduleResponse(row.StudyMode, StudentScheduleRules.Decode(row.StudyWeekdayMask)),
        row.Version, row.CreatedAt, row.UpdatedAt);

    private static IQueryable<StudentRow> ProjectRows(IQueryable<Student> query) =>
        query.Select(student => new StudentRow(
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
            student.DriveFolderId,
            student.GroupId,
            student.Group == null ? null : student.Group.Code,
            student.Group == null ? null : student.Group.Name,
            student.Group == null || student.Group.ResponsibleTeacher == null || student.Group.ResponsibleTeacher.User == null ? null : student.Group.ResponsibleTeacher.User.FullName,
            student.StudyMode,
            student.StudyWeekdayMask,
            student.Version,
            student.CreatedAt,
            student.UpdatedAt));

    private static void EnsureStudentReadRole(ActorContext actor)
    {
        if (actor.Role is not (UserRole.SuperAdmin or UserRole.Admin or UserRole.Teacher))
            throw new ForbiddenException("Tài khoản không có quyền xem học sinh.");
    }

    private IQueryable<Student> BuildReadableStudentsQuery(ActorContext actor)
    {
        var students = dbContext.Students.AsNoTracking();
        if (actor.Role != UserRole.Teacher)
            return students;

        return students.Where(student =>
            student.Group != null &&
            student.Group.ResponsibleTeacher != null &&
            student.Group.ResponsibleTeacher.UserId == actor.UserId);
    }

    private static void EnsureVersion(Student student, int expectedVersion)
    {
        if (student.Version != expectedVersion)
        {
            throw new ConflictException(
                "Phiên bản học sinh đã thay đổi.",
                ProblemCodes.StudentVersionConflict,
                new Dictionary<string, object?> { ["currentVersion"] = student.Version });
        }
    }

    private static NotFoundException StudentNotFound() =>
        new("Không tìm thấy học sinh.", ProblemCodes.StudentNotFound);

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeDriveFolderId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var input = value.Trim();
        var folderId = Uri.TryCreate(input, UriKind.Absolute, out var uri)
            ? ExtractDriveFolderId(uri)
            : input;

        if (!IsValidDriveFolderId(folderId))
        {
            const string message = "Drive Folder ID không hợp lệ. Hãy nhập ID thư mục hoặc link Google Drive có dạng /folders/{id} hay ?id={id}.";
            throw new AppValidationException(message, new Dictionary<string, string[]>
            {
                ["driveFolderId"] = [message]
            });
        }

        return folderId;
    }

    private static string? ExtractDriveFolderId(Uri uri)
    {
        if (uri.Scheme is not ("http" or "https") ||
            !uri.Host.Equals("drive.google.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (segments[index].Equals("folders", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(segments[index + 1]);
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            var key = separatorIndex < 0 ? pair : pair[..separatorIndex];
            if (!Uri.UnescapeDataString(key).Equals("id", StringComparison.OrdinalIgnoreCase))
                continue;

            var encodedValue = separatorIndex < 0 ? string.Empty : pair[(separatorIndex + 1)..];
            return Uri.UnescapeDataString(encodedValue.Replace('+', ' '));
        }

        return null;
    }

    private static bool IsValidDriveFolderId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 200 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private void ValidateDateOfBirth(DateOnly value) =>
        StudentRules.ValidateDateOfBirth(value, DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));

    private static void ValidateRequired(string value, string field, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new AppValidationException(message, new Dictionary<string, string[]> { [field] = [message] });
    }

    private static readonly string[] StudentEditableFields =
    [
        "studentCode", "fullName", "nickName", "dateOfBirth", "gender", "status",
        "guardianName", "guardianPhone", "note", "driveFolderId", "studySchedule.mode", "studySchedule.weekdays"
    ];

    private sealed record StudentRow(
        Guid Id,
        string StudentCode,
        string FullName,
        string NickName,
        DateOnly DateOfBirth,
        Gender? Gender,
        StudentStatus Status,
        string? GuardianName,
        string? GuardianPhone,
        string? Note,
        string? DriveFolderId,
        Guid? GroupId,
        string? GroupCode, 
        string? GroupName, 
        string? ResponsibleTeacherName,
        StudyMode StudyMode,
        short StudyWeekdayMask, 
        int Version, 
        DateTimeOffset CreatedAt, 
        DateTimeOffset UpdatedAt);
}
