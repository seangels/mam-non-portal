import {
  AttendanceStatus,
  Gender,
  HalfDayPart,
  SheetState,
  SnapshotSource,
  StudyMode,
  StudyWeekday,
  StudentStatus,
  UserRole,
  UserStatus
} from '../models/api.models';

export const ROLE_LABELS: Record<UserRole, string> = {
  SuperAdmin: 'Siêu quản trị viên',
  Admin: 'Quản trị viên',
  Teacher: 'Giáo viên'
};

export const USER_STATUS_LABELS: Record<UserStatus, string> = {
  Active: 'Đang hoạt động',
  Inactive: 'Ngừng hoạt động',
  Locked: 'Đã khóa'
};

export const STUDENT_STATUS_LABELS: Record<StudentStatus, string> = {
  Active: 'Đang học',
  Inactive: 'Ngừng học'
};

export const GENDER_LABELS: Record<Gender, string> = {
  Male: 'Nam',
  Female: 'Nữ',
  Other: 'Khác'
};

export const STUDY_MODE_LABELS: Record<StudyMode, string> = {
  OneToOne: 'Học 1-1',
  FullDay: 'Học cả ngày'
};

export const STUDY_WEEKDAY_LABELS: Record<StudyWeekday, string> = {
  Monday: 'Thứ Hai',
  Tuesday: 'Thứ Ba',
  Wednesday: 'Thứ Tư',
  Thursday: 'Thứ Năm',
  Friday: 'Thứ Sáu',
  Saturday: 'Thứ Bảy'
};

export const STUDY_WEEKDAY_SHORT_LABELS: Record<StudyWeekday, string> = {
  Monday: 'T2',
  Tuesday: 'T3',
  Wednesday: 'T4',
  Thursday: 'T5',
  Friday: 'T6',
  Saturday: 'T7'
};

export const ATTENDANCE_STATUS_LABELS: Record<AttendanceStatus, string> = {
  Present: 'Có mặt',
  AbsentFullDay: 'Vắng nguyên buổi',
  AbsentHalfDay: 'Vắng 1/2 buổi',
  OneToOneHour: 'Học 1-1 (1 giờ)',
  Unmarked: 'Chưa điểm danh'
};

export const HALF_DAY_LABELS: Record<HalfDayPart, string> = {
  Morning: 'Buổi sáng',
  Afternoon: 'Buổi chiều'
};

export const SHEET_STATE_LABELS: Record<SheetState, string> = {
  Missing: 'Chưa lưu',
  Saved: 'Đã lưu'
};

export const SNAPSHOT_SOURCE_LABELS: Record<SnapshotSource, string> = {
  CurrentSnapshot: 'Dữ liệu hiện tại',
  HistoricalRecovery: 'Phục hồi lịch sử'
};

export const READ_ONLY_REASON_LABELS: Record<string, string> = {
  HistoricalSnapshotUnavailable: 'Không thể xác minh danh sách học sinh của ngày này từ dữ liệu hiện tại.',
  AttendanceEditWindowExceeded: 'Ngày này nằm ngoài thời hạn chỉnh sửa điểm danh của giáo viên.',
  FutureDate: 'Không thể điểm danh cho ngày trong tương lai.',
  GroupInactive: 'Nhóm hiện không hoạt động.',
  ResponsibleTeacherRequired: 'Nhóm chưa có giáo viên phụ trách.',
  NotResponsibleTeacher: 'Bạn không còn phụ trách nhóm này.',
  NoScheduledStudents: 'Không có học sinh có lịch học trong ngày này.',
  ReadOnly: 'Phiếu hiện chỉ có thể xem.'
};

export const API_ERROR_CODE_LABELS: Record<string, string> = {
  SnapshotChanged: 'Danh sách hoặc thông tin nhóm đã thay đổi. Vui lòng tải lại trước khi lưu.',
  AttendanceSheetAlreadyExists: 'Phiếu điểm danh đã được người khác tạo. Vui lòng tải lại.',
  SheetVersionConflict: 'Phiếu đã được người khác cập nhật. Vui lòng tải lại để xem dữ liệu mới nhất.',
  HistoricalSnapshotUnavailable: 'Không thể xác minh danh sách học sinh của ngày này từ dữ liệu hiện tại.',
  ResponsibleTeacherRequired: 'Nhóm cần có giáo viên phụ trách trước khi điểm danh.',
  GroupInactive: 'Nhóm hiện không hoạt động.',
  AttendanceRosterMismatch: 'Danh sách học sinh đã thay đổi hoặc không khớp với phiếu. Vui lòng tải lại.',
  AttendanceEditWindowExceeded: 'Ngày này nằm ngoài thời hạn chỉnh sửa điểm danh của bạn.',
  GroupCapacityExceeded: 'Nhóm đã đủ 100 học sinh.',
  FutureAttendanceDate: 'Không thể điểm danh cho ngày trong tương lai.',
  StudentAlreadyRecordedToday: 'Không thể chuyển nhóm vì học sinh đã có trong phiếu điểm danh hôm nay.',
  TeacherHasResponsibleGroups: 'Cần gỡ giáo viên khỏi các nhóm đang phụ trách trước khi xóa.',
  StudentHasCurrentGroup: 'Cần gỡ học sinh khỏi nhóm trước khi ngừng hoạt động hoặc xóa.',
  StudentInactive: 'Chỉ có thể phân nhóm học sinh đang hoạt động.',
  StudentNotFound: 'Không tìm thấy học sinh hoặc hồ sơ đã bị xóa.',
  StudentVersionConflict: 'Thông tin học sinh đã được người khác cập nhật. Vui lòng tải dữ liệu mới nhất.',
  NoScheduledStudents: 'Không có học sinh có lịch học trong ngày này.',
  GroupHasResponsibleTeacher: 'Cần gỡ giáo viên phụ trách trước khi xóa nhóm.',
  GroupHasStudents: 'Cần gỡ toàn bộ học sinh trước khi xóa nhóm.',
  TeacherNotFound: 'Không tìm thấy giáo viên hoặc hồ sơ đã bị xóa.',
  TeacherCodeAlreadyExists: 'Mã giáo viên đã tồn tại.',
  TeacherVersionConflict: 'Thông tin giáo viên đã được người khác cập nhật. Vui lòng tải dữ liệu mới nhất.',
  TeacherMustBeManagedViaTeachers: 'Tài khoản giáo viên phải được quản lý tại mục Giáo viên.',
  InvalidAttendanceEditWindow: 'Thời hạn sửa điểm danh phải từ 1 đến 7 ngày.',
  EmailAlreadyExists: 'Email đã được sử dụng.',
  HistoricalRecoveryNotAllowed: 'Không thể dùng khôi phục lịch sử khi vẫn có thể tạo phiếu theo quy trình thông thường.',
  DuplicateGroupCode: 'Mã nhóm đã tồn tại.',
  ValidationFailed: 'Một hoặc nhiều thông tin chưa hợp lệ.'
};

export function labelOrFallback<T extends string>(labels: Partial<Record<T, string>>, value: T | null | undefined): string {
  return value ? labels[value] ?? 'Không xác định' : '—';
}

export function readOnlyReasonLabel(reason: string | null | undefined): string {
  return reason ? READ_ONLY_REASON_LABELS[reason] ?? 'Bạn không thể chỉnh sửa dữ liệu này.' : '';
}

export function apiErrorCodeLabel(code: string | null | undefined): string | null {
  return code ? API_ERROR_CODE_LABELS[code] ?? null : null;
}
