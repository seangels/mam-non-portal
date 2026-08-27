using System.Globalization;
using System.Text;
using System.Text.Json;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.GoogleSheets;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;

namespace AdminPortal.Application.AssessmentSheets;

public sealed partial class AssessmentSheetService(
    IApplicationDbContext dbContext,
    ICurrentActor currentActor,
    TimeProvider timeProvider,
    IGoogleSheetsService googleSheetsService) : IAssessmentSheetService
{
    private static readonly TimeSpan BusinessDateOffset = TimeSpan.FromHours(7);

    public async Task<PagedResponse<AssessmentSheetListItemResponse>> ListAsync(
        AssessmentSheetListQuery query,
        CancellationToken cancellationToken)
    {
        AssessmentSheetRules.EnsureAssessmentSheetRole(currentActor.GetRequired());

        var dateFrom = NormalizeTimestamp(query.DateFrom);
        var dateTo = NormalizeTimestamp(query.DateTo);
        var sheets = dbContext.AssessmentSheets.AsNoTracking();
        if (query.StudentId is not null) sheets = sheets.Where(x => x.StudentId == query.StudentId);
        if (query.Status is not null) sheets = sheets.Where(x => x.AssessmentSheetStatus == query.Status);
        if(dateFrom is not null) sheets = sheets.Where(x => x.StartDate <= dateFrom && dateFrom <= x.DueDate);
        if(dateTo is not null) sheets = sheets.Where(x => x.StartDate <= dateTo && dateTo >= x.DueDate);

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
        AssessmentSheetRules.EnsureAssessmentSheetRole(currentActor.GetRequired());
        return await BuildDetailAsync(id, cancellationToken);
    }

    public async Task<AssessmentSheetDetailResponse> CreateAsync(
        CreateAssessmentSheetRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AssessmentSheetRules.EnsureAssessmentSheetRole(actor);
        var assessmentIds = request.Records.Select(x => x.AssessmentId).ToArray();
        AssessmentSheetRules.EnsureDistinctIds(assessmentIds, "records");

        var student = await dbContext.Students.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy học sinh.", ProblemCodes.StudentNotFound);
        if (student.Status != StudentStatus.Active)
            throw new ConflictException("Không thể tạo bảng đánh giá cho học sinh ngừng hoạt động.", ProblemCodes.StudentInactive);

        var responsibleTeacher = await LoadResponsibleTeacherAsync(request.ResponsibleTeacherId, cancellationToken);
        var assessments = await LoadAssessmentsByIdsAsync(assessmentIds, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var sheet = new AssessmentSheet
        {
            Id = Guid.NewGuid(),
            AssessmentSheetStatus = AssessmentSheetStatus.Open,
            StudentId = student.Id,
            StudentSnapshot = Snapshot(student),
            ResponsibleTeacherId = responsibleTeacher?.Id,
            ResponsibleTeacherFullNameSnapshot = responsibleTeacher?.User.FullName,
            Note = NormalizeOptional(request.Note),
            StartDate = NormalizeTimestamp(request.StartDate),
            DueDate = NormalizeTimestamp(request.DueDate),
            Feedback = null,
            UpdatedByUserId = actor.UserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.AssessmentSheets.Add(sheet);
        await dbContext.AssessmentRecords.AddRangeAsync(
            AssessmentSheetRules.BuildRecords(sheet.Id, assessments, request.Records, now, actor.UserId),
            cancellationToken);
        AddAudit(actor, "AssessmentSheet.Created", sheet.Id, null, new { sheet.StudentId, RecordCount = request.Records.Count });
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildDetailAsync(sheet.Id, cancellationToken);
    }

    public async Task<ImportAssessmentSheetsPreviewResponse> PreviewExcelImportAsync(
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AssessmentSheetRules.EnsureAssessmentSheetRole(actor);
        var plan = await BuildExcelImportPlanAsync(fileName, content, cancellationToken);
        return BuildImportPreviewResponse(plan);
    }

    public async Task<ImportAssessmentSheetsResponse> ImportExcelAsync(
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AssessmentSheetRules.EnsureAssessmentSheetRole(actor);
        var plan = await BuildExcelImportPlanAsync(fileName, content, cancellationToken);
        if (!plan.CanImport)
            throw BuildImportValidationException(plan);

        var now = timeProvider.GetUtcNow();
        var created = 0;
        var updated = 0;
        var importedRecords = 0;
        var results = new List<ImportedAssessmentSheetResponse>();
        var existingSheetIds = plan.Groups
            .Where(x => x.ExistingSheetId is not null)
            .Select(x => x.ExistingSheetId!.Value)
            .ToArray();
        var existingSheets = existingSheetIds.Length == 0
            ? new Dictionary<Guid, AssessmentSheet>()
            : await dbContext.AssessmentSheets
                .Where(x => existingSheetIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var group in plan.Groups)
        {
            var action = "Created";
            AssessmentSheet sheet;
            if (group.ExistingSheetId is not null)
            {
                sheet = existingSheets[group.ExistingSheetId.Value];
                var oldRecordCount = await dbContext.AssessmentRecords
                    .CountAsync(x => x.AssessmentSheetId == sheet.Id, cancellationToken);
                var old = new { AssessmentSheetId = sheet.Id, RecordCount = oldRecordCount };
                var oldRecords = await dbContext.AssessmentRecords
                    .Where(x => x.AssessmentSheetId == sheet.Id)
                    .ToListAsync(cancellationToken);
                dbContext.AssessmentRecords.RemoveRange(oldRecords);
                sheet.StudentSnapshot = Snapshot(group.Student);
                sheet.StartDate = group.StartDate;
                sheet.DueDate = group.DueDate;
                sheet.UpdatedByUserId = actor.UserId;
                sheet.UpdatedAt = now;
                updated++;
                action = "Updated";
                AddAudit(actor, "AssessmentSheet.ExcelImported", sheet.Id, old, BuildExcelImportAuditSnapshot(
                    sheet,
                    fileName,
                    content.LongLength,
                    action,
                    group.Rows.Count,
                    plan.SkippedDuplicateRowCount));
            }
            else
            {
                sheet = new AssessmentSheet
                {
                    Id = Guid.NewGuid(),
                    AssessmentSheetStatus = AssessmentSheetStatus.Open,
                    StudentId = group.Student.Id,
                    StudentSnapshot = Snapshot(group.Student),
                    ResponsibleTeacherId = null,
                    ResponsibleTeacherFullNameSnapshot = null,
                    Note = null,
                    StartDate = group.StartDate,
                    DueDate = group.DueDate,
                    Feedback = null,
                    UpdatedByUserId = actor.UserId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                dbContext.AssessmentSheets.Add(sheet);
                created++;
                AddAudit(actor, "AssessmentSheet.ExcelImported", sheet.Id, null, BuildExcelImportAuditSnapshot(
                    sheet,
                    fileName,
                    content.LongLength,
                    action,
                    group.Rows.Count,
                    plan.SkippedDuplicateRowCount));
            }

            foreach (var row in group.Rows)
            {
                AssessmentGrade? planGrade = null;
                if (!string.IsNullOrWhiteSpace(row.PlanGrade) &&
                    AssessmentSheetRules.TryParseGradeLabel(row.PlanGrade, out var planGradeOut))
                {
                    planGrade = planGradeOut;
                }
                dbContext.AssessmentRecords.Add(BuildImportedRecord(
                    sheet,
                    row.Assessment!,
                    planGrade,
                    row.PlanNote,
                    now, actor.UserId));
            }

            importedRecords += group.Rows.Count;
            results.Add(new ImportedAssessmentSheetResponse(
                sheet.Id,
                group.Student.StudentCode,
                group.Student.FullName,
                group.StartDate,
                group.DueDate,
                action,
                group.Rows.Count));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ImportAssessmentSheetsResponse(
            created,
            updated,
            importedRecords,
            plan.SkippedDuplicateRowCount,
            plan.Warnings,
            results);
    }

    public async Task<AssessmentSheetDetailResponse> UpdateAsync(
        Guid id,
        UpdateAssessmentSheetRequest request,
        CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AssessmentSheetRules.EnsureAssessmentSheetRole(actor);
        var sheet = await FindRequiredAsync(id, cancellationToken);
        AssessmentSheetRules.EnsureOpen(sheet);
        var responsibleTeacher = await LoadResponsibleTeacherAsync(request.ResponsibleTeacherId, cancellationToken);
        var old = SnapshotForAudit(sheet);
        var now = timeProvider.GetUtcNow();

        sheet.ResponsibleTeacherId = responsibleTeacher?.Id;
        sheet.ResponsibleTeacherFullNameSnapshot = responsibleTeacher?.User.FullName;
        sheet.Note = NormalizeOptional(request.Note);
        sheet.StartDate = NormalizeTimestamp(request.StartDate);
        sheet.DueDate = NormalizeTimestamp(request.DueDate);
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
        AssessmentSheetRules.EnsureAssessmentSheetRole(actor);
        AssessmentSheetRules.EnsureDistinctIds(request.Records.Select(x => x.AssessmentId).ToArray(), "records");
        var sheet = await FindRequiredAsync(id, cancellationToken);
        AssessmentSheetRules.EnsureOpen(sheet);
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
            dbContext.AssessmentRecords.Add(
                AssessmentSheetRules.BuildReplacementRecord(sheet, assessment, requestRecord, now, actor.UserId));
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
        AssessmentSheetRules.EnsureAssessmentSheetRole(actor);
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

    public async Task<AssessmentSheetDetailResponse> UploadPlanPdfAsync(
        Guid id, string fileName, byte[] content, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AssessmentSheetRules.EnsureAssessmentSheetRole(actor);
        if (content.Length == 0)
        {
            throw new AppValidationException("File PDF kế hoạch không hợp lệ.", new Dictionary<string, string[]>
            {
                ["file"] = ["Vui lòng chọn file PDF có nội dung."]
            });
        }

        var sheet = await FindRequiredAsync(id, cancellationToken);
        var oldLink = sheet.PlanFileLinkPdf;
        var old = PdfUploadAuditSnapshot(sheet, "Plan", fileName, content.LongLength, oldLink, oldLink);
        var link = await googleSheetsService.UploadAssessmentSheetPlanPdfAsync(
            sheet.Id, sheet.StudentId, sheet.PlanFileLinkPdf, fileName, content, cancellationToken);

        var now = timeProvider.GetUtcNow();
        sheet.PlanFileLinkPdf = link;
        sheet.UpdatedByUserId = actor.UserId;
        sheet.UpdatedAt = now;

        AddAudit(actor, "AssessmentSheet.PlanPdfUploaded", sheet.Id, old,
            PdfUploadAuditSnapshot(sheet, "Plan", fileName, content.LongLength, oldLink, link));
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildDetailAsync(id, cancellationToken);
    }

    public async Task<AssessmentSheetDetailResponse> UploadResultPdfAsync(
        Guid id, string fileName, byte[] content, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AssessmentSheetRules.EnsureAssessmentSheetRole(actor);
        if (content.Length == 0)
        {
            throw new AppValidationException("File PDF kết quả không hợp lệ.", new Dictionary<string, string[]>
            {
                ["file"] = ["Vui lòng chọn file PDF có nội dung."]
            });
        }

        var sheet = await FindRequiredAsync(id, cancellationToken);
        var oldLink = sheet.ResultFileLinkPdf;
        var old = PdfUploadAuditSnapshot(sheet, "Result", fileName, content.LongLength, oldLink, oldLink);
        var link = await googleSheetsService.UploadAssessmentSheetResultPdfAsync(
            sheet.Id, sheet.StudentId, sheet.ResultFileLinkPdf, fileName, content, cancellationToken);

        var now = timeProvider.GetUtcNow();
        sheet.ResultFileLinkPdf = link;
        sheet.UpdatedByUserId = actor.UserId;
        sheet.UpdatedAt = now;

        AddAudit(actor, "AssessmentSheet.ResultPdfUploaded", sheet.Id, old,
            PdfUploadAuditSnapshot(sheet, "Result", fileName, content.LongLength, oldLink, link));
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildDetailAsync(id, cancellationToken);
    }

    public async Task<AssessmentSheetDetailResponse> SubmitResultsAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = currentActor.GetRequired();
        AssessmentSheetRules.EnsureAssessmentSheetRole(actor);
        var sheet = await FindRequiredAsync(id, cancellationToken);
        var records = await LoadRecordEntitiesAsync(id, cancellationToken);

        var studentCode = sheet.StudentSnapshot.StudentCode
            ?? throw new ConflictException(
                "Bảng đánh giá thiếu mã học sinh trong snapshot, không thể ghi vào [F0.ĐG].",
                ProblemCodes.AssessmentSheetGoogleOperationFailed);
        var resultSourceUpdates = await googleSheetsService.WriteFinalGradesToSourceSheetAsync(studentCode, records, cancellationToken);

        var old = SnapshotForAudit(sheet);
        var now = timeProvider.GetUtcNow();
        sheet.SubmissionDate = now;
        sheet.UpdatedByUserId = actor.UserId;
        sheet.UpdatedAt = now;
        AddAudit(actor, "AssessmentSheet.ResultsSubmitted", sheet.Id, old, SubmitResultsAuditSnapshot(sheet, resultSourceUpdates.Count));
        foreach (var resultSourceUpdate in resultSourceUpdates)
        {
            AddAudit(
                actor,
                "AssessmentSheet.ResultSourceCellUpdated",
                sheet.Id,
                ResultSourceCellAuditSnapshot(sheet, resultSourceUpdate, resultSourceUpdate.CurrentValue, now),
                ResultSourceCellAuditSnapshot(sheet, resultSourceUpdate, resultSourceUpdate.NewValue, now));
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildDetailAsync(id, cancellationToken);
    }

    private async Task<ExcelImportPlan> BuildExcelImportPlanAsync(
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var rows = ParseExcelImportRows(content);
        if (rows.Count == 0)
        {
            throw new AppValidationException("File Excel import không có dòng dữ liệu.", new Dictionary<string, string[]>
            {
                ["file"] = ["File phải có ít nhất một dòng dữ liệu sau header."]
            });
        }

        MarkDuplicateRows(rows);
        var studentCodes = rows
            .Where(x => x.Errors.Count == 0 && !x.IsDuplicate && x.NormalizedStudentCode is not null)
            .Select(x => x.NormalizedStudentCode!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var assessmentCodes = rows
            .Where(x => x.Errors.Count == 0 && !x.IsDuplicate && x.NormalizedAssessmentCode is not null)
            .Select(x => x.NormalizedAssessmentCode!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var students = await dbContext.Students.AsNoTracking()
            .ToListAsync(cancellationToken);
        var studentsByCode = students
            .Where(x => studentCodes.Contains(NormalizeStudentCode(x.StudentCode)))
            .GroupBy(x => NormalizeStudentCode(x.StudentCode)!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);

        var assessments = await dbContext.Assessments.AsNoTracking()
            .Where(x => assessmentCodes.Contains(x.Code))
            .ToListAsync(cancellationToken);
        var assessmentsByCode = assessments
            .GroupBy(x => x.Code, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);

        foreach (var row in rows.Where(x => x.Errors.Count == 0 && !x.IsDuplicate))
        {
            if (!studentsByCode.TryGetValue(row.NormalizedStudentCode!, out var matchedStudents))
            {
                row.Errors.Add($"Không tìm thấy học sinh có mã '{row.NormalizedStudentCode}'.");
            }
            else if (matchedStudents.Count > 1)
            {
                row.Errors.Add($"Mã học sinh '{row.NormalizedStudentCode}' khớp nhiều hồ sơ.");
            }
            else
            {
                row.Student = matchedStudents[0];
                if (row.Student.Status != StudentStatus.Active)
                    row.Errors.Add($"Học sinh '{row.NormalizedStudentCode}' đang ngừng hoạt động.");
                if (!string.IsNullOrWhiteSpace(row.StudentName) &&
                    !string.Equals(row.StudentName.Trim(), row.Student.FullName, StringComparison.Ordinal))
                {
                    row.Warnings.Add($"Tên học sinh trong file khác hồ sơ hiện tại: '{row.Student.FullName}'.");
                }
            }

            if (!assessmentsByCode.TryGetValue(row.NormalizedAssessmentCode!, out var matchedAssessments))
            {
                row.Errors.Add($"Không tìm thấy mục đánh giá có mã '{row.NormalizedAssessmentCode}'.");
            }
            else if (matchedAssessments.Count > 1)
            {
                row.Errors.Add($"Mã mục đánh giá '{row.NormalizedAssessmentCode}' khớp nhiều hồ sơ.");
            }
            else
            {
                row.Assessment = matchedAssessments[0];
            }
        }

        var candidateStudentIds = rows
            .Where(x => x.Errors.Count == 0 && !x.IsDuplicate && x.Student is not null)
            .Select(x => x.Student!.Id)
            .Distinct()
            .ToArray();
        var existingCandidates = candidateStudentIds.Length == 0
            ? new List<AssessmentSheet>()
            : await dbContext.AssessmentSheets.AsNoTracking()
                .Where(x => candidateStudentIds.Contains(x.StudentId))
                .ToListAsync(cancellationToken);

        var groups = rows
            .Where(x => x.Errors.Count == 0 && !x.IsDuplicate && x.Student is not null && x.Assessment is not null)
            .GroupBy(x => new ExcelImportGroupKey(x.Student!.Id, x.StartDate!.Value, x.DueDate!.Value))
            .Select(x => new ExcelImportGroup(x.Key, x.ToList(), x.First().Student!))
            .ToList();

        foreach (var group in groups)
        {
            var matches = existingCandidates
                .Where(x =>
                    x.StudentId == group.Key.StudentId &&
                    x.StartDate == group.Key.StartDate &&
                    x.DueDate == group.Key.DueDate)
                .ToList();
            if (matches.Count > 1)
            {
                foreach (var row in group.Rows)
                    row.Errors.Add("Có nhiều bảng đánh giá cùng học sinh và khoảng ngày, không thể xác định bảng cần cập nhật.");
                continue;
            }

            var existingSheet = matches.SingleOrDefault();
            if (existingSheet is null)
            {
                foreach (var row in group.Rows)
                    row.Action = "Created";
                continue;
            }

            if (existingSheet.AssessmentSheetStatus == AssessmentSheetStatus.Done)
            {
                foreach (var row in group.Rows)
                    row.Errors.Add("Bảng đánh giá hiện có đã Done, không thể import đè.");
                continue;
            }

            group.ExistingSheetId = existingSheet.Id;
            foreach (var row in group.Rows)
                row.Action = "Updated";
        }

        foreach (var row in rows.Where(x => x.Errors.Count > 0))
        {
            row.Action = "Invalid";
        }

        var validGroups = groups
            .Where(x => x.Rows.All(row => row.Errors.Count == 0))
            .ToList();
        return new ExcelImportPlan(fileName, rows, validGroups);
    }

    private static List<ExcelImportRow> ParseExcelImportRows(byte[] content)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var stream = new MemoryStream(content);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        if (!reader.Read())
        {
            throw new AppValidationException("File Excel import thiếu header.", new Dictionary<string, string[]>
            {
                ["file"] = ["Sheet đầu tiên phải có header row."]
            });
        }

        var headerMap = ReadHeaderMap(reader);
        var requiredHeaders = new[] { "planGrade", "planNote", "assessmentCode", "studentCode", "studentName", "startDate", "dueDate" };
        var missingHeaders = requiredHeaders
            .Where(x => !headerMap.ContainsKey(x))
            .ToArray();
        if (missingHeaders.Length > 0)
        {
            throw new AppValidationException("File Excel import thiếu cột bắt buộc.", new Dictionary<string, string[]>
            {
                ["headers"] = missingHeaders
            });
        }

        var rows = new List<ExcelImportRow>();
        var rowNumber = 1;
        while (reader.Read())
        {
            rowNumber++;
            var values = requiredHeaders.ToDictionary(x => x, x => ReadCellString(reader, headerMap[x]), StringComparer.Ordinal);
            var startDateValue = ReadCellValue(reader, headerMap["startDate"]);
            var dueDateValue = ReadCellValue(reader, headerMap["dueDate"]);
            if (values.Values.All(string.IsNullOrWhiteSpace) && startDateValue is null && dueDateValue is null)
                continue;

            var row = new ExcelImportRow
            {
                RowNumber = rowNumber,
                AssessmentCode = values["assessmentCode"],
                StudentCode = values["studentCode"],
                StudentName = values["studentName"],
                PlanGrade = NormalizeCellString(values["planGrade"]),
                PlanNote = NormalizeCellString(values["planNote"]),
                NormalizedAssessmentCode = NormalizeImportCode(values["assessmentCode"]),
                NormalizedStudentCode = NormalizeStudentCode(values["studentCode"])
            };

            if (row.NormalizedAssessmentCode is null)
                row.Errors.Add("assessmentCode là bắt buộc.");
            if (row.NormalizedStudentCode is null)
                row.Errors.Add("studentCode là bắt buộc.");
            if (!string.IsNullOrWhiteSpace(row.PlanGrade) &&
                !AssessmentSheetRules.TryParseGradeLabel(row.PlanGrade, out _))
            {
                row.Errors.Add("planGrade không hợp lệ.");
            }
            if (!TryParseImportDate(startDateValue, out var startDate))
                row.Errors.Add("startDate không hợp lệ.");
            else
                row.StartDate = startDate;
            if (!TryParseImportDate(dueDateValue, out var dueDate))
                row.Errors.Add("dueDate không hợp lệ.");
            else
                row.DueDate = dueDate;
            if (row.StartDate is not null && row.DueDate is not null && row.StartDate > row.DueDate)
                row.Errors.Add("startDate phải nhỏ hơn hoặc bằng dueDate.");

            rows.Add(row);
        }

        return rows;
    }

    private static Dictionary<string, int> ReadHeaderMap(IExcelDataReader reader)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var value = ReadCellString(reader, index);
            if (!string.IsNullOrWhiteSpace(value))
                headers.TryAdd(value.Trim(), index);
        }

        return headers;
    }

    private static void MarkDuplicateRows(List<ExcelImportRow> rows)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.NormalizedStudentCode is null ||
                row.NormalizedAssessmentCode is null ||
                row.StartDate is null ||
                row.DueDate is null)
            {
                continue;
            }

            var key = string.Join(
                "|",
                row.NormalizedStudentCode,
                row.StartDate.Value.ToUnixTimeSeconds(),
                row.DueDate.Value.ToUnixTimeSeconds(),
                row.NormalizedAssessmentCode);
            if (seen.Add(key))
                continue;

            row.IsDuplicate = true;
            row.Action = "SkippedDuplicate";
            row.Warnings.Add("Dòng trùng studentCode + startDate + dueDate + assessmentCode; hệ thống giữ dòng đầu tiên.");
        }
    }

    private static ImportAssessmentSheetsPreviewResponse BuildImportPreviewResponse(ExcelImportPlan plan)
    {
        var rows = plan.Rows
            .Select(x => new ImportAssessmentSheetsPreviewRowResponse(
                x.RowNumber,
                x.AssessmentCode,
                x.StudentCode,
                x.StudentName,
                x.StartDate,
                x.DueDate,
                x.PlanGrade,
                x.PlanNote,
                x.NormalizedAssessmentCode,
                x.NormalizedStudentCode,
                FormatImportDate(x.StartDate),
                FormatImportDate(x.DueDate),
                x.Action,
                x.Errors,
                x.Warnings))
            .ToList();
        var errorCount = plan.Rows.Count(x => x.Errors.Count > 0);
        var warningCount = plan.Rows.Count(x => x.Warnings.Count > 0);
        var summary = new ImportAssessmentSheetsPreviewSummaryResponse(
            plan.CanImport,
            plan.ValidRowCount,
            errorCount,
            warningCount,
            plan.SkippedDuplicateRowCount,
            plan.Groups.Count);
        return new ImportAssessmentSheetsPreviewResponse(summary, rows);
    }

    private static AppValidationException BuildImportValidationException(ExcelImportPlan plan)
    {
        var errors = plan.Rows
            .Where(x => x.Errors.Count > 0)
            .Select(x => $"Dòng {x.RowNumber}: {string.Join("; ", x.Errors)}")
            .ToArray();
        return new AppValidationException("File Excel import còn lỗi, không ghi dữ liệu.", new Dictionary<string, string[]>
        {
            ["rows"] = errors.Length == 0 ? ["File import không có dòng hợp lệ."] : errors
        });
    }

    private static AssessmentRecord BuildImportedRecord(
        AssessmentSheet sheet,
        Assessment assessment,
        AssessmentGrade? planGrade,
        string? planNode,
        DateTimeOffset now,
        Guid actorUserId) => new()
        {
            Id = Guid.NewGuid(),
            AssessmentSheetId = sheet.Id,
            AssessmentSheet = sheet,
            AssessmentRowIndex = assessment.RowIndex,
            AssessmentSnapshot = new AssessmentSnapshot
            {
                Code = assessment.Code,
                Name = assessment.Name,
                GroupLv1Name = assessment.GroupLv1Name,
                GroupLv2Name = assessment.GroupLv2Name,
                GroupLv3Name = assessment.GroupLv3Name,
                RowIndex = assessment.RowIndex
            },
            PlanGrade = planGrade,
            PlanNote = planNode,
            FinalGrade = null,
            FinalNote = null,
            UpdatedByUserId = actorUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static object BuildExcelImportAuditSnapshot(
        AssessmentSheet sheet,
        string fileName,
        long fileSizeBytes,
        string action,
        int recordCount,
        int skippedDuplicateRowCount) => new
        {
            AssessmentSheetId = sheet.Id,
            sheet.StudentId,
            StudentCode = sheet.StudentSnapshot.StudentCode,
            StudentName = sheet.StudentSnapshot.FullName,
            sheet.StartDate,
            sheet.DueDate,
            FileName = fileName,
            FileSizeBytes = fileSizeBytes,
            Action = action,
            RecordCount = recordCount,
            SkippedDuplicateRowCount = skippedDuplicateRowCount
        };

    private static object? ReadCellValue(IExcelDataReader reader, int index) =>
        index < reader.FieldCount ? reader.GetValue(index) : null;

    private static string? ReadCellString(IExcelDataReader reader, int index) =>
        ReadCellValue(reader, index)?.ToString()?.Trim();

    private static string? NormalizeCellString(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeImportCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeStudentCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static bool TryParseImportDate(object? value, out DateTimeOffset result)
    {
        result = default;
        if (value is null)
            return false;

        if (value is DateTime dateTime)
        {
            result = new DateTimeOffset(dateTime.Date, BusinessDateOffset).ToUniversalTime();
            return true;
        }

        if (value is double number)
        {
            result = new DateTimeOffset(DateTime.FromOADate(number).Date, BusinessDateOffset).ToUniversalTime();
            return true;
        }

        var text = value.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string[] dateOnlyFormats = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "M/d/yyyy"];
        if (DateOnly.TryParseExact(text, dateOnlyFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly) ||
            DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateOnly))
        {
            result = new DateTimeOffset(dateOnly.ToDateTime(TimeOnly.MinValue), BusinessDateOffset).ToUniversalTime();
            return true;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dateTimeOffset))
        {
            result = dateTimeOffset.ToUniversalTime();
            return true;
        }

        return false;
    }

    private static string? FormatImportDate(DateTimeOffset? value) =>
        value?.ToOffset(BusinessDateOffset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<List<AssessmentRecord>> LoadRecordEntitiesAsync(Guid sheetId, CancellationToken cancellationToken) =>
        await dbContext.AssessmentRecords.AsNoTracking()
            .Where(x => x.AssessmentSheetId == sheetId)
            .OrderBy(x => x.AssessmentRowIndex ?? int.MaxValue)
            .ToListAsync(cancellationToken);

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
        Guid[] assessmentIds,
        CancellationToken cancellationToken)
    {
        var assessments = await dbContext.Assessments.AsNoTracking()
            .Where(x => assessmentIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (assessments.Count != assessmentIds.Length)
            throw new NotFoundException("Không tìm thấy đủ mục đánh giá.", ProblemCodes.AssessmentNotFound);

        return assessments;
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
        Status = sheet.AssessmentSheetStatus.ToString(),
        sheet.StudentId,
        sheet.ResponsibleTeacherId,
        sheet.StartDate,
        sheet.DueDate,
        sheet.DoneDate,
        sheet.SubmissionDate
    };

    private static object SubmitResultsAuditSnapshot(AssessmentSheet sheet, int changedCellCount) => new
    {
        Status = sheet.AssessmentSheetStatus.ToString(),
        sheet.StudentId,
        StudentCode = sheet.StudentSnapshot.StudentCode,
        StudentName = sheet.StudentSnapshot.FullName,
        AssessmentSheetId = sheet.Id,
        sheet.StartDate,
        sheet.DueDate,
        sheet.SubmissionDate,
        ChangedCellCount = changedCellCount
    };

    private static object PdfUploadAuditSnapshot(
        AssessmentSheet sheet,
        string kind,
        string fileName,
        long fileSizeBytes,
        string? oldLink,
        string? newLink) => new
    {
        Kind = kind,
        AssessmentSheetId = sheet.Id,
        sheet.StudentId,
        StudentCode = sheet.StudentSnapshot.StudentCode,
        StudentName = sheet.StudentSnapshot.FullName,
        sheet.StartDate,
        sheet.DueDate,
        FileName = fileName,
        FileSizeBytes = fileSizeBytes,
        OldLink = oldLink,
        NewLink = newLink
    };

    private static object ResultSourceCellAuditSnapshot(
        AssessmentSheet sheet,
        ResultSourceCellUpdate update,
        string? value,
        DateTimeOffset submittedAt) => new
    {
        update.SpreadsheetId,
        update.SheetName,
        update.Cell,
        update.Row,
        update.Column,
        update.Kind,
        Value = value,
        update.StudentCode,
        StudentName = sheet.StudentSnapshot.FullName,
        sheet.StudentId,
        AssessmentSheetId = sheet.Id,
        sheet.StartDate,
        sheet.DueDate,
        update.AssessmentCode,
        update.AssessmentName,
        update.FinalGrade,
        update.FinalGradeLabel,
        update.FinalNote,
        SubmittedAt = submittedAt
    };

    private static IQueryable<AssessmentSheetListItemResponse> ProjectList(IQueryable<AssessmentSheet> query) =>
        query.Select(x => new AssessmentSheetListItemResponse(
            x.Id,
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
            x.PlanFileLinkPdf,
            x.ResultFileLinkPdf,
            x.CreatedAt,
            x.UpdatedAt));

    private static bool Matches(AssessmentSheetListItemResponse item, string foldedSearch) =>
        VietnameseSearchNormalizer.Fold(item.StudentCode).Contains(foldedSearch, StringComparison.Ordinal) ||
        VietnameseSearchNormalizer.Fold(item.StudentFullName).Contains(foldedSearch, StringComparison.Ordinal);

    private static IOrderedQueryable<AssessmentSheet> ApplySheetSort(
        IQueryable<AssessmentSheet> query,
        string sortBy,
        bool descending) => (sortBy.ToLowerInvariant(), descending) switch
        {
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
            { ["sortBy"] = ["Chỉ hỗ trợ status, startDate, dueDate, updatedAt hoặc createdAt."] })
        };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTimeOffset? NormalizeTimestamp(DateTimeOffset? value) =>
        value?.ToUniversalTime();

    private static StudentSnapshot Snapshot(Student student) => new()
    {
        StudentCode = student.StudentCode,
        FullName = student.FullName,
        NickName = student.NickName,
        DateOfBirth = student.DateOfBirth,
        Gender = student.Gender
    };

    private static AssessmentSheetStudentSnapshotResponse ToResponse(StudentSnapshot snapshot) =>
        new(snapshot.StudentCode, snapshot.FullName, snapshot.NickName, snapshot.DateOfBirth, snapshot.Gender);

    private static AssessmentSnapshotResponse ToResponse(AssessmentSnapshot snapshot) =>
        new(snapshot.Code, snapshot.Name, snapshot.GroupLv1Name, snapshot.GroupLv2Name, snapshot.GroupLv3Name, snapshot.RowIndex);

    private static NotFoundException AssessmentSheetNotFound() =>
        new("Không tìm thấy bảng đánh giá.", ProblemCodes.AssessmentSheetNotFound);
}
