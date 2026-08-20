using System.Text.Json;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AdminPortal.Application.AssessmentSheets;

public sealed class AssessmentSheetService(
    IApplicationDbContext dbContext,
    ICurrentActor currentActor,
    TimeProvider timeProvider) : IAssessmentSheetService
{
    public async Task<PagedResponse<AssessmentSheetListItemResponse>> ListAsync(
        AssessmentSheetListQuery query,
        CancellationToken cancellationToken)
    {
        EnsureAssessmentSheetRole(currentActor.GetRequired());

        var sheets = dbContext.AssessmentSheets.AsNoTracking();
        if (query.StudentId is not null) sheets = sheets.Where(x => x.StudentId == query.StudentId);
        if (query.Status is not null) sheets = sheets.Where(x => x.AssessmentSheetStatus == query.Status);

        var descending = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var ordered = ApplySheetSort(sheets, query.SortBy, descending);
        if (string.IsNullOrWhiteSpace(query.Search))
        {
            var total = await sheets.CountAsync(cancellationToken);
            var page = ordered
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize);
            var items = await ProjectList(page).ToListAsync(cancellationToken);

            return new PagedResponse<AssessmentSheetListItemResponse>(
                items,
                new PaginationMetadata(query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)));
        }

        var foldedSearch = VietnameseSearchNormalizer.Fold(query.Search);
        var matches = (await ProjectList(ordered).ToListAsync(cancellationToken))
            .Where(x => Matches(x, foldedSearch))
            .ToList();
        var pageItems = matches
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new PagedResponse<AssessmentSheetListItemResponse>(
            pageItems,
            new PaginationMetadata(query.Page, query.PageSize, matches.Count, (int)Math.Ceiling(matches.Count / (double)query.PageSize)));
    }

    public async Task<AssessmentSheetDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureAssessmentSheetRole(currentActor.GetRequired());
        return await BuildDetailAsync(id, cancellationToken);
    }

    public async Task<PagedResponse<AssessmentPlanCandidateResponse>> ListPlanCandidatesAsync(
        AssessmentPlanCandidateQuery query,
        CancellationToken cancellationToken)
    {
        EnsureAssessmentSheetRole(currentActor.GetRequired());

        var studentExists = await dbContext.Students.AsNoTracking()
            .AnyAsync(x => x.Id == query.StudentId, cancellationToken);
        if (!studentExists)
            throw new NotFoundException("Không tìm thấy học sinh.", ProblemCodes.StudentNotFound);

        var latestGrades = await LoadLatestGradesAsync(query.StudentId, cancellationToken);
        var assessments = dbContext.Assessments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.GroupLv1Name))
            assessments = assessments.Where(x => x.GroupLv1Name == query.GroupLv1Name);
        if (!string.IsNullOrWhiteSpace(query.GroupLv2Name))
            assessments = assessments.Where(x => x.GroupLv2Name == query.GroupLv2Name);
        if (!string.IsNullOrWhiteSpace(query.GroupLv3Name))
            assessments = assessments.Where(x => x.GroupLv3Name == query.GroupLv3Name);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToUpperInvariant();
