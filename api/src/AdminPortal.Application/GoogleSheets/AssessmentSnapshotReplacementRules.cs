using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.GoogleSheets;

/// <summary>
/// Logic thuần cho tùy chọn 2 của popup đồng bộ Google Sheets: xác thực cấu hình người dùng chọn và
/// ghi đè các trường snapshot trên <see cref="AssessmentRecord"/> từ catalog <see cref="Assessment"/> vừa đồng bộ.
/// Tách khỏi <see cref="GoogleSheetsService"/> để test được mà không cần Google/DB.
/// </summary>
public static class AssessmentSnapshotReplacementRules
{
    public static readonly IReadOnlyList<AssessmentSheetStatus> AllSheetStatuses =
        [AssessmentSheetStatus.Open, AssessmentSheetStatus.Planed, AssessmentSheetStatus.Done, AssessmentSheetStatus.Canceled];

    /// <summary>
    /// Phải chọn ít nhất một trường và một trạng thái bảng; nếu không thì không có gì để làm.
    /// </summary>
    public static void Validate(AssessmentRecordSnapshotReplacement spec)
    {
        var errors = new Dictionary<string, string[]>();
        if (!spec.HasAnyField)
            errors["replaceRecordSnapshots.fields"] = ["Chọn ít nhất một trường để thay thế."];
        if (spec.SheetStatuses is null || spec.SheetStatuses.Count == 0)
            errors["replaceRecordSnapshots.sheetStatuses"] = ["Chọn ít nhất một trạng thái bảng đánh giá."];
        if (errors.Count > 0)
            throw new AppValidationException("Cấu hình thay thế snapshot không hợp lệ.", errors);
    }

    /// <summary>
    /// Ghi đè các trường đã chọn vào <see cref="AssessmentRecord.AssessmentSnapshot"/> cho từng bản ghi
    /// có mã khớp một <see cref="Assessment"/> trong <paramref name="assessmentByCode"/>. Chỉ đóng dấu
    /// <c>UpdatedAt</c>/<c>UpdatedByUserId</c> khi có giá trị thực sự thay đổi. Trả về số bản ghi đã đổi.
    /// </summary>
    public static int Apply(
        IEnumerable<AssessmentRecord> records,
        IReadOnlyDictionary<string, Assessment> assessmentByCode,
        AssessmentRecordSnapshotReplacement spec,
        DateTimeOffset now,
        Guid actorUserId)
    {
        var replaced = 0;
        foreach (var record in records)
        {
            var snapshot = record.AssessmentSnapshot;
            if (snapshot?.Code is null || !assessmentByCode.TryGetValue(snapshot.Code, out var assessment))
                continue;

            var changed = false;
            if (spec.Name && snapshot.Name != assessment.Name)
            {
                snapshot.Name = assessment.Name;
                changed = true;
            }
            if (spec.GroupLv1Name && snapshot.GroupLv1Name != assessment.GroupLv1Name)
            {
                snapshot.GroupLv1Name = assessment.GroupLv1Name;
                changed = true;
            }
            if (spec.GroupLv2Name && snapshot.GroupLv2Name != assessment.GroupLv2Name)
            {
                snapshot.GroupLv2Name = assessment.GroupLv2Name;
                changed = true;
            }
            if (spec.GroupLv3Name && snapshot.GroupLv3Name != assessment.GroupLv3Name)
            {
                snapshot.GroupLv3Name = assessment.GroupLv3Name;
                changed = true;
            }
            if (spec.RowIndex && snapshot.RowIndex != assessment.RowIndex)
            {
                snapshot.RowIndex = assessment.RowIndex;
                changed = true;
            }

            if (!changed)
                continue;

            record.UpdatedAt = now;
            record.UpdatedByUserId = actorUserId;
            replaced++;
        }

        return replaced;
    }
}
