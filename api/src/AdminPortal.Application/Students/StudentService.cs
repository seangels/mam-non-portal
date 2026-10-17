using System.Text.Json;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using AdminPortal.Application.StudentDrive;
using AdminPortal.Application.GoogleSheets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AdminPortal.Application.Students;

public sealed partial class StudentService(
    IApplicationDbContext dbContext,
    ICurrentActor currentActor,
    IAttendancePersistence attendancePersistence,
    IBusinessDateProvider businessDateProvider,
    IStudentDriveFolderService studentDriveFolderService,
    IStudentGoogleSheetsService studentGoogleSheetsService,
    TimeProvider timeProvider,
    ILogger<StudentService> logger) : IStudentService
{
    public async Task<StudentCodeSuggestionsResponse> GetStudentCodeSuggestionsAsync(
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        var sheetCodes = await studentGoogleSheetsService.GetAvailableStudentCodesAsync(cancellationToken);
        if (sheetCodes.Count == 0) return new StudentCodeSuggestionsResponse([]);

        var activeDatabaseCodes = await dbContext.Students
            .AsNoTracking()
            .Select(student => student.StudentCode)
            .ToListAsync(cancellationToken);
        var used = activeDatabaseCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new StudentCodeSuggestionsResponse(sheetCodes.Where(code => !used.Contains(code)).ToList());
    }

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
        var requestedDriveFolderId = StudentDriveFolderRules.NormalizeFolderId(request.DriveFolderId);

        var code = NormalizeCode(request.StudentCode);
        if (await dbContext.Students.AnyAsync(student => student.StudentCode == code, cancellationToken))
        {
            throw new ConflictException("Mã học sinh đã được sử dụng.");
        }

        await using var transaction = await attendancePersistence.BeginTransactionAsync(cancellationToken);
        var driveMutation = await studentDriveFolderService.EnsureStudentFolderAsync(
            code,
            request.FullName.Trim(),
            requestedDriveFolderId,
            studentDriveFolderService.GetRequiredPoolFolderId(),
            cancellationToken);
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
            DriveFolderId = driveMutation.Folder.Id,
            GoogleSheetsStudentId = code,
            StudyMode = request.StudySchedule.Mode,
            StudyWeekdayMask = weekdayMask,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Students.Add(student);
        StudentSheetMutation? sheetMutation = null;
        try
        {
            AddAudit(actor, "Student.Created", student.Id, null,
                AuditState(student, StudentEditableFields, false));
            await dbContext.SaveChangesAsync(cancellationToken);
            sheetMutation = await studentGoogleSheetsService.CreateOrUpdateAsync(
                student.GoogleSheetsStudentId,
                student.FullName,
                student.NickName,
                student.DateOfBirth,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await CompensateStudentSheetAsync(sheetMutation, CancellationToken.None);
            await CompensateAsync(driveMutation, cancellationToken);
            throw;
        }
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
        var requestedDriveFolderId = StudentDriveFolderRules.NormalizeFolderId(request.DriveFolderId);
        var code = NormalizeCode(request.StudentCode);
        if (code != student.StudentCode &&
            await dbContext.Students.AnyAsync(candidate => candidate.StudentCode == code && candidate.Id != id, cancellationToken))
        {
            throw new ConflictException("Mã học sinh đã được sử dụng.");
        }

        if (student.GroupId is not null &&
            student.Status != StudentStatus.Active &&
            request.Status == StudentStatus.Active)
        {
            var activeCount = await dbContext.Students.CountAsync(
                candidate => candidate.GroupId == student.GroupId &&
                    candidate.Status == StudentStatus.Active && candidate.Id != id,
                cancellationToken);
            if (activeCount >= 100)
                throw new ConflictException("Nhóm đã đủ tối đa 100 học sinh.", ProblemCodes.GroupCapacityExceeded);
        }

        var destinationFolderId = await ResolveDestinationFolderIdAsync(student.GroupId, cancellationToken);
        var driveMutation = await studentDriveFolderService.EnsureStudentFolderAsync(
            code,
            request.FullName.Trim(),
            requestedDriveFolderId ?? student.DriveFolderId,
            destinationFolderId,
            cancellationToken);
        var driveFolderId = driveMutation.Folder.Id;
        var sheetStudentId = StudentGoogleSheetsRules.ResolveStableStudentId(
            student.GoogleSheetsStudentId,
            student.StudentCode);
        var changedFields = ChangedFields(student, request, code, driveFolderId, weekdayMask);
        var snapshotChanged = student.GroupId is not null &&
            StudentRules.AffectsAttendanceSnapshot(changedFields);
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
        student.GoogleSheetsStudentId = sheetStudentId;
        student.StudyMode = request.StudySchedule.Mode;
        student.StudyWeekdayMask = weekdayMask;
        student.Version++;
        student.UpdatedAt = timeProvider.GetUtcNow();
        try
        {
            if (snapshotChanged)
            {
                var group = await dbContext.StudentGroups.SingleAsync(x => x.Id == student.GroupId, cancellationToken);
                group.SnapshotVersion++;
                group.SnapshotChangedAt = student.UpdatedAt;
                group.UpdatedAt = student.UpdatedAt;
            }
        }
        catch
        {
            await CompensateAsync(driveMutation, cancellationToken);
            throw;
        }

        StudentSheetMutation? sheetMutation = null;
        try
        {
            AddAudit(actor, "Student.Updated", student.Id, oldAudit,
                AuditState(student, changedFields, changedFields.Contains("note", StringComparer.Ordinal)));
            await SaveWithVersionGuardAsync(cancellationToken);
            sheetMutation = await studentGoogleSheetsService.UpdateAsync(
                sheetStudentId,
                student.FullName,
                student.NickName,
                student.DateOfBirth,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await CompensateStudentSheetAsync(sheetMutation, CancellationToken.None);
            await CompensateAsync(driveMutation, cancellationToken);
            throw;
        }
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

        var destinationFolderId = await ResolveDestinationFolderIdAsync(group?.Id, cancellationToken);
        var driveMutation = await studentDriveFolderService.EnsureStudentFolderAsync(
            student.StudentCode,
            student.FullName,
            student.DriveFolderId,
            destinationFolderId,
            cancellationToken);

        var oldGroupId = student.GroupId;
        var versionBefore = student.Version;
        var now = timeProvider.GetUtcNow();
        student.GroupId = group?.Id;
        student.Group = group;
        student.GroupAssignedAt = group is null ? null : now;
        student.GroupAssignedByUserId = group is null ? null : actor.UserId;
        student.DriveFolderId = driveMutation.Folder.Id;
        student.Version++;
        student.UpdatedAt = now;
        try
        {
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
        }
        catch
        {
            await CompensateAsync(driveMutation, cancellationToken);
            throw;
        }
        return Map(student);
    }

    public async Task<StudentDriveFolderActionResponse> RenameDriveFolderAsync(
        Guid id,
        RenameStudentDriveFolderRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        var student = await dbContext.Students.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw StudentNotFound();
        EnsureVersion(student, request.ExpectedVersion);
        if (string.IsNullOrWhiteSpace(student.DriveFolderId))
            throw new ConflictException("Học sinh chưa có thư mục Google Drive.", ProblemCodes.StudentDriveFolderRequired);

        var previousFolder = await studentDriveFolderService.ValidateFolderAsync(
            student.DriveFolderId, cancellationToken);
        var folder = await studentDriveFolderService.RenameFolderAsync(
            student.DriveFolderId,
            StudentDriveFolderRules.BuildFolderName(student.StudentCode, student.FullName),
            cancellationToken);
        try
        {
            AddAudit(actor, "Student.DriveFolderRenamed", student.Id, null,
                new { folderPresent = true, student.Version });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await studentDriveFolderService.RenameFolderAsync(
                    student.DriveFolderId, previousFolder.Name, cancellationToken);
            }
            catch (Exception exception)
            {
                LogCompensationFailure(logger, exception.GetType().Name);
            }
            throw;
        }
        return new StudentDriveFolderActionResponse(student.Id, folder.Id, folder.Name, folder.WebViewLink);
    }

    public async Task<StudentDriveReconcileResponse> ReconcileDriveFoldersAsync(CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        var students = await dbContext.Students.AsNoTracking()
            .OrderBy(student => student.Id)
            .Select(student => new
            {
                student.Id,
                student.StudentCode,
                student.FullName,
                student.DriveFolderId,
                student.GroupId
            })
            .ToListAsync(cancellationToken);
        var created = 0;
        var reused = 0;
        var moved = 0;
        var unchanged = 0;
        var errors = new List<StudentDriveReconcileError>();

        foreach (var student in students)
        {
            StudentDriveMutation? mutation = null;
            try
            {
                var destination = await ResolveDestinationFolderIdAsync(student.GroupId, cancellationToken);
                mutation = await studentDriveFolderService.EnsureStudentFolderAsync(
                    student.StudentCode, student.FullName, student.DriveFolderId, destination, cancellationToken);
                if (!string.Equals(student.DriveFolderId, mutation.Folder.Id, StringComparison.Ordinal))
                {
                    await dbContext.Students.Where(candidate => candidate.Id == student.Id)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(candidate => candidate.DriveFolderId, mutation.Folder.Id), cancellationToken);
                }
                if (mutation.Created) created++;
                if (mutation.Reused) reused++;
                if (mutation.Moved) moved++;
                if (!mutation.Created && !mutation.Reused && !mutation.Moved) unchanged++;
            }
            catch (Exception exception)
            {
                if (mutation is not null) await CompensateAsync(mutation, cancellationToken);
                var code = exception is AppException appException && appException.Code is not null
                    ? appException.Code
                    : ProblemCodes.StudentDriveOperationFailed;
                errors.Add(new StudentDriveReconcileError(
                    student.Id, student.StudentCode, student.FullName, code, SafeReconcileMessage(code)));
            }
        }

        AddAudit(actor, "Student.DriveFoldersReconciled", Guid.Empty, null,
            new { processed = students.Count, created, reused, moved, unchanged, failed = errors.Count });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new StudentDriveReconcileResponse(
            students.Count, created, reused, moved, unchanged, errors.Count, errors);
    }

    private async Task<string> ResolveDestinationFolderIdAsync(Guid? groupId, CancellationToken cancellationToken)
    {
        if (groupId is null) return studentDriveFolderService.GetRequiredPoolFolderId();
        var destination = await dbContext.StudentGroups.AsNoTracking()
            .Where(group => group.Id == groupId)
            .Select(group => new
            {
                group.ResponsibleTeacherId,
                DriveFolderId = group.ResponsibleTeacher == null ? null : group.ResponsibleTeacher.DriveFolderId
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (destination?.ResponsibleTeacherId is null) return studentDriveFolderService.GetRequiredPoolFolderId();
        return destination.DriveFolderId ?? throw new ConflictException(
            "Giáo viên phụ trách chưa có thư mục Google Drive.",
            ProblemCodes.TeacherDriveFolderRequired);
    }

    private async Task CompensateAsync(StudentDriveMutation mutation, CancellationToken cancellationToken)
    {
        try
        {
            if (mutation.Created)
                await studentDriveFolderService.DeleteFolderIfEmptyAsync(mutation.Folder.Id, cancellationToken);
            else if (mutation.Moved)
                await studentDriveFolderService.RestoreParentsAsync(
                    mutation.Folder.Id, mutation.PreviousParentIds, cancellationToken);
        }
        catch (Exception exception)
        {
            LogCompensationFailure(logger, exception.GetType().Name);
        }
    }

    private async Task CompensateStudentSheetAsync(
        StudentSheetMutation? mutation,
        CancellationToken cancellationToken)
    {
        if (mutation is null) return;
        try
        {
            await studentGoogleSheetsService.RestoreAsync(mutation, cancellationToken);
        }
        catch (Exception exception)
        {
            LogStudentSheetCompensationFailure(logger, exception.GetType().Name);
        }
    }

    private static string SafeReconcileMessage(string code) => code switch
    {
        ProblemCodes.StudentDrivePoolNotConfigured => "Chưa cấu hình thư mục pool Google Drive.",
        ProblemCodes.StudentDriveFolderInvalid => "Thư mục Google Drive không hợp lệ hoặc không có quyền chỉnh sửa.",
        ProblemCodes.StudentDriveDuplicateFolder => "Có nhiều thư mục trùng tên trong pool Google Drive.",
        ProblemCodes.TeacherDriveFolderRequired => "Giáo viên phụ trách chưa có thư mục Google Drive.",
        _ => "Không thể đồng bộ thư mục Google Drive."
    };

    [LoggerMessage(EventId = 33, Level = LogLevel.Error, Message = "Student Drive compensation failed with {ExceptionType}.")]
    private static partial void LogCompensationFailure(ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 34, Level = LogLevel.Error, Message = "Student Google Sheets compensation failed with {ExceptionType}.")]
    private static partial void LogStudentSheetCompensationFailure(ILogger logger, string exceptionType);

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
