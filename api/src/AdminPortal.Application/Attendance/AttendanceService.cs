using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.Students;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AdminPortal.Application.Attendance;

public sealed class AttendanceService(
    IApplicationDbContext dbContext,
    ICurrentActor currentActor,
    IAttendancePersistence attendancePersistence,
    IBusinessDateProvider businessDateProvider,
    TimeProvider timeProvider) : IAttendanceService
{
    public async Task<AttendanceContextResponse> GetContextAsync(
        DateOnly attendanceDate,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        EnsureAttendanceRole(actor);
        var serverDate = businessDateProvider.Today;
        var teacher = actor.Role == UserRole.Teacher
            ? await GetCurrentTeacherAsync(actor.UserId, cancellationToken)
            : null;

        var groupsQuery = dbContext.StudentGroups.IgnoreQueryFilters().AsNoTracking();
        if (teacher is not null)
        {
            groupsQuery = groupsQuery.Where(x =>
                x.DeletedAt == null && x.Status == GroupStatus.Active && x.ResponsibleTeacherId == teacher.Id);
        }
        else
        {
            groupsQuery = groupsQuery.Where(x =>
                (x.DeletedAt == null && x.Status == GroupStatus.Active) ||
                x.AttendanceSheets.Any(sheet => sheet.AttendanceDate == attendanceDate));
        }

        var groups = await groupsQuery.OrderBy(x => x.Code).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var groupIds = groups.Select(x => x.Id).ToArray();
        var savedCounts = await dbContext.AttendanceSheets.AsNoTracking()
            .Where(x => x.AttendanceDate == attendanceDate && groupIds.Contains(x.GroupId))
            .Select(x => new { x.GroupId, Count = x.Records.Count })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count, cancellationToken);
        var weekdayMask = StudentScheduleRules.ToMask(attendanceDate.DayOfWeek);
        var rosterCounts = await dbContext.Students.AsNoTracking()
            .Where(x => weekdayMask != 0 && x.GroupId != null && groupIds.Contains(x.GroupId.Value) &&
                x.Status == StudentStatus.Active && (x.StudyWeekdayMask & weekdayMask) != 0)
            .GroupBy(x => x.GroupId!.Value)
            .Select(x => new { GroupId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count, cancellationToken);
        var policy = EvaluateDatePolicy(actor, teacher, attendanceDate, serverDate);
        return new AttendanceContextResponse(
            attendanceDate,
            serverDate,
            groups.Select(x => new AttendanceContextGroupResponse(
                x.Id, x.Code, x.Name,
                rosterCounts.GetValueOrDefault(x.Id, savedCounts.GetValueOrDefault(x.Id)))).ToList(),
            teacher?.AttendanceEditWindowDays,
            policy.CanEdit,
            policy.Reason);
    }

    public async Task<AttendanceDailyResponse> GetDailyAsync(
        DateOnly attendanceDate,
        Guid? groupId,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        EnsureAttendanceRole(actor);
        var teacher = actor.Role == UserRole.Teacher
            ? await GetCurrentTeacherAsync(actor.UserId, cancellationToken)
            : null;
        var resolvedGroupId = await ResolveGroupIdAsync(actor, teacher, groupId, cancellationToken);
        var group = await dbContext.StudentGroups.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.ResponsibleTeacher!).ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == resolvedGroupId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhóm học sinh.");
        EnsureGroupAccess(actor, teacher, group);

        var sheet = await dbContext.AttendanceSheets.AsNoTracking()
            .Include(x => x.Records.OrderBy(record => record.StudentFullNameSnapshot).ThenBy(record => record.StudentCodeSnapshot))
            .SingleOrDefaultAsync(x => x.GroupId == group.Id && x.AttendanceDate == attendanceDate, cancellationToken);
        return sheet is null
            ? await BuildMissingAsync(actor, teacher, group, attendanceDate, cancellationToken)
            : BuildSaved(actor, teacher, group, sheet);
    }

    public Task<AttendanceDailyResponse> CreateAsync(
        CreateAttendanceSheetRequest request,
        CancellationToken cancellationToken) => CreateSheetAsync(request, cancellationToken);

    public Task<AttendanceDailyResponse> UpdateAsync(
        Guid sheetId,
        UpdateAttendanceSheetRequest request,
        CancellationToken cancellationToken) => UpdateSheetAsync(sheetId, request, cancellationToken);

    public Task<AttendanceDailyResponse> RecoverAsync(
        HistoricalRecoveryRequest request,
        CancellationToken cancellationToken) => RecoverSheetAsync(request, cancellationToken);

    public Task<PagedResponse<HistoricalGroupCandidateResponse>> ListGroupCandidatesAsync(
        CandidateListQuery query,
        CancellationToken cancellationToken) => ListRecoveryGroupCandidatesAsync(query, cancellationToken);

    public Task<PagedResponse<HistoricalStudentCandidateResponse>> ListStudentCandidatesAsync(
        CandidateListQuery query,
        CancellationToken cancellationToken) => ListRecoveryStudentCandidatesAsync(query, cancellationToken);

    public Task<PagedResponse<HistoricalTeacherCandidateResponse>> ListTeacherCandidatesAsync(
        CandidateListQuery query,
        CancellationToken cancellationToken) => ListRecoveryTeacherCandidatesAsync(query, cancellationToken);

    private async Task<AttendanceDailyResponse> BuildMissingAsync(
        ActorContext actor,
        Teacher? teacher,
        StudentGroup group,
        DateOnly attendanceDate,
        CancellationToken cancellationToken)
    {
        var roster = await ScheduledRoster(group.Id, attendanceDate)
            .OrderBy(x => x.FullName).ThenBy(x => x.StudentCode)
            .ToListAsync(cancellationToken);
        var datePolicy = EvaluateDatePolicy(actor, teacher, attendanceDate, businessDateProvider.Today);
        var standardAvailable = IsStandardSnapshotAvailable(group, attendanceDate);
        var standardReason = StandardReadOnlyReason(group, attendanceDate, datePolicy.Reason);
        var reason = standardReason ?? (roster.Count == 0 ? AttendanceReadOnlyReason.NoScheduledStudents : null);
        var canCreate = reason is null;
        var canRecover = actor.Role is UserRole.Admin or UserRole.SuperAdmin &&
            attendanceDate < businessDateProvider.Today && !standardAvailable;
        var items = roster.Select(x => new AttendanceItemResponse(
            null, x.Id, x.StudentCode, x.FullName, x.NickName,
            DefaultStatus(x.StudyMode), null, null, DefaultDuration(x.StudyMode), null, null)).ToList();
        return new AttendanceDailyResponse(
            attendanceDate,
            businessDateProvider.Today,
            new AttendanceGroupResponse(group.Id, group.Code, group.Name),
            AttendanceSheetState.Missing,
            null, null, null,
            group.SnapshotVersion,
            null,
            canCreate,
            false,
            canRecover,
            reason,
            BuildSummary(items),
            items);
    }

    private AttendanceDailyResponse BuildSaved(
        ActorContext actor,
        Teacher? teacher,
        StudentGroup group,
        AttendanceSheet sheet)
    {
        var datePolicy = EvaluateDatePolicy(actor, teacher, sheet.AttendanceDate, businessDateProvider.Today);
        var items = sheet.Records.OrderBy(x => x.StudentFullNameSnapshot).ThenBy(x => x.StudentCodeSnapshot)
            .Select(x => new AttendanceItemResponse(
            x.Id, x.StudentId, x.StudentCodeSnapshot, x.StudentFullNameSnapshot, x.StudentNickNameSnapshot,
            x.Status, x.HalfDayPart, x.IsExcused, x.DurationMinutes, x.Notes, x.UpdatedAt)).ToList();
        return new AttendanceDailyResponse(
            sheet.AttendanceDate,
            businessDateProvider.Today,
            new AttendanceGroupResponse(sheet.GroupId, sheet.GroupCodeSnapshot, sheet.GroupNameSnapshot),
            AttendanceSheetState.Saved,
            sheet.Id,
            sheet.Version,
            sheet.SnapshotSource,
            null,
            sheet.SourceSnapshotVersion,
            false,
            datePolicy.CanEdit,
            false,
            datePolicy.Reason,
            BuildSummary(items),
            items);
    }

    private bool IsStandardSnapshotAvailable(StudentGroup group, DateOnly attendanceDate) =>
        group.DeletedAt is null && group.Status == GroupStatus.Active && group.ResponsibleTeacherId is not null &&
        (attendanceDate >= businessDateProvider.Today || group.SnapshotChangedAt < businessDateProvider.EndOfDayUtc(attendanceDate));

    private AttendanceReadOnlyReason? StandardReadOnlyReason(
        StudentGroup group,
        DateOnly attendanceDate,
        AttendanceReadOnlyReason? dateReason)
    {
        if (dateReason is not null) return dateReason;
        if (group.DeletedAt is not null || group.Status != GroupStatus.Active) return AttendanceReadOnlyReason.GroupInactive;
        if (group.ResponsibleTeacherId is null) return AttendanceReadOnlyReason.ResponsibleTeacherRequired;
        if (attendanceDate < businessDateProvider.Today &&
            group.SnapshotChangedAt >= businessDateProvider.EndOfDayUtc(attendanceDate))
            return AttendanceReadOnlyReason.HistoricalSnapshotUnavailable;
        return null;
    }

    private static AttendanceSummaryResponse BuildSummary(List<AttendanceItemResponse> items) => new(
        items.Count,
        items.Count(x => x.Status == AttendanceStatus.Present),
        items.Count(x => x.Status is AttendanceStatus.AbsentFullDay or AttendanceStatus.AbsentHalfDay),
        items.Count(x => x.Status == AttendanceStatus.OneToOneHour),
        items.Count(x => x.Status == AttendanceStatus.Unmarked));

    private IQueryable<Student> ScheduledRoster(Guid groupId, DateOnly attendanceDate)
    {
        var weekdayMask = StudentScheduleRules.ToMask(attendanceDate.DayOfWeek);
        return dbContext.Students.AsNoTracking().Where(student =>
            weekdayMask != 0 && student.GroupId == groupId && student.Status == StudentStatus.Active &&
            (student.StudyWeekdayMask & weekdayMask) != 0);
    }

    private static AttendanceStatus DefaultStatus(StudyMode mode) => mode switch
    {
        StudyMode.FullDay => AttendanceStatus.Present,
        StudyMode.OneToOne => AttendanceStatus.OneToOneHour,
        _ => throw new InvalidOperationException("Stored study mode is invalid.")
    };

    private static int? DefaultDuration(StudyMode mode) => mode == StudyMode.OneToOne ? 60 : null;

    private async Task<Guid> ResolveGroupIdAsync(
        ActorContext actor,
        Teacher? teacher,
        Guid? groupId,
        CancellationToken cancellationToken)
    {
        if (groupId is not null) return groupId.Value;
        if (actor.Role != UserRole.Teacher || teacher is null)
            throw Validation("groupId", "groupId là bắt buộc.");
        var ids = await dbContext.StudentGroups.AsNoTracking()
            .Where(x => x.ResponsibleTeacherId == teacher.Id && x.Status == GroupStatus.Active)
            .Select(x => x.Id).Take(2).ToListAsync(cancellationToken);
        return ids.Count switch
        {
            1 => ids[0],
            0 => throw new NotFoundException("Giáo viên chưa được phân công nhóm."),
            _ => throw Validation("groupId", "groupId là bắt buộc khi giáo viên phụ trách nhiều nhóm.")
        };
    }

    private static void EnsureGroupAccess(ActorContext actor, Teacher? teacher, StudentGroup group)
    {
        if (actor.Role == UserRole.Teacher && (teacher is null || group.ResponsibleTeacherId != teacher.Id))
            throw new ForbiddenException("Giáo viên không phụ trách nhóm này.");
    }

    private async Task<Teacher> GetCurrentTeacherAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Teachers.AsNoTracking().SingleOrDefaultAsync(
            x => x.UserId == userId && x.User.Role == UserRole.Teacher,
            cancellationToken)
        ?? throw new ForbiddenException("Tài khoản chưa có hồ sơ giáo viên.");

    private static (bool CanEdit, AttendanceReadOnlyReason? Reason) EvaluateDatePolicy(
        ActorContext actor,
        Teacher? teacher,
        DateOnly attendanceDate,
        DateOnly serverDate)
    {
        if (attendanceDate > serverDate) return (false, AttendanceReadOnlyReason.FutureDate);
        if (actor.Role != UserRole.Teacher) return (true, null);
        return AttendanceRules.CanTeacherEdit(attendanceDate, serverDate, teacher!.AttendanceEditWindowDays)
            ? (true, null)
            : (false, AttendanceReadOnlyReason.AttendanceEditWindowExceeded);
    }

    private static void EnsureAttendanceRole(ActorContext actor)
    {
        if (actor.Role is not (UserRole.SuperAdmin or UserRole.Admin or UserRole.Teacher))
            throw new ForbiddenException("Không đủ quyền điểm danh.");
    }

    private static AppValidationException Validation(string field, string message) =>
        new(message, new Dictionary<string, string[]> { [field] = [message] });

    private async Task<AttendanceDailyResponse> CreateSheetAsync(
        CreateAttendanceSheetRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        EnsureAttendanceRole(actor);
        AttendanceRules.ValidateRecords(request.Records, allowEmpty: true);
        EnsureNotFuture(request.Date);
        var teacher = actor.Role == UserRole.Teacher
            ? await GetCurrentTeacherAsync(actor.UserId, cancellationToken)
            : null;
        EnsureTeacherWindow(actor, teacher, request.Date);

        await using var transaction = await attendancePersistence.BeginTransactionAsync(cancellationToken);
        await attendancePersistence.LockGroupsAsync([request.GroupId], cancellationToken);
        var group = await dbContext.StudentGroups.AsNoTracking()
            .Include(x => x.ResponsibleTeacher!).ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == request.GroupId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhóm học sinh.");
        EnsureGroupAccess(actor, teacher, group);
        if (request.ExpectedSnapshotVersion != group.SnapshotVersion)
            throw ConflictWithVersion("Snapshot nhóm đã thay đổi.", ProblemCodes.SnapshotChanged,
                "currentSnapshotVersion", group.SnapshotVersion);
        EnsureStandardCreationAvailable(group, request.Date);
        if (await dbContext.AttendanceSheets.AnyAsync(
            x => x.GroupId == group.Id && x.AttendanceDate == request.Date, cancellationToken))
            throw new ConflictException("Phiếu điểm danh đã tồn tại.", ProblemCodes.AttendanceSheetAlreadyExists);

        var roster = await ScheduledRoster(group.Id, request.Date)
            .OrderBy(x => x.Id).ToListAsync(cancellationToken);
        if (roster.Count == 0)
            throw new ConflictException("Không có học sinh có lịch học trong ngày này.", ProblemCodes.NoScheduledStudents);
        EnsureRosterMatches(request.Records, roster.Select(x => x.Id));
        var now = timeProvider.GetUtcNow();
        var sheet = new AttendanceSheet
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            AttendanceDate = request.Date,
            GroupCodeSnapshot = group.Code,
            GroupNameSnapshot = group.Name,
            ResponsibleTeacherIdSnapshot = group.ResponsibleTeacherId!.Value,
            ResponsibleTeacherNameSnapshot = group.ResponsibleTeacher!.User.FullName,
            SnapshotSource = AttendanceSnapshotSource.CurrentSnapshot,
            SourceSnapshotVersion = group.SnapshotVersion,
            Version = 1,
            CreatedByUserId = actor.UserId,
            UpdatedByUserId = actor.UserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        var requests = request.Records.ToDictionary(x => x.StudentId);
        foreach (var student in roster)
        {
            var item = requests[student.Id];
            sheet.Records.Add(CreateRecord(sheet, student, item, actor.UserId, now));
        }
        dbContext.AttendanceSheets.Add(sheet);
        AddAttendanceAudit(actor, "Attendance.SheetCreated", sheet, null,
            AuditSnapshot(sheet, request.Records.Any(x => !string.IsNullOrWhiteSpace(x.Notes))));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new ConflictException(
                "Phiếu điểm danh đã tồn tại hoặc học sinh đã có điểm danh trong ngày.",
                ProblemCodes.AttendanceSheetAlreadyExists,
                new Dictionary<string, object?> { ["databaseConstraint"] = exception.InnerException?.Message is not null });
        }
        return BuildSaved(actor, teacher, group, sheet);
    }

    private async Task<AttendanceDailyResponse> UpdateSheetAsync(
        Guid sheetId,
        UpdateAttendanceSheetRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        EnsureAttendanceRole(actor);
        AttendanceRules.ValidateRecords(request.Records);
        await using var transaction = await attendancePersistence.BeginTransactionAsync(cancellationToken);
        await attendancePersistence.LockSheetAsync(sheetId, cancellationToken);
        var sheet = await dbContext.AttendanceSheets.Include(x => x.Records)
            .SingleOrDefaultAsync(x => x.Id == sheetId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phiếu điểm danh.");
        var group = await dbContext.StudentGroups.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == sheet.GroupId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhóm của phiếu điểm danh.");
        var teacher = actor.Role == UserRole.Teacher
            ? await GetCurrentTeacherAsync(actor.UserId, cancellationToken)
            : null;
        EnsureGroupAccess(actor, teacher, group);
        EnsureNotFuture(sheet.AttendanceDate);
        EnsureTeacherWindow(actor, teacher, sheet.AttendanceDate);
        if (sheet.Version != request.ExpectedVersion)
            throw ConflictWithVersion("Phiên bản phiếu đã thay đổi.", ProblemCodes.SheetVersionConflict,
                "currentVersion", sheet.Version);
        EnsureRosterMatches(request.Records, sheet.Records.Select(x => x.StudentId));
        var oldAudit = AuditSnapshot(sheet, false);
        var byStudent = request.Records.ToDictionary(x => x.StudentId);
        var now = timeProvider.GetUtcNow();
        var notesChanged = false;
        foreach (var record in sheet.Records)
        {
            var item = byStudent[record.StudentId];
            var notes = NormalizeOptional(item.Notes);
            notesChanged |= !string.Equals(notes, record.Notes, StringComparison.Ordinal);
            var preservedLegacyHalfDayPart =
                record.Status == AttendanceStatus.AbsentHalfDay && item.Status == AttendanceStatus.AbsentHalfDay
                    ? record.HalfDayPart
                    : null;
            ApplyRecord(record, item, actor.UserId, now, notes, preservedLegacyHalfDayPart);
        }
        sheet.Version++;
        sheet.UpdatedByUserId = actor.UserId;
        sheet.UpdatedAt = now;
        AddAttendanceAudit(actor, "Attendance.SheetUpdated", sheet, oldAudit, AuditSnapshot(sheet, notesChanged));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Phiên bản phiếu đã thay đổi.", ProblemCodes.SheetVersionConflict);
        }
        return BuildSaved(actor, teacher, group, sheet);
    }

    private async Task<AttendanceDailyResponse> RecoverSheetAsync(
        HistoricalRecoveryRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        AttendanceRules.ValidateRecords(request.Records);
        if (request.Date >= businessDateProvider.Today)
            throw Validation("date", "Recovery chỉ áp dụng cho ngày quá khứ.");
        if (!request.AcknowledgeHistoricalSnapshot)
            throw Validation("acknowledgeHistoricalSnapshot", "Phải xác nhận snapshot lịch sử.");
        var reason = NormalizeRequired(request.RecoveryReason, "recoveryReason", "Lý do recovery là bắt buộc.");
        if (reason.Length > 500) throw Validation("recoveryReason", "Lý do recovery tối đa 500 ký tự.");

        await using var transaction = await attendancePersistence.BeginTransactionAsync(cancellationToken);
        await attendancePersistence.LockGroupsAsync([request.GroupId], cancellationToken);
        var group = await dbContext.StudentGroups.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.GroupId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhóm học sinh.");
        if (IsStandardSnapshotAvailable(group, request.Date))
            throw new ConflictException("Snapshot hiện tại vẫn đủ điều kiện tạo phiếu chuẩn.", ProblemCodes.HistoricalRecoveryNotAllowed);

        var teacher = await dbContext.Teachers.IgnoreQueryFilters().AsNoTracking().Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == request.ResponsibleTeacherId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy hồ sơ giáo viên.");
        

        var existingSheet = await dbContext.AttendanceSheets
            .Include(x => x.Records)
            .SingleOrDefaultAsync(x => x.GroupId == group.Id && x.AttendanceDate == request.Date, cancellationToken);

        var existingRecordRequests = new List<AttendanceRecordRequest>();
        if (existingSheet != null)
        {
            if (existingSheet.Records.Count > 0)
            {
                existingRecordRequests = existingSheet.Records.Select(x => new AttendanceRecordRequest(
               x.StudentId, x.Status, x.HalfDayPart, x.IsExcused, x.DurationMinutes, x.Notes)).ToList();
                dbContext.AttendanceRecords.RemoveRange(existingSheet.Records);
            }
            dbContext.AttendanceSheets.Remove(existingSheet);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        var sheet = new AttendanceSheet
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            AttendanceDate = request.Date,
            GroupCodeSnapshot = group.Code,
            GroupNameSnapshot = group.Name,
            ResponsibleTeacherIdSnapshot = teacher.Id,
            ResponsibleTeacherNameSnapshot = teacher.User.FullName,
            SnapshotSource = AttendanceSnapshotSource.HistoricalRecovery,
            HistoricalRecoveryReason = reason,
            Version = 1,
            CreatedByUserId = actor.UserId,
            UpdatedByUserId = actor.UserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        var requests = request.Records.ToDictionary(x => x.StudentId);
        var studentIds = request.Records.Select(x => x.StudentId).ToList();
        if (existingRecordRequests.Count > 0)
        {
            foreach (var record in existingRecordRequests)
            {
                requests.TryAdd(record.StudentId, record);
                studentIds.Add(record.StudentId);
            }
        }
        var students = await dbContext.Students.IgnoreQueryFilters().AsNoTracking()
            .Where(x => studentIds.Contains(x.Id)).ToListAsync(cancellationToken);
        if (students.Count != studentIds.Distinct().Count())
            throw new AppValidationException("Danh sách học sinh recovery không hợp lệ.",
                new Dictionary<string, string[]> { ["records"] = ["Có học sinh không tồn tại hoặc bị trùng."] });

        foreach (var student in students.OrderBy(x => x.Id))
            sheet.Records.Add(CreateRecord(sheet, student, requests[student.Id], actor.UserId, now));
        dbContext.AttendanceSheets.Add(sheet);
        AddAttendanceAudit(actor, "Attendance.HistoricalSheetRecovered", sheet, null,
            new
            {
                attendance = AuditSnapshot(sheet, request.Records.Any(x => x.Notes is not null)),
                recoveryReason = reason
            });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("Phiếu điểm danh đã tồn tại.", ProblemCodes.AttendanceSheetAlreadyExists);
        }
        return BuildSaved(actor, null, group, sheet);
    }

    private async Task<PagedResponse<HistoricalGroupCandidateResponse>> ListRecoveryGroupCandidatesAsync(
        CandidateListQuery query,
        CancellationToken cancellationToken)
    {
        AuthorizationRules.EnsurePortalManager(currentActor.GetRequired());
        var candidates = dbContext.StudentGroups.IgnoreQueryFilters().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862
            candidates = candidates.Where(x => x.Code.ToLower().Contains(search) || x.Name.ToLower().Contains(search));
#pragma warning restore CA1304, CA1311, CA1862
        }
        var total = await candidates.CountAsync(cancellationToken);
        var items = await candidates.OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new HistoricalGroupCandidateResponse(x.Id, x.Code, x.Name, x.Status, x.DeletedAt != null))
            .ToListAsync(cancellationToken);
        return Page(items, query, total);
    }

    private async Task<PagedResponse<HistoricalStudentCandidateResponse>> ListRecoveryStudentCandidatesAsync(
        CandidateListQuery query,
        CancellationToken cancellationToken)
    {
        AuthorizationRules.EnsurePortalManager(currentActor.GetRequired());
        var candidates = dbContext.Students.IgnoreQueryFilters().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862
            candidates = candidates.Where(x => x.StudentCode.ToLower().Contains(search) ||
                x.FullName.ToLower().Contains(search) || x.NickName.ToLower().Contains(search));
#pragma warning restore CA1304, CA1311, CA1862
        }
        var total = await candidates.CountAsync(cancellationToken);
        var items = await candidates.OrderBy(x => x.FullName).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new HistoricalStudentCandidateResponse(
                x.Id,
                x.StudentCode,
                x.FullName,
                x.NickName,
                x.Group == null ? null : x.Group.Code,
                x.Group == null ? null : x.Group.Name,
                x.Group == null || x.Group.ResponsibleTeacher == null ? null : x.Group.ResponsibleTeacher.User.FullName,
                x.Status,
                x.DeletedAt != null,
                x.GroupId))
            .ToListAsync(cancellationToken);
        return Page(items, query, total);
    }

    private async Task<PagedResponse<HistoricalTeacherCandidateResponse>> ListRecoveryTeacherCandidatesAsync(
        CandidateListQuery query,
        CancellationToken cancellationToken)
    {
        AuthorizationRules.EnsurePortalManager(currentActor.GetRequired());
        var candidates = dbContext.Teachers.IgnoreQueryFilters().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862
            candidates = candidates.Where(x => x.User.FullName.ToLower().Contains(search));
#pragma warning restore CA1304, CA1311, CA1862
        }
        var total = await candidates.CountAsync(cancellationToken);
        var items = await candidates.OrderBy(x => x.User.FullName).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new HistoricalTeacherCandidateResponse(
                x.Id, x.UserId, x.User.FullName, x.User.Status, x.User.DeletedAt != null,
                x.User.Role == UserRole.Teacher))
            .ToListAsync(cancellationToken);
        return Page(items, query, total);
    }

    private void EnsureStandardCreationAvailable(StudentGroup group, DateOnly attendanceDate)
    {
        if (group.Status != GroupStatus.Active)
            throw new ConflictException("Nhóm không hoạt động.", ProblemCodes.GroupInactive);
        if (group.ResponsibleTeacherId is null)
            throw new ConflictException("Nhóm chưa có giáo viên phụ trách.", ProblemCodes.ResponsibleTeacherRequired);
        if (attendanceDate < businessDateProvider.Today &&
            group.SnapshotChangedAt >= businessDateProvider.EndOfDayUtc(attendanceDate))
            throw new ConflictException("Không thể xác minh snapshot lịch sử.", ProblemCodes.HistoricalSnapshotUnavailable);
    }

    private void EnsureNotFuture(DateOnly attendanceDate)
    {
        if (attendanceDate > businessDateProvider.Today)
            throw new ConflictException("Không thể điểm danh ngày tương lai.", ProblemCodes.FutureAttendanceDate);
    }

    private void EnsureTeacherWindow(ActorContext actor, Teacher? teacher, DateOnly attendanceDate)
    {
        if (actor.Role != UserRole.Teacher) return;
        if (!AttendanceRules.CanTeacherEdit(
            attendanceDate, businessDateProvider.Today, teacher!.AttendanceEditWindowDays))
            throw new ForbiddenException("Đã quá cửa sổ sửa điểm danh.", ProblemCodes.AttendanceEditWindowExceeded);
    }

    private static void EnsureRosterMatches(
        IReadOnlyList<AttendanceRecordRequest> records,
        IEnumerable<Guid> expectedStudentIds)
    {
        var actual = records.Select(x => x.StudentId).Order().ToArray();
        var expected = expectedStudentIds.Order().ToArray();
        if (!actual.SequenceEqual(expected))
            throw new ConflictException("Danh sách học sinh không khớp roster.", ProblemCodes.AttendanceRosterMismatch);
    }

    private static AttendanceRecord CreateRecord(
        AttendanceSheet sheet,
        Student student,
        AttendanceRecordRequest request,
        Guid actorUserId,
        DateTimeOffset now)
    {
        var record = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            SheetId = sheet.Id,
            Sheet = sheet,
            AttendanceDate = sheet.AttendanceDate,
            StudentId = student.Id,
            StudentCodeSnapshot = student.StudentCode,
            StudentFullNameSnapshot = student.FullName,
            StudentNickNameSnapshot = student.NickName,
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedByUserId = actorUserId
        };
        ApplyRecord(record, request, actorUserId, now, NormalizeOptional(request.Notes), null);
        return record;
    }

    private static void ApplyRecord(
        AttendanceRecord record,
        AttendanceRecordRequest request,
        Guid actorUserId,
        DateTimeOffset now,
        string? notes,
        HalfDayPart? halfDayPart)
    {
        record.Status = request.Status;
        record.HalfDayPart = halfDayPart;
        record.IsExcused = request.IsExcused;
        record.DurationMinutes = request.DurationMinutes;
        record.Notes = notes;
        record.UpdatedByUserId = actorUserId;
        record.UpdatedAt = now;
    }

    private void AddAttendanceAudit(
        ActorContext actor,
        string action,
        AttendanceSheet sheet,
        object? oldValue,
        object? newValue) => dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor.UserId,
            Action = action,
            EntityType = "AttendanceSheet",
            EntityId = sheet.Id,
            OldValues = oldValue is null ? null : JsonSerializer.Serialize(oldValue),
            NewValues = newValue is null ? null : JsonSerializer.Serialize(newValue),
            IpAddress = actor.IpAddress,
            CreatedAt = timeProvider.GetUtcNow()
        });

    private static object AuditSnapshot(AttendanceSheet sheet, bool notesChanged) => new
    {
        sheet.GroupId,
        sheet.AttendanceDate,
        sheet.Version,
        SnapshotSource = sheet.SnapshotSource.ToString(),
        rosterTotal = sheet.Records.Count,
        present = sheet.Records.Count(x => x.Status == AttendanceStatus.Present),
        absent = sheet.Records.Count(x => x.Status is AttendanceStatus.AbsentFullDay or AttendanceStatus.AbsentHalfDay),
        oneToOne = sheet.Records.Count(x => x.Status == AttendanceStatus.OneToOneHour),
        unmarked = sheet.Records.Count(x => x.Status == AttendanceStatus.Unmarked),
        records = sheet.Records.Select(x => new
        {
            x.StudentId,
            status = x.Status.ToString(),
            halfDayPart = x.HalfDayPart?.ToString(),
            x.IsExcused,
            x.DurationMinutes
        }),
        notesChanged
    };

    private static ConflictException ConflictWithVersion(
        string message, string code, string key, int value) =>
        new(message, code, new Dictionary<string, object?> { [key] = value });

    private static PagedResponse<T> Page<T>(List<T> items, CandidateListQuery query, int total) =>
        new(items, new PaginationMetadata(query.Page, query.PageSize, total,
            (int)Math.Ceiling(total / (double)query.PageSize)));

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeRequired(string value, string field, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw Validation(field, message);
        return value.Trim();
    }
}
