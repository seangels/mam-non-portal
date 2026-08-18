using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.Users;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AdminPortal.Application.Assessments;

public interface IAssessmentService : IGenericService<Assessment, CreateAssessmentRequest, UpdateAssessmentRequest, AssessmentListQuery, AssessmentListItemResponse, AssessmentDetailResponse>
{
}

public sealed partial class AssessmentService(
    IApplicationDbContext dbContext,
    UserAccountCoordinator userAccountCoordinator,
    ICurrentActor currentActor,
    IAttendancePersistence attendancePersistence,
    IDatabaseExceptionClassifier databaseExceptionClassifier,
    TimeProvider timeProvider,
    ILogger<AssessmentService> logger) : IAssessmentService
{
    private const string TeacherCodeIndex = "ix_teachers_teacher_code";
    private const string UserEmailIndex = "ix_users_normalized_email";

    public async Task<PagedResponse<AssessmentListItemResponse>> ListAsync(
        AssessmentListQuery query,
        CancellationToken cancellationToken)
    {
        AuthorizationRules.EnsurePortalManager(currentActor.GetRequired());
        if (query.GroupId is not null && query.Unassigned == true)
        {
            throw new AppValidationException(
                "Không thể kết hợp nhóm phụ trách với bộ lọc chưa phân công.",
                new Dictionary<string, string[]>
                {
                    ["unassigned"] = ["Bỏ bộ lọc chưa phân công khi đã chọn nhóm phụ trách."]
                });
        }

        var teachers = QueryCurrent();
        if (query.Status is not null) teachers = teachers.Where(x => x.User.Status == query.Status);
        if (query.GroupId is not null)
            teachers = teachers.Where(x => x.ResponsibleGroups.Any(group => group.Id == query.GroupId));
        if (query.Unassigned == true) teachers = teachers.Where(x => !x.ResponsibleGroups.Any());

        var descending = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var ordered = ApplySort(teachers, query.SortBy, descending);
        if (string.IsNullOrWhiteSpace(query.Search))
        {
            var totalItems = await teachers.CountAsync(cancellationToken);
            var items = await ProjectList(ordered)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
            return CreatePage(items, query, totalItems);
        }

        var startedAt = Stopwatch.GetTimestamp();
        var candidates = await ProjectList(ordered).ToListAsync(cancellationToken);
        var foldedSearch = VietnameseSearchNormalizer.Fold(query.Search);
        var searchDigits = VietnameseSearchNormalizer.Digits(query.Search);
        var matches = candidates.Where(candidate => Matches(candidate, foldedSearch, searchDigits)).ToList();
        var pageItems = matches
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();
        if (logger.IsEnabled(LogLevel.Information))
        {
            var durationMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            LogAccentSearch(
                logger,
                candidates.Count,
                matches.Count,
                durationMs);
        }
        return CreatePage(pageItems, query, matches.Count);
    }

    public async Task<AssessmentDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        AuthorizationRules.EnsurePortalManager(currentActor.GetRequired());
        return await ProjectDetail(QueryCurrent().Where(x => x.Id == id))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw TeacherNotFound();
    }

    public async Task<AssessmentDetailResponse> CreateAsync(
        CreateAssessmentRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        var teacherCode = NormalizeTeacherCode(request.TeacherCode);
        var note = NormalizeOptional(request.Note);
        if (await dbContext.Teachers.AnyAsync(x => x.TeacherCode == teacherCode, cancellationToken))
        {
            throw TeacherCodeConflict();
        }

        var now = timeProvider.GetUtcNow();
        await using var transaction = await attendancePersistence.BeginTransactionAsync(cancellationToken);
        var user = await userAccountCoordinator.CreateAsync(
            request.Email,
            request.FullName,
            request.PhoneNumber,
            UserRole.Teacher,
            request.Status,
            request.Password,
            now,
            cancellationToken);
        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TeacherCode = teacherCode,
            Note = note,
            AttendanceEditWindowDays = 7,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Teachers.Add(teacher);
        AddAudit(actor, "Teacher.Created", teacher, null, AuditSnapshot(teacher));
        await SaveTeacherChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(teacher.Id, cancellationToken);
    }

    public async Task<AssessmentDetailResponse> UpdateAsync(
        Guid id,
        UpdateAssessmentRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        await using var transaction = await attendancePersistence.BeginTransactionAsync(cancellationToken);
        var teacher = await LockAndFindRequiredAsync(id, cancellationToken);
        EnsureExpectedVersion(teacher, request.ExpectedVersion);

        var teacherCode = NormalizeTeacherCode(request.TeacherCode);
        var note = NormalizeOptional(request.Note);
        if (teacherCode != teacher.TeacherCode &&
            await dbContext.Teachers.AnyAsync(x => x.TeacherCode == teacherCode && x.Id != id, cancellationToken))
        {
            throw TeacherCodeConflict();
        }

        var now = timeProvider.GetUtcNow();
        var before = CaptureState(teacher);
        var nameChanged = await userAccountCoordinator.UpdateAsync(
            teacher.User,
            request.Email,
            request.FullName,
            request.PhoneNumber,
            request.Status,
            now,
            cancellationToken);

        var affectedGroupIds = nameChanged
            ? await dbContext.StudentGroups
                .Where(group => group.ResponsibleTeacherId == teacher.Id)
                .Select(group => group.Id)
                .ToListAsync(cancellationToken)
            : [];
        await attendancePersistence.LockGroupsAsync(affectedGroupIds, cancellationToken);
        if (affectedGroupIds.Count > 0)
        {
            var groups = await dbContext.StudentGroups
                .Where(group => affectedGroupIds.Contains(group.Id) && group.ResponsibleTeacherId == teacher.Id)
                .ToListAsync(cancellationToken);
            foreach (var group in groups)
            {
                group.SnapshotVersion++;
                group.SnapshotChangedAt = now;
                group.UpdatedAt = now;
            }
        }

        teacher.TeacherCode = teacherCode;
        teacher.Note = note;
        teacher.Version++;
        teacher.UpdatedAt = now;
        var noteChanged = !string.Equals(before.Note, note, StringComparison.Ordinal);
        var changedFields = ChangedFields(before, teacher, noteChanged);
        AddAudit(actor, "Teacher.Updated", teacher,
            AuditSnapshot(before),
            new
            {
                values = AuditSnapshot(teacher),
                changedFields,
                noteChanged,
                statusBefore = before.Status.ToString(),
                statusAfter = teacher.User.Status.ToString(),
                versionBefore = before.Version,
                versionAfter = teacher.Version
            });
        await SaveTeacherChangesAsync(cancellationToken, teacher.Id);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(teacher.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, int expectedVersion, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AuthorizationRules.EnsurePortalManager(actor);
        await using var transaction = await attendancePersistence.BeginTransactionAsync(cancellationToken);
        var teacher = await LockAndFindRequiredAsync(id, cancellationToken);
        EnsureExpectedVersion(teacher, expectedVersion);
        if (await dbContext.StudentGroups.AnyAsync(
                group => group.ResponsibleTeacherId == teacher.Id,
                cancellationToken))
        {
            throw new ConflictException(
                "Không thể xóa giáo viên đang phụ trách nhóm.",
                ProblemCodes.TeacherHasResponsibleGroups);
        }

        var now = timeProvider.GetUtcNow();
        var before = CaptureState(teacher);
        await userAccountCoordinator.SoftDeleteAsync(teacher.User, now, cancellationToken);
        teacher.Version++;
        teacher.UpdatedAt = now;
        AddAudit(actor, "Teacher.Deleted", teacher, AuditSnapshot(before),
            new { deleted = true, teacher.UserId, teacher.Version });
        await SaveTeacherChangesAsync(cancellationToken, teacher.Id);
        await transaction.CommitAsync(cancellationToken);
    }

    private IQueryable<Teacher> QueryCurrent() => dbContext.Teachers.AsNoTracking()
        .Where(x => x.User.Role == UserRole.Teacher);

    private async Task<Teacher> LockAndFindRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var identity = await QueryCurrent().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.UserId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw TeacherNotFound();
        await attendancePersistence.LockTeachersAsync([identity.Id], cancellationToken);
        await attendancePersistence.LockUsersAsync([identity.UserId], cancellationToken);
        return await dbContext.Teachers.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == id && x.User.Role == UserRole.Teacher, cancellationToken)
            ?? throw TeacherNotFound();
    }

    private async Task SaveTeacherChangesAsync(
        CancellationToken cancellationToken,
        Guid? teacherId = null)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) when (teacherId is not null)
        {
            throw await VersionConflictAsync(teacherId.Value, cancellationToken);
        }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsUniqueViolation(exception, TeacherCodeIndex))
        {
            throw TeacherCodeConflict();
        }
        catch (DbUpdateException exception) when (
            databaseExceptionClassifier.IsUniqueViolation(exception, UserEmailIndex))
        {
            throw new ConflictException("Email đã được sử dụng.", ProblemCodes.EmailAlreadyExists);
        }
    }

    private async Task<ConflictException> VersionConflictAsync(Guid teacherId, CancellationToken cancellationToken)
    {
        var currentVersion = await dbContext.Teachers.AsNoTracking()
            .Where(x => x.Id == teacherId)
            .Select(x => (int?)x.Version)
            .SingleOrDefaultAsync(cancellationToken);
        return VersionConflict(currentVersion);
    }

    private static void EnsureExpectedVersion(Teacher teacher, int expectedVersion)
    {
        if (teacher.Version != expectedVersion)
        {
            throw VersionConflict(teacher.Version);
        }
    }

    private static ConflictException VersionConflict(int? currentVersion) =>
        new(
            "Dữ liệu giáo viên đã thay đổi. Vui lòng tải lại.",
            ProblemCodes.TeacherVersionConflict,
            new Dictionary<string, object?> { ["currentVersion"] = currentVersion });

    private static ConflictException TeacherCodeConflict() =>
        new("Mã giáo viên đã được sử dụng.", ProblemCodes.TeacherCodeAlreadyExists);

    private static NotFoundException TeacherNotFound() =>
        new("Không tìm thấy giáo viên.", ProblemCodes.TeacherNotFound);

    private void AddAudit(
        ActorContext actor,
        string action,
        Teacher teacher,
        object? oldValues,
        object? newValues) =>
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor.UserId,
            Action = action,
            EntityType = "Teacher",
            EntityId = teacher.Id,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = actor.IpAddress,
            CreatedAt = timeProvider.GetUtcNow()
        });

    private static TeacherState CaptureState(Teacher teacher) => new(
        teacher.UserId,
        teacher.TeacherCode,
        teacher.User.Email,
        teacher.User.FullName,
        teacher.User.PhoneNumber,
        teacher.User.Status,
        teacher.AttendanceEditWindowDays,
        teacher.Note,
        teacher.Version);

    private static TeacherAuditSnapshot AuditSnapshot(Teacher teacher) => new(
        teacher.UserId,
        teacher.TeacherCode,
        teacher.User.Status.ToString(),
        teacher.AttendanceEditWindowDays,
        teacher.User.PhoneNumber is not null,
        teacher.Note is not null,
        teacher.Version);

    private static TeacherAuditSnapshot AuditSnapshot(TeacherState teacher) => new(
        teacher.UserId,
        teacher.TeacherCode,
        teacher.Status.ToString(),
        teacher.AttendanceEditWindowDays,
        teacher.PhoneNumber is not null,
        teacher.Note is not null,
        teacher.Version);

    private static List<string> ChangedFields(
        TeacherState before,
        Teacher teacher,
        bool noteChanged)
    {
        var fields = new List<string>();
        AddIfChanged(fields, before.TeacherCode, teacher.TeacherCode, "teacherCode");
        AddIfChanged(fields, before.Email, teacher.User.Email, "email");
        AddIfChanged(fields, before.FullName, teacher.User.FullName, "fullName");
        AddIfChanged(fields, before.PhoneNumber, teacher.User.PhoneNumber, "phoneNumber");
        if (before.Status != teacher.User.Status) fields.Add("status");
        if (noteChanged) fields.Add("note");
        return fields;
    }

    private static void AddIfChanged(
        List<string> fields,
        string? oldValue,
        string? currentValue,
        string fieldName)
    {
        if (!string.Equals(oldValue, currentValue, StringComparison.Ordinal)) fields.Add(fieldName);
    }

    private static string NormalizeTeacherCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AppValidationException(
                "Mã giáo viên là bắt buộc.",
                new Dictionary<string, string[]> { ["teacherCode"] = ["Mã giáo viên là bắt buộc."] });
        }

        return value.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool Matches(
        AssessmentListItemResponse candidate,
        string foldedSearch,
        string searchDigits) =>
        VietnameseSearchNormalizer.Fold(candidate.TeacherCode).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Fold(candidate.FullName).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Fold(candidate.Email).Contains(foldedSearch, StringComparison.Ordinal) ||
        (searchDigits.Length > 0 && candidate.PhoneNumber is not null &&
            VietnameseSearchNormalizer.Digits(candidate.PhoneNumber).Contains(searchDigits, StringComparison.Ordinal));

    private static PagedResponse<AssessmentListItemResponse> CreatePage(
        IReadOnlyList<AssessmentListItemResponse> items,
        AssessmentListQuery query,
        int totalItems) =>
        new(items, new PaginationMetadata(
            query.Page,
            query.PageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)query.PageSize)));

    private static IQueryable<AssessmentListItemResponse> ProjectList(IQueryable<Assessment> query) =>
        query.Select(x => new AssessmentListItemResponse(
            x.Id,
            x.UserId,
            x.TeacherCode,
            x.User.FullName,
            x.User.Email,
            x.User.PhoneNumber,
            x.User.Status,
            x.AttendanceEditWindowDays,
            x.ResponsibleGroups.Count,
            x.CreatedAt,
            x.UpdatedAt,
            x.Version));

    private static IQueryable<AssessmentDetailResponse> ProjectDetail(IQueryable<Assessment> query) =>
        query.Select(x => new AssessmentDetailResponse(
            x.Id,
            x.UserId,
            x.TeacherCode,
            x.User.FullName,
            x.User.Email,
            x.User.PhoneNumber,
            x.User.Status,
            x.AttendanceEditWindowDays,
            x.ResponsibleGroups.Count,
            x.CreatedAt,
            x.UpdatedAt,
            x.Version,
            x.Note,
            x.ResponsibleGroups
                .OrderBy(group => group.Code)
                .ThenBy(group => group.Id)
                .Select(group => new AssessmentGroupSummaryResponse(
                    group.Id,
                    group.Code,
                    group.Name,
                    group.Status,
                    group.Students.Count(student => student.Status == StudentStatus.Active)))
                .ToList()));

    private static IOrderedQueryable<Assessment> ApplySort(
        IQueryable<Assessment> query,
        string sortBy,
        bool descending) => (sortBy.ToLowerInvariant(), descending) switch
        {
            ("teachercode", false) => query.OrderBy(x => x.TeacherCode).ThenBy(x => x.Id),
            ("teachercode", true) => query.OrderByDescending(x => x.TeacherCode).ThenByDescending(x => x.Id),
            ("fullname", false) => query.OrderBy(x => x.User.FullName).ThenBy(x => x.Id),
            ("fullname", true) => query.OrderByDescending(x => x.User.FullName).ThenByDescending(x => x.Id),
            ("email", false) => query.OrderBy(x => x.User.Email).ThenBy(x => x.Id),
            ("email", true) => query.OrderByDescending(x => x.User.Email).ThenByDescending(x => x.Id),
            ("status", false) => query.OrderBy(x => x.User.Status).ThenBy(x => x.Id),
            ("status", true) => query.OrderByDescending(x => x.User.Status).ThenByDescending(x => x.Id),
            ("attendanceeditwindowdays", false) => query.OrderBy(x => x.AttendanceEditWindowDays).ThenBy(x => x.Id),
            ("attendanceeditwindowdays", true) => query.OrderByDescending(x => x.AttendanceEditWindowDays).ThenByDescending(x => x.Id),
            ("responsiblegroupcount", false) => query.OrderBy(x => x.ResponsibleGroups.Count).ThenBy(x => x.Id),
            ("responsiblegroupcount", true) => query.OrderByDescending(x => x.ResponsibleGroups.Count).ThenByDescending(x => x.Id),
            ("createdat", false) => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            ("createdat", true) => query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id),
            ("updatedat", false) => query.OrderBy(x => x.UpdatedAt).ThenBy(x => x.Id),
            ("updatedat", true) => query.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id),
            _ => throw new AppValidationException(
                "Trường sắp xếp không hợp lệ.",
                new Dictionary<string, string[]>
                {
                    ["sortBy"] =
                    [
                        "Chỉ hỗ trợ teacherCode, fullName, email, status, attendanceEditWindowDays, " +
                        "responsibleGroupCount, createdAt hoặc updatedAt."
                    ]
                })
        };

    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Information,
        Message = "Teacher accent search evaluated {CandidateCount} candidates, matched {MatchCount}, duration {DurationMs} ms")]
    private static partial void LogAccentSearch(
        ILogger logger,
        int candidateCount,
        int matchCount,
        double durationMs);

    private sealed record TeacherState(
        Guid UserId,
        string TeacherCode,
        string Email,
        string FullName,
        string? PhoneNumber,
        UserStatus Status,
        int AttendanceEditWindowDays,
        string? Note,
        int Version);

    private sealed record TeacherAuditSnapshot(
        Guid UserId,
        string TeacherCode,
        string Status,
        int AttendanceEditWindowDays,
        bool PhoneNumberPresent,
        bool NotePresent,
        int Version);
}

public static class VietnameseSearchNormalizer
{
    public static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasWhitespace = true;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            var normalized = character is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(character);
            if (char.IsWhiteSpace(normalized))
            {
                if (!previousWasWhitespace) builder.Append(' ');
                previousWasWhitespace = true;
                continue;
            }

            builder.Append(normalized);
            previousWasWhitespace = false;
        }

        return builder.ToString().TrimEnd();
    }

    public static string Digits(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsDigit(character)) builder.Append(character);
        }

        return builder.ToString();
    }
}
