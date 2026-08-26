namespace AdminPortal.Application.Common;

public static class ProblemCodes
{
    public const string ValidationFailed = nameof(ValidationFailed);
    public const string EmailAlreadyExists = nameof(EmailAlreadyExists);
    public const string TeacherNotFound = nameof(TeacherNotFound);
    public const string TeacherCodeAlreadyExists = nameof(TeacherCodeAlreadyExists);
    public const string TeacherVersionConflict = nameof(TeacherVersionConflict);
    public const string TeacherMustBeManagedViaTeachers = nameof(TeacherMustBeManagedViaTeachers);
    public const string InvalidAttendanceEditWindow = nameof(InvalidAttendanceEditWindow);
    public const string SnapshotChanged = nameof(SnapshotChanged);
    public const string AttendanceSheetAlreadyExists = nameof(AttendanceSheetAlreadyExists);
    public const string SheetVersionConflict = nameof(SheetVersionConflict);
    public const string HistoricalSnapshotUnavailable = nameof(HistoricalSnapshotUnavailable);
    public const string ResponsibleTeacherRequired = nameof(ResponsibleTeacherRequired);
    public const string GroupInactive = nameof(GroupInactive);
    public const string AttendanceRosterMismatch = nameof(AttendanceRosterMismatch);
    public const string AttendanceEditWindowExceeded = nameof(AttendanceEditWindowExceeded);
    public const string FutureAttendanceDate = nameof(FutureAttendanceDate);
    public const string GroupCapacityExceeded = nameof(GroupCapacityExceeded);
    public const string GroupHasResponsibleTeacher = nameof(GroupHasResponsibleTeacher);
    public const string GroupHasStudents = nameof(GroupHasStudents);
    public const string TeacherHasResponsibleGroups = nameof(TeacherHasResponsibleGroups);
    public const string StudentHasCurrentGroup = nameof(StudentHasCurrentGroup);
    public const string StudentAlreadyRecordedToday = nameof(StudentAlreadyRecordedToday);
    public const string StudentInactive = nameof(StudentInactive);
    public const string StudentNotFound = nameof(StudentNotFound);
    public const string StudentDriveFolderRequired = nameof(StudentDriveFolderRequired);
    public const string StudentVersionConflict = nameof(StudentVersionConflict);
    public const string NoScheduledStudents = nameof(NoScheduledStudents);
    public const string HistoricalRecoveryNotAllowed = nameof(HistoricalRecoveryNotAllowed);
    public const string AssessmentNotFound = nameof(AssessmentNotFound);
    public const string AssessmentGroupNotFound = nameof(AssessmentGroupNotFound);
    public const string AssessmentSheetNotFound = nameof(AssessmentSheetNotFound);
    public const string AssessmentSheetAlreadyExists = nameof(AssessmentSheetAlreadyExists);
    public const string AssessmentSheetDone = nameof(AssessmentSheetDone);
    public const string AssessmentSheetGoogleMappingBlocked = nameof(AssessmentSheetGoogleMappingBlocked);
    public const string AssessmentSheetGoogleOperationFailed = nameof(AssessmentSheetGoogleOperationFailed);
}
