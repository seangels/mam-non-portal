using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace AdminPortal.Infrastructure.Attendance;

public sealed class BusinessDateProvider : IBusinessDateProvider
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public BusinessDateProvider(TimeProvider timeProvider, IOptions<AttendanceOptions> options)
    {
        _timeProvider = timeProvider;
        _timeZone = ResolveTimeZone(options.Value.BusinessTimeZone);
    }

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone).DateTime);

    public DateTimeOffset EndOfDayUtc(DateOnly attendanceDate)
    {
        var localEndExclusive = attendanceDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEndExclusive, _timeZone));
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException) when (id == "Asia/Ho_Chi_Minh")
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
