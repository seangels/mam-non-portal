using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.AssessmentSheets;

public static class AssessmentSheetRules
{
    public static void EnsureAssessmentSheetRole(ActorContext actor)
    {
        if (actor.Role is not (UserRole.SuperAdmin or UserRole.Admin or UserRole.Teacher))
            throw new ForbiddenException("Không đủ quyền.");
    }

    public static void EnsureOpen(AssessmentSheet sheet)
    {
        if (sheet.AssessmentSheetStatus == AssessmentSheetStatus.Done)
            throw new ConflictException("Bảng đánh giá đã hoàn thành, không thể chỉnh sửa.", ProblemCodes.AssessmentSheetDone);
    }

    public static void EnsureDistinctIds(IReadOnlyCollection<Guid> ids, string field)
    {
        if (ids.Count == 0 || ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            throw new AppValidationException("Danh sách mục đánh giá không hợp lệ.", new Dictionary<string, string[]>
            { [field] = ["Danh sách phải có ít nhất một mục hợp lệ và không được trùng."] });
    }

    public static int GradeRank(AssessmentGrade grade) => grade switch
    {
        AssessmentGrade.A => 4,
        AssessmentGrade.B => 3,
        AssessmentGrade.C => 2,
        AssessmentGrade.D => 1,
        _ => 0
    };

    /// <summary>
    /// Nhãn hiển thị của FinalGrade/PlanGrade, đã xác nhận với người dùng ở requirements 09 mục 11
    /// (lưu ý cặp B chưa đối xứng với A/C/D, đội vận hành cần xác nhận lại trước khi dùng ghi dữ liệu thật).
    /// </summary>
    public static string GradeLabel(AssessmentGrade grade) => grade switch
    {
        AssessmentGrade.A => "Đạt +",
        AssessmentGrade.B => "Chưa đạt -",
        AssessmentGrade.C => "Hỗ trợ +",
        AssessmentGrade.D => "Hỗ trợ -",
        _ => grade.ToString()
    };

    /// <summary>Chiều ngược của GradeLabel — dùng khi đọc nhãn kết quả từ Google Sheet (cột ket_qua của _data_DG).</summary>
    public static bool TryParseGradeLabel(string label, out AssessmentGrade grade)
    {
        foreach (var candidate in Enum.GetValues<AssessmentGrade>())
        {
            if (string.Equals(GradeLabel(candidate), label, StringComparison.Ordinal))
            {
                grade = candidate;
                return true;
            }
        }

        grade = default;
        return false;
    }

    public static List<AssessmentRecord> BuildRecords(
        Guid sheetId,
        IReadOnlyCollection<Assessment> assessments,
        IReadOnlyList<CreateAssessmentSheetRecordRequest> requestRecords,
        DateTimeOffset now,
        Guid actorUserId)
    {
        var assessmentById = assessments.ToDictionary(x => x.Id);
        return requestRecords.Select(requestRecord =>
        {
            var assessment = assessmentById[requestRecord.AssessmentId];
            return new AssessmentRecord
            {
                Id = Guid.NewGuid(),
                AssessmentSheetId = sheetId,
                AssessmentSheet = null!,
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
                PlanGrade = requestRecord.LatestGrade,
                PlanNote = NormalizeOptional(requestRecord.Note),
                FinalGrade = null,
                FinalNote = null,
                UpdatedByUserId = actorUserId,
                CreatedAt = now,
                UpdatedAt = now
            };
        }).ToList();
    }

    public static AssessmentRecord BuildReplacementRecord(
        AssessmentSheet sheet,
        Assessment assessment,
        AssessmentSheetRecordRequest requestRecord,
        DateTimeOffset now,
        Guid actorUserId) => new()
        {
            Id = Guid.NewGuid(),
            AssessmentSheetId = sheet.Id,
            AssessmentSheet = sheet,
            AssessmentRowIndex = assessment.RowIndex,
            // STT hiển thị do người dùng chỉnh trên form; null nghĩa là chưa đặt (UI tự đếm theo nhóm lv3).
            DisplayOrder = requestRecord.DisplayOrder,
            AssessmentSnapshot = new AssessmentSnapshot
            {
                Code = assessment.Code,
                Name = assessment.Name,
                GroupLv1Name = assessment.GroupLv1Name,
                GroupLv2Name = assessment.GroupLv2Name,
                GroupLv3Name = assessment.GroupLv3Name,
                RowIndex = assessment.RowIndex
            },
            PlanGrade = requestRecord.PlanGrade,
            PlanNote = NormalizeOptional(requestRecord.PlanNote),
            FinalGrade = requestRecord.FinalGrade,
            FinalNote = NormalizeOptional(requestRecord.FinalNote),
            UpdatedByUserId = actorUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
