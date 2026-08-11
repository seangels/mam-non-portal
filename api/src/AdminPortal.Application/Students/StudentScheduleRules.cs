using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.Students;

public static class StudentScheduleRules
{
    public const short AllWeekdaysMask = 63;

    public static short Encode(StudyScheduleRequest? schedule)
    {
        if (schedule is null)
        {
            throw Validation("studySchedule", "Lịch học là bắt buộc.");
        }

        if (!Enum.IsDefined(schedule.Mode))
        {
            throw Validation("studySchedule.mode", "Hình thức học không hợp lệ.");
        }

        if (schedule.Weekdays is null || schedule.Weekdays.Count is < 1 or > 6)
        {
            throw Validation("studySchedule.weekdays", "Lịch học phải có từ 1 đến 6 ngày.");
        }

        short mask = 0;
        foreach (var weekday in schedule.Weekdays)
        {
            var bit = ToMask(weekday);
            if ((mask & bit) != 0)
            {
                throw Validation("studySchedule.weekdays", "Ngày học không được trùng nhau.");
            }

            mask |= bit;
        }

        return mask;
    }

    public static short ToMask(StudyWeekday weekday) => weekday switch
    {
        StudyWeekday.Monday => 1,
        StudyWeekday.Tuesday => 2,
        StudyWeekday.Wednesday => 4,
        StudyWeekday.Thursday => 8,
        StudyWeekday.Friday => 16,
        StudyWeekday.Saturday => 32,
        _ => throw Validation("studySchedule.weekdays", "Ngày học không hợp lệ.")
    };

    public static short ToMask(DayOfWeek weekday) => weekday switch
    {
        DayOfWeek.Monday => 1,
        DayOfWeek.Tuesday => 2,
        DayOfWeek.Wednesday => 4,
        DayOfWeek.Thursday => 8,
        DayOfWeek.Friday => 16,
        DayOfWeek.Saturday => 32,
        _ => 0
    };

    public static IReadOnlyList<StudyWeekday> Decode(short mask)
    {
        if (mask is < 1 or > AllWeekdaysMask)
        {
            throw new InvalidOperationException("Stored study weekday mask is invalid.");
        }

        return Enum.GetValues<StudyWeekday>()
            .Where(weekday => (mask & ToMask(weekday)) != 0)
            .ToArray();
    }

    private static AppValidationException Validation(string field, string message) =>
        new(message, new Dictionary<string, string[]> { [field] = [message] });
}
