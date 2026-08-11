using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.Attendance;

public static class AttendanceRules
{
    public static void ValidateRecords(IReadOnlyList<AttendanceRecordRequest> records, bool allowEmpty = false)
    {
        var errors = new Dictionary<string, string[]>();
        if (records.Count > 100 || (!allowEmpty && records.Count < 1))
            errors["records"] = ["Danh sách phải có từ 1 đến 100 học sinh."];
        var duplicates = records.GroupBy(x => x.StudentId).Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet();
        for (var index = 0; index < records.Count; index++)
        {
            var item = records[index];
            if (item.StudentId == Guid.Empty) errors[$"records[{index}].studentId"] = ["studentId là bắt buộc."];
            if (duplicates.Contains(item.StudentId)) errors[$"records[{index}].studentId"] = ["studentId bị trùng."];
            if (item.Notes?.Trim().Length > 2000) errors[$"records[{index}].notes"] = ["Ghi chú tối đa 2000 ký tự."];
            var invalid = item.Status switch
            {
                AttendanceStatus.Present => item.HalfDayPart is not null || item.IsExcused is not null || item.DurationMinutes is not null,
                AttendanceStatus.AbsentFullDay => item.HalfDayPart is not null || item.IsExcused is null || item.DurationMinutes is not null,
                AttendanceStatus.AbsentHalfDay => item.HalfDayPart is null || item.IsExcused is null || item.DurationMinutes is not null,
                AttendanceStatus.OneToOneHour => item.HalfDayPart is not null || item.IsExcused is not null || item.DurationMinutes != 60,
                _ => true
            };
            if (invalid) errors[$"records[{index}].status"] = ["Các trường điều kiện không khớp trạng thái điểm danh."];
        }
        if (errors.Count > 0) throw new AppValidationException("Dữ liệu điểm danh không hợp lệ.", errors);
    }

    public static bool CanTeacherEdit(DateOnly attendanceDate, DateOnly serverDate, int editWindowDays)
    {
        if (editWindowDays is < 1 or > 7 || attendanceDate > serverDate) return false;
        return attendanceDate >= serverDate.AddDays(-(editWindowDays - 1));
    }
}
