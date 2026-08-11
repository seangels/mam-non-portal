using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Students;
using AdminPortal.Domain.Enums;

namespace AdminPortal.UnitTests;

public sealed class StudentScheduleRulesTests
{
    [Fact]
    public void EncodeAndDecodeUseCanonicalMondayToSaturdayOrder()
    {
        var mask = StudentScheduleRules.Encode(new StudyScheduleRequest(
            StudyMode.OneToOne,
            [StudyWeekday.Saturday, StudyWeekday.Monday, StudyWeekday.Wednesday]));

        Assert.Equal(37, mask);
        Assert.Equal(
            [StudyWeekday.Monday, StudyWeekday.Wednesday, StudyWeekday.Saturday],
            StudentScheduleRules.Decode(mask));
    }

    [Fact]
    public void DuplicateWeekdayReturnsNestedValidationPath()
    {
        var exception = Assert.Throws<AppValidationException>(() => StudentScheduleRules.Encode(
            new StudyScheduleRequest(StudyMode.FullDay, [StudyWeekday.Monday, StudyWeekday.Monday])));

        Assert.Contains("studySchedule.weekdays", exception.Errors.Keys);
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, 1)]
    [InlineData(DayOfWeek.Saturday, 32)]
    [InlineData(DayOfWeek.Sunday, 0)]
    public void CalendarDayUsesExpectedMask(DayOfWeek weekday, short expected) =>
        Assert.Equal(expected, StudentScheduleRules.ToMask(weekday));
}
