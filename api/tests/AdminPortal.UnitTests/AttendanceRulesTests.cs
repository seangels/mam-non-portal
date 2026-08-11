using AdminPortal.Application.Attendance;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Domain.Enums;
using AdminPortal.Infrastructure.Attendance;
using AdminPortal.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace AdminPortal.UnitTests;

public sealed class AttendanceRulesTests
{
    public static TheoryData<AttendanceRecordRequest> ValidRecords => new()
    {
        new(Guid.NewGuid(), AttendanceStatus.Present, null, null, null, "ghi chú"),
        new(Guid.NewGuid(), AttendanceStatus.AbsentFullDay, null, true, null, null),
        new(Guid.NewGuid(), AttendanceStatus.AbsentHalfDay, HalfDayPart.Morning, false, null, null),
        new(Guid.NewGuid(), AttendanceStatus.OneToOneHour, null, null, 60, null)
    };

    [Theory]
    [MemberData(nameof(ValidRecords))]
    public void FourAttendanceStatesAcceptTheirExactConditionalFields(AttendanceRecordRequest record) =>
        AttendanceRules.ValidateRecords([record]);

    [Fact]
    public void InvalidConditionalFieldsReturnCardPath()
    {
        var exception = Assert.Throws<AppValidationException>(() => AttendanceRules.ValidateRecords([
            new AttendanceRecordRequest(Guid.NewGuid(), AttendanceStatus.AbsentHalfDay, null, true, null, null)
        ]));
        Assert.Contains("records[0].status", exception.Errors.Keys);
    }

    [Fact]
    public void DuplicateStudentIsRejected()
    {
        var id = Guid.NewGuid();
        var exception = Assert.Throws<AppValidationException>(() => AttendanceRules.ValidateRecords([
            new AttendanceRecordRequest(id, AttendanceStatus.Present, null, null, null, null),
            new AttendanceRecordRequest(id, AttendanceStatus.Present, null, null, null, null)
        ]));
        Assert.Contains("records[1].studentId", exception.Errors.Keys);
    }

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(1, -1, false)]
    [InlineData(7, -6, true)]
    [InlineData(7, -7, false)]
    [InlineData(7, 1, false)]
    public void TeacherWindowUsesInclusiveLocalCalendarDays(int windowDays, int offsetDays, bool expected)
    {
        var today = new DateOnly(2026, 8, 11);
        Assert.Equal(expected, AttendanceRules.CanTeacherEdit(today.AddDays(offsetDays), today, windowDays));
    }

    [Fact]
    public void BusinessDateUsesHoChiMinhBoundary()
    {
        var provider = new BusinessDateProvider(
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 10, 17, 30, 0, TimeSpan.Zero)),
            Options.Create(new AttendanceOptions()));
        Assert.Equal(new DateOnly(2026, 8, 11), provider.Today);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 11, 17, 0, 0, TimeSpan.Zero),
            provider.EndOfDayUtc(new DateOnly(2026, 8, 11)));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
