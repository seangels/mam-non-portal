using AdminPortal.Application.Common.Exceptions;

namespace AdminPortal.Application.Students;

public static class StudentRules
{
    public static bool AffectsAttendanceSnapshot(IEnumerable<string> changedFields)
    {
        ArgumentNullException.ThrowIfNull(changedFields);
        return changedFields.Any(field => field is
            "studentCode" or
            "fullName" or
            "nickName" or
            "status" or
            "studySchedule.mode" or
            "studySchedule.weekdays");
    }

    public static void ValidateDateOfBirth(DateOnly value, DateOnly today)
    {
        if (value == default)
        {
            throw new AppValidationException("Ngày sinh là bắt buộc.", new Dictionary<string, string[]>
            {
                ["dateOfBirth"] = ["Ngày sinh là bắt buộc."]
            });
        }

        if (value > today)
        {
            throw new AppValidationException("Ngày sinh không hợp lệ.", new Dictionary<string, string[]>
            {
                ["dateOfBirth"] = ["Ngày sinh không được lớn hơn ngày hiện tại."]
            });
        }
    }
}