#pragma warning disable CA1304, CA1311, CA1862
            assessments = assessments.Where(x =>
                x.Code.ToUpper().Contains(search) ||
                x.Name.ToUpper().Contains(search) ||
                (x.GroupLv1Name != null && x.GroupLv1Name.ToUpper().Contains(search)) ||
                (x.GroupLv2Name != null && x.GroupLv2Name.ToUpper().Contains(search)) ||
                (x.GroupLv3Name != null && x.GroupLv3Name.ToUpper().Contains(search)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var candidates = await assessments
            .Select(x => new AssessmentPlanCandidateResponse(
                x.Id,
                x.Code,
                x.Name,
                x.GroupLv1Name,
                x.GroupLv2Name,
                x.GroupLv3Name,
                x.RowIndex,
                null))
            .ToListAsync(cancellationToken);

        candidates = candidates
            .Select(x => x with { LatestGrade = latestGrades.GetValueOrDefault(x.Code) })
            .ToList();

        if (query.LatestGradeAtOrBelow is not null)
        {
            var threshold = GradeRank(query.LatestGradeAtOrBelow.Value);
            candidates = candidates
                .Where(x => x.LatestGrade is not null && GradeRank(x.LatestGrade.Value) <= threshold)
                .ToList();
        }

        var descending = query.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var ordered = ApplyCandidateSort(candidates, query.SortBy, descending);
        var total = candidates.Count;
        var items = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new PagedResponse<AssessmentPlanCandidateResponse>(
            items,
            new PaginationMetadata(query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)));
    }

    public async Task<AssessmentSheetDetailResponse> CreateAsync(
        CreateAssessmentSheetRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        EnsureAssessmentSheetRole(actor);
        var name = NormalizeRequired(request.Name, "name", "Tên bảng đánh giá là bắt buộc.");
        EnsureDistinctIds(request.AssessmentIds, "assessmentIds");

        var student = await dbContext.Students.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy học sinh.", ProblemCodes.StudentNotFound);
        if (student.Status != StudentStatus.Active)
            throw new ConflictException("Không thể tạo bảng đánh giá cho học sinh ngừng hoạt động.", ProblemCodes.StudentInactive);

        var responsibleTeacher = await LoadResponsibleTeacherAsync(request.ResponsibleTeacherId, cancellationToken);
        var assessments = await LoadAssessmentsByIdsAsync(request.AssessmentIds, cancellationToken);
        var latestGrades = await LoadLatestGradesAsync(student.Id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var sheet = new AssessmentSheet
        {
            Id = Guid.NewGuid(),
            Name = name,
            AssessmentSheetStatus = AssessmentSheetStatus.Open,
            StudentId = student.Id,
            StudentSnapshot = Snapshot(student),
            ResponsibleTeacherId = responsibleTeacher?.Id,
            ResponsibleTeacherFullNameSnapshot = responsibleTeacher?.User.FullName,
            Note = NormalizeOptional(request.Note),
            StartDate = request.StartDate,
            DueDate = request.DueDate,
            Feedback = null,
            UpdatedByUserId = actor.UserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.AssessmentSheets.Add(sheet);
        await dbContext.AssessmentRecords.AddRangeAsync(
            BuildRecords(sheet.Id, assessments, latestGrades, request.AssessmentIds, now, actor.UserId),
            cancellationToken);
        AddAudit(actor, "AssessmentSheet.Created", sheet.Id, null, new { sheet.StudentId, RecordCount = request.AssessmentIds.Count });
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildDetailAsync(sheet.Id, cancellationToken);
    }

    public async Task<AssessmentSheetDetailResponse> UpdateAsync(
        Guid id,
        UpdateAssessmentSheetRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        EnsureAssessmentSheetRole(actor);
        var sheet = await FindRequiredAsync(id, cancellationToken);
        EnsureOpen(sheet);
        var name = NormalizeRequired(request.Name, "name", "Tên bảng đánh giá là bắt buộc.");
        var responsibleTeacher = await LoadResponsibleTeacherAsync(request.ResponsibleTeacherId, cancellationToken);
        var old = SnapshotForAudit(sheet);
        var now = timeProvider.GetUtcNow();

        sheet.Name = name;
        sheet.ResponsibleTeacherId = responsibleTeacher?.Id;
        sheet.ResponsibleTeacherFullNameSnapshot = responsibleTeacher?.User.FullName;
        sheet.Note = NormalizeOptional(request.Note);
        sheet.StartDate = request.StartDate;
        sheet.DueDate = request.DueDate;
        sheet.Feedback = NormalizeOptional(request.Feedback);
        sheet.UpdatedByUserId = actor.UserId;
        sheet.UpdatedAt = now;

        AddAudit(actor, "AssessmentSheet.Updated", sheet.Id, old, SnapshotForAudit(sheet));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildDetailAsync(sheet.Id, cancellationToken);
    }

    public async Task<AssessmentSheetDetailResponse> ReplaceRecordsAsync(
        Guid id,
        ReplaceAssessmentSheetRecordsRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        EnsureAssessmentSheetRole(actor);
        EnsureDistinctIds(request.Records.Select(x => x.AssessmentId).ToArray(), "records");
        var sheet = await FindRequiredAsync(id, cancellationToken);
        EnsureOpen(sheet);
        var assessments = await LoadAssessmentsByIdsAsync(request.Records.Select(x => x.AssessmentId).ToArray(), cancellationToken);
        var assessmentById = assessments.ToDictionary(x => x.Id);
        var oldRecords = await dbContext.AssessmentRecords
            .Where(x => x.AssessmentSheetId == id)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        dbContext.AssessmentRecords.RemoveRange(oldRecords);
        foreach (var requestRecord in request.Records)
        {
            var assessment = assessmentById[requestRecord.AssessmentId];
            dbContext.AssessmentRecords.Add(new AssessmentRecord
            {
                Id = Guid.NewGuid(),
                AssessmentSheetId = sheet.Id,
                AssessmentSheet = sheet,
                AssessmentRowIndex = assessment.RowIndex,
                AssessmentSnapshot = Snapshot(assessment),
                PlanGrade = requestRecord.PlanGrade,
                PlanNote = NormalizeOptional(requestRecord.PlanNote),
                FinalGrade = requestRecord.FinalGrade,
                FinalNote = NormalizeOptional(requestRecord.FinalNote),
                UpdatedByUserId = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        sheet.UpdatedByUserId = actor.UserId;
        sheet.UpdatedAt = now;
        AddAudit(actor, "AssessmentSheet.RecordsReplaced", sheet.Id, new { RecordCount = oldRecords.Count }, new { RecordCount = request.Records.Count });
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildDetailAsync(sheet.Id, cancellationToken);
    }

    public async Task<AssessmentSheetDetailResponse> UpdateStatusAsync(
        Guid id,
        UpdateAssessmentSheetStatusRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        EnsureAssessmentSheetRole(actor);
        var sheet = await FindRequiredAsync(id, cancellationToken);
        var old = SnapshotForAudit(sheet);
        var now = timeProvider.GetUtcNow();

        sheet.AssessmentSheetStatus = request.Status;
        sheet.DoneDate = request.Status == AssessmentSheetStatus.Done ? now : null;
        sheet.UpdatedByUserId = actor.UserId;
        sheet.UpdatedAt = now;

        AddAudit(actor, "AssessmentSheet.StatusUpdated", sheet.Id, old, SnapshotForAudit(sheet));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildDetailAsync(sheet.Id, cancellationToken);
    }

    public async Task<AssessmentSheetDetailResponse> ExportToSheetAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureExistsForGoogleActionAsync(id, cancellationToken);
        throw GoogleMappingBlocked("Chưa có mapping cột cho sheet data của file F01 nên chưa thể xuất bảng đánh giá.");
    }

    public async Task<AssessmentSheetDetailResponse> SyncToSheetAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureExistsForGoogleActionAsync(id, cancellationToken);
        throw GoogleMappingBlocked("Chưa có mapping cột cho sheet data của file F01 nên chưa thể đồng bộ bảng đánh giá.");
    }

    public async Task<AssessmentSheetDetailResponse> GeneratePlanPdfAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureExistsForGoogleActionAsync(id, cancellationToken);
        throw GoogleMappingBlocked("Chưa có mapping vị trí dữ liệu trên khcn_template và cấu hình lưu PDF nên chưa thể sinh F02.");
    }

    public async Task<AssessmentSheetDetailResponse> GenerateResultPdfAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureExistsForGoogleActionAsync(id, cancellationToken);
        throw GoogleMappingBlocked("Chưa có mapping vị trí dữ liệu trên KQ_template và cấu hình lưu PDF nên chưa thể sinh F03.");
    }

    public async Task<AssessmentSheetDetailResponse> SubmitResultsAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureExistsForGoogleActionAsync(id, cancellationToken);
        throw GoogleMappingBlocked("Chưa có mapping dò ô E16:E/H16:16 trong F0.ĐG nên chưa thể ghi kết quả đánh giá.");
    }

    private async Task EnsureExistsForGoogleActionAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureAssessmentSheetRole(currentActor.GetRequired());
        _ = await dbContext.AssessmentSheets.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw AssessmentSheetNotFound();
    }

    private async Task<AssessmentSheetDetailResponse> BuildDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var sheet = await dbContext.AssessmentSheets.AsNoTracking()
            .Include(x => x.ResponsibleTeacher)
            .ThenInclude(x => x!.User)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw AssessmentSheetNotFound();
        var records = await dbContext.AssessmentRecords.AsNoTracking()
            .Where(x => x.AssessmentSheetId == id)
            .OrderBy(x => x.AssessmentRowIndex ?? int.MaxValue)
            .ThenBy(x => x.AssessmentSnapshot.Code)
            .Select(x => new AssessmentSheetRecordResponse(
                x.Id,
                x.AssessmentSheetId,
                x.AssessmentRowIndex,
                ToResponse(x.AssessmentSnapshot),
                x.PlanGrade,
                x.PlanNote,
                x.FinalGrade,
                x.FinalNote,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new AssessmentSheetDetailResponse(
            sheet.Id,
            sheet.Name,
            sheet.AssessmentSheetStatus,
            sheet.StudentId,
            ToResponse(sheet.StudentSnapshot),
            sheet.ResponsibleTeacherId,
            sheet.ResponsibleTeacherFullNameSnapshot,
            sheet.Note,
            sheet.StartDate,
            sheet.DueDate,
            sheet.DoneDate,
            sheet.SubmissionDate,
            sheet.Feedback,
            sheet.AssessmentSheetSpreadsheetId,
            sheet.PlanFileLinkPdf,
            sheet.ResultFileLinkPdf,
            sheet.CreatedAt,
            sheet.UpdatedAt,
            records);
    }

    private async Task<AssessmentSheet> FindRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.AssessmentSheets.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw AssessmentSheetNotFound();

    private async Task<Teacher?> LoadResponsibleTeacherAsync(Guid? responsibleTeacherId, CancellationToken cancellationToken)
    {
        if (responsibleTeacherId is null)
            return null;

        var teacher = await dbContext.Teachers
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == responsibleTeacherId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy giáo viên.", ProblemCodes.TeacherNotFound);
        if (teacher.User.Role != UserRole.Teacher || teacher.User.Status != UserStatus.Active)
            throw new ConflictException("Chỉ có thể chọn giáo viên đang hoạt động.", ProblemCodes.TeacherNotFound);

        return teacher;
    }

    private async Task<List<Assessment>> LoadAssessmentsByIdsAsync(
        IReadOnlyCollection<Guid> assessmentIds,
        CancellationToken cancellationToken)
    {
        var assessments = await dbContext.Assessments.AsNoTracking()
            .Where(x => assessmentIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (assessments.Count != assessmentIds.Count)
            throw new NotFoundException("Không tìm thấy đủ mục đánh giá.", ProblemCodes.AssessmentNotFound);

        return assessments;
    }

    private async Task<Dictionary<string, AssessmentGrade?>> LoadLatestGradesAsync(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var latestSheetId = await dbContext.AssessmentSheetLatests.AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (latestSheetId is null)
            return new Dictionary<string, AssessmentGrade?>(StringComparer.Ordinal);

        return await dbContext.AssessmentRecordLatests.AsNoTracking()
            .Where(x => x.AssessmentSheetLatestId == latestSheetId)
            .ToDictionaryAsync(x => x.AssessmentCode, x => x.LatestGrade, StringComparer.Ordinal, cancellationToken);
    }

    private static List<AssessmentRecord> BuildRecords(
        Guid sheetId,
        IReadOnlyCollection<Assessment> assessments,
        IReadOnlyDictionary<string, AssessmentGrade?> latestGrades,
        IReadOnlyList<Guid> assessmentIds,
        DateTimeOffset now,
        Guid actorUserId)
    {
        var assessmentById = assessments.ToDictionary(x => x.Id);
        return assessmentIds.Select(assessmentId =>
        {
            var assessment = assessmentById[assessmentId];
            return new AssessmentRecord
            {
                Id = Guid.NewGuid(),
                AssessmentSheetId = sheetId,
                AssessmentSheet = null!,
                AssessmentRowIndex = assessment.RowIndex,
                AssessmentSnapshot = Snapshot(assessment),
                PlanGrade = latestGrades.GetValueOrDefault(assessment.Code),
                PlanNote = null,
                FinalGrade = null,
                FinalNote = null,
                UpdatedByUserId = actorUserId,
                CreatedAt = now,
                UpdatedAt = now
            };
        }).ToList();
    }

    private void AddAudit(ActorContext actor, string action, Guid entityId, object? oldValue, object? newValue) =>
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor.UserId,
            Action = action,
            EntityType = "AssessmentSheet",
            EntityId = entityId,
            OldValues = oldValue is null ? null : JsonSerializer.Serialize(oldValue),
            NewValues = newValue is null ? null : JsonSerializer.Serialize(newValue),
            IpAddress = actor.IpAddress,
            CreatedAt = timeProvider.GetUtcNow()
        });

    private static object SnapshotForAudit(AssessmentSheet sheet) => new
    {
        sheet.Name,
        Status = sheet.AssessmentSheetStatus.ToString(),
        sheet.StudentId,
        sheet.ResponsibleTeacherId,
        sheet.StartDate,
        sheet.DueDate,
        sheet.DoneDate,
        sheet.SubmissionDate
    };

    private static IQueryable<AssessmentSheetListItemResponse> ProjectList(IQueryable<AssessmentSheet> query) =>
        query.Select(x => new AssessmentSheetListItemResponse(
            x.Id,
            x.Name,
            x.AssessmentSheetStatus,
            x.StudentId,
            x.StudentSnapshot.StudentCode,
            x.StudentSnapshot.FullName,
            x.ResponsibleTeacherId,
            x.ResponsibleTeacherFullNameSnapshot,
            x.StartDate,
            x.DueDate,
            x.DoneDate,
            x.SubmissionDate,
            x.AssessmentSheetSpreadsheetId,
            x.PlanFileLinkPdf,
            x.ResultFileLinkPdf,
            x.CreatedAt,
            x.UpdatedAt));

    private static bool Matches(AssessmentSheetListItemResponse item, string foldedSearch) =>
        VietnameseSearchNormalizer.Fold(item.Name).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Fold(item.StudentCode).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Fold(item.StudentFullName).Contains(foldedSearch, StringComparison.Ordinal);

    private static IOrderedQueryable<AssessmentSheet> ApplySheetSort(
        IQueryable<AssessmentSheet> query,
        string sortBy,
        bool descending) => (sortBy.ToLowerInvariant(), descending) switch
        {
            ("name", false) => query.OrderBy(x => x.Name).ThenBy(x => x.Id),
            ("name", true) => query.OrderByDescending(x => x.Name).ThenByDescending(x => x.Id),
            ("status", false) => query.OrderBy(x => x.AssessmentSheetStatus).ThenBy(x => x.Id),
            ("status", true) => query.OrderByDescending(x => x.AssessmentSheetStatus).ThenByDescending(x => x.Id),
            ("startdate", false) => query.OrderBy(x => x.StartDate).ThenBy(x => x.Id),
            ("startdate", true) => query.OrderByDescending(x => x.StartDate).ThenByDescending(x => x.Id),
            ("duedate", false) => query.OrderBy(x => x.DueDate).ThenBy(x => x.Id),
            ("duedate", true) => query.OrderByDescending(x => x.DueDate).ThenByDescending(x => x.Id),
            ("updatedat", false) => query.OrderBy(x => x.UpdatedAt).ThenBy(x => x.Id),
            ("updatedat", true) => query.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id),
            ("createdat", false) => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            ("createdat", true) => query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id),
            _ => throw new AppValidationException("Trường sắp xếp không hợp lệ.", new Dictionary<string, string[]>
            { ["sortBy"] = ["Chỉ hỗ trợ name, status, startDate, dueDate, updatedAt hoặc createdAt."] })
        };

    private static IOrderedEnumerable<AssessmentPlanCandidateResponse> ApplyCandidateSort(
        IEnumerable<AssessmentPlanCandidateResponse> query,
        string sortBy,
        bool descending) => (sortBy.ToLowerInvariant(), descending) switch
        {
            ("code", false) => query.OrderBy(x => x.Code, StringComparer.Ordinal),
            ("code", true) => query.OrderByDescending(x => x.Code, StringComparer.Ordinal),
            ("name", false) => query.OrderBy(x => x.Name, StringComparer.Ordinal),
            ("name", true) => query.OrderByDescending(x => x.Name, StringComparer.Ordinal),
            ("rowindex", false) => query.OrderBy(x => x.RowIndex).ThenBy(x => x.Id),
            ("rowindex", true) => query.OrderByDescending(x => x.RowIndex).ThenByDescending(x => x.Id),
            ("latestgrade", false) => query.OrderBy(x => x.LatestGrade is null).ThenBy(x => x.LatestGrade),
            ("latestgrade", true) => query.OrderByDescending(x => x.LatestGrade is not null).ThenByDescending(x => x.LatestGrade),
            _ => throw new AppValidationException("Trường sắp xếp không hợp lệ.", new Dictionary<string, string[]>
            { ["sortBy"] = ["Chỉ hỗ trợ code, name, rowIndex hoặc latestGrade."] })
        };

    private static void EnsureAssessmentSheetRole(ActorContext actor)
    {
        if (actor.Role is not (UserRole.SuperAdmin or UserRole.Admin or UserRole.Teacher))
            throw new ForbiddenException("Không đủ quyền.");
    }

    private static void EnsureOpen(AssessmentSheet sheet)
    {
        if (sheet.AssessmentSheetStatus == AssessmentSheetStatus.Done)
            throw new ConflictException("Bảng đánh giá đã hoàn thành, không thể chỉnh sửa.", ProblemCodes.AssessmentSheetDone);
    }

    private static void EnsureDistinctIds(IReadOnlyCollection<Guid> ids, string field)
    {
        if (ids.Count == 0 || ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            throw new AppValidationException("Danh sách mục đánh giá không hợp lệ.", new Dictionary<string, string[]>
            { [field] = ["Danh sách phải có ít nhất một mục hợp lệ và không được trùng."] });
    }

    private static string NormalizeRequired(string value, string field, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new AppValidationException(message, new Dictionary<string, string[]> { [field] = [message] });
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static StudentSnapshot Snapshot(Student student) => new()
    {
        StudentCode = student.StudentCode,
        FullName = student.FullName,
        NickName = student.NickName,
        DateOfBirth = student.DateOfBirth,
        Gender = student.Gender
    };

    private static AssessmentSnapshot Snapshot(Assessment assessment) => new()
    {
        Code = assessment.Code,
        Name = assessment.Name,
        GroupLv1Name = assessment.GroupLv1Name,
        GroupLv2Name = assessment.GroupLv2Name,
        GroupLv3Name = assessment.GroupLv3Name,
        RowIndex = assessment.RowIndex
    };

    private static AssessmentSheetStudentSnapshotResponse ToResponse(StudentSnapshot snapshot) =>
        new(snapshot.StudentCode, snapshot.FullName, snapshot.NickName, snapshot.DateOfBirth, snapshot.Gender);

    private static AssessmentSnapshotResponse ToResponse(AssessmentSnapshot snapshot) =>
        new(snapshot.Code, snapshot.Name, snapshot.GroupLv1Name, snapshot.GroupLv2Name, snapshot.GroupLv3Name, snapshot.RowIndex);

    private static int GradeRank(AssessmentGrade grade) => grade switch
    {
        AssessmentGrade.A => 4,
        AssessmentGrade.B => 3,
        AssessmentGrade.C => 2,
        AssessmentGrade.D => 1,
        _ => 0
    };

    private static NotFoundException AssessmentSheetNotFound() =>
        new("Không tìm thấy bảng đánh giá.", ProblemCodes.AssessmentSheetNotFound);

    private static NormalException GoogleMappingBlocked(string message) =>
        new(message, ProblemCodes.AssessmentSheetGoogleMappingBlocked);
}
