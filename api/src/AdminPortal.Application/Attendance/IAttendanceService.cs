using AdminPortal.Application.Common.Models;

namespace AdminPortal.Application.Attendance;

public interface IAttendanceService
{
    Task<AttendanceContextResponse> GetContextAsync(DateOnly attendanceDate, CancellationToken cancellationToken);
    Task<AttendanceDailyResponse> GetDailyAsync(DateOnly attendanceDate, Guid? groupId, CancellationToken cancellationToken);
    Task<AttendanceDailyResponse> CreateAsync(CreateAttendanceSheetRequest request, CancellationToken cancellationToken);
    Task<AttendanceDailyResponse> UpdateAsync(Guid sheetId, UpdateAttendanceSheetRequest request, CancellationToken cancellationToken);
    Task<AttendanceDailyResponse> RecoverAsync(HistoricalRecoveryRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<HistoricalGroupCandidateResponse>> ListGroupCandidatesAsync(CandidateListQuery query, CancellationToken cancellationToken);
    Task<PagedResponse<HistoricalStudentCandidateResponse>> ListStudentCandidatesAsync(CandidateListQuery query, CancellationToken cancellationToken);
    Task<PagedResponse<HistoricalTeacherCandidateResponse>> ListTeacherCandidatesAsync(CandidateListQuery query, CancellationToken cancellationToken);
}
