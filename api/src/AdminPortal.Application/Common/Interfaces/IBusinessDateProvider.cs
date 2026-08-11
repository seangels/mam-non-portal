namespace AdminPortal.Application.Common.Interfaces;

public interface IBusinessDateProvider
{
    DateOnly Today { get; }
    DateTimeOffset EndOfDayUtc(DateOnly attendanceDate);
}
