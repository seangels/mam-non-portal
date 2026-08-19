export type UserRole = 'SuperAdmin' | 'Admin' | 'Teacher';
export type UserStatus = 'Active' | 'Inactive' | 'Locked';
export type StudentStatus = 'Active' | 'Inactive';
export type Gender = 'Male' | 'Female' | 'Other';
export type StudyMode = 'OneToOne' | 'FullDay';
export type StudyWeekday = 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday';
export type SortOrder = 'asc' | 'desc';
export type StudentGroupStatus = 'Active' | 'Inactive';
export type AttendanceStatus = 'Present' | 'AbsentFullDay' | 'AbsentHalfDay' | 'OneToOneHour' | 'Unmarked';
export type HalfDayPart = 'Morning' | 'Afternoon';
export type SheetState = 'Missing' | 'Saved';
export type SnapshotSource = 'CurrentSnapshot' | 'HistoricalRecovery';

export interface Pagination {
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface PagedResponse<T> {
  items: T[];
  pagination: Pagination;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
  code?: string;
  currentVersion?: number;
}

export interface ListQuery {
  page: number;
  pageSize: number;
  search?: string;
  sortBy?: string;
  sortOrder?: SortOrder;
}

export interface UserListQuery extends ListQuery {
  status?: UserStatus;
  role?: UserRole;
  createdFrom?: string;
  createdTo?: string;
}

export interface User {
  id: string;
  email: string;
  fullName: string;
  phoneNumber?: string | null;
  role: UserRole;
  status: UserStatus;
  createdAt: string;
  updatedAt: string;
}

export interface CreateUserRequest {
  email: string;
  fullName: string;
  phoneNumber?: string | null;
  role: 'Admin';
  status: UserStatus;
  password: string;
}

export type UpdateUserRequest = Omit<CreateUserRequest, 'password'>;

export interface ChangeUserPasswordRequest {
  password: string;
}

export interface StudentListQuery extends ListQuery {
  status?: StudentStatus;
  gender?: Gender;
  dateOfBirthFrom?: string;
  dateOfBirthTo?: string;
  groupId?: string;
  unassigned?: boolean;
  studyMode?: StudyMode;
  studyWeekday?: StudyWeekday;
}

export interface StudySchedule {
  mode: StudyMode;
  weekdays: StudyWeekday[];
}

export interface Student {
  id: string;
  studentCode: string;
  fullName: string;
  nickName: string;
  dateOfBirth: string;
  gender?: Gender | null;
  status: StudentStatus;
  guardianName?: string | null;
  guardianPhone?: string | null;
  note?: string | null;
  groupId?: string | null;
  groupCode?: string | null;
  groupName?: string | null;
  responsibleTeacherName?: string | null;
  studySchedule: StudySchedule;
  createdAt: string;
  updatedAt: string;
  version: number;
}

export interface CreateStudentRequest {
  studentCode: string;
  fullName: string;
  nickName: string;
  dateOfBirth: string;
  gender: Gender | null;
  status: StudentStatus;
  guardianName: string | null;
  guardianPhone: string | null;
  note: string | null;
  studySchedule: StudySchedule;
}

export interface UpdateStudentRequest extends CreateStudentRequest {
  expectedVersion: number;
}

export interface CurrentUser {
  id: string;
  email: string;
  fullName: string;
  phoneNumber?: string | null;
  role: UserRole;
  status: UserStatus;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  expiresIn: number;
  csrfToken: string;
  user: CurrentUser;
}

export interface CsrfResponse {
  csrfToken: string;
}

export interface SetupStatusResponse {
  requiresInitialization: boolean;
}

export interface CreateSuperAdminRequest {
  email: string;
  fullName: string;
  password: string;
}

export interface SetupSuperAdminResponse {
  id: string;
  email: string;
  fullName: string;
}

export interface TeacherListQuery extends ListQuery {
  status?: UserStatus;
  groupId?: string;
  unassigned?: boolean;
}

export interface Teacher {
  id: string;
  userId: string;
  teacherCode: string;
  fullName: string;
  email: string;
  phoneNumber: string | null;
  status: UserStatus;
  attendanceEditWindowDays: number;
  responsibleGroupCount: number;
  createdAt: string;
  updatedAt: string;
  version: number;
}

export interface TeacherGroupSummary {
  id: string;
  code: string;
  name: string;
  status: StudentGroupStatus;
  studentCount: number;
}

export interface TeacherDetail extends Teacher {
  note: string | null;
  responsibleGroups: TeacherGroupSummary[];
}

export interface CreateTeacherRequest {
  teacherCode: string;
  fullName: string;
  email: string;
  phoneNumber: string | null;
  status: UserStatus;
  password: string;
  note: string | null;
}

export interface UpdateTeacherRequest {
  teacherCode: string;
  fullName: string;
  email: string;
  phoneNumber: string | null;
  status: UserStatus;
  note: string | null;
  expectedVersion: number;
}

export interface UpdateAttendancePolicyRequest {
  attendanceEditWindowDays: number;
  expectedVersion: number;
}

export interface StudentGroupListQuery extends ListQuery {
  status?: StudentGroupStatus;
  unassigned?: boolean;
}

export interface StudentGroup {
  id: string;
  code: string;
  name: string;
  status: StudentGroupStatus;
  responsibleTeacherId?: string | null;
  responsibleTeacherName?: string | null;
  studentCount: number;
  snapshotVersion: number;
  createdAt: string;
  updatedAt: string;
}

export interface SaveStudentGroupRequest {
  code: string;
  name: string;
  status: StudentGroupStatus;
}

export interface AssignResponsibleTeacherRequest {
  teacherId: string | null;
}

export interface AssignStudentGroupRequest {
  groupId: string | null;
  expectedVersion: number;
}

export interface AttendanceContextGroup {
  id: string;
  code: string;
  name: string;
  studentCount: number;
}

export interface AttendanceContext {
  date: string;
  serverDate: string;
  groups: AttendanceContextGroup[];
  attendanceEditWindowDays: number | null;
  canEdit: boolean;
  readOnlyReason: string | null;
}

export interface AttendanceGroupSnapshot {
  id: string;
  code: string;
  name: string;
}

export interface AttendanceSummary {
  rosterTotal: number;
  present: number;
  absent: number;
  oneToOne: number;
  unmarked: number;
}

export interface AttendanceEntry {
  entryId: string | null;
  studentId: string;
  studentCode: string;
  fullName: string;
  nickName: string;
  status: AttendanceStatus;
  halfDayPart: HalfDayPart | null;
  isExcused: boolean | null;
  durationMinutes: number | null;
  notes: string | null;
  updatedAt: string | null;
}

export interface DailyAttendance {
  date: string;
  serverDate: string;
  group: AttendanceGroupSnapshot;
  sheetState: SheetState;
  sheetId: string | null;
  sheetVersion: number | null;
  snapshotSource: SnapshotSource | null;
  currentSnapshotVersion: number | null;
  sourceSnapshotVersion: number | null;
  canCreate: boolean;
  canEdit: boolean;
  canRecover: boolean;
  readOnlyReason: string | null;
  summary: AttendanceSummary;
  items: AttendanceEntry[];
}

export interface SaveAttendanceRecord {
  studentId: string;
  status: AttendanceStatus;
  halfDayPart: HalfDayPart | null;
  isExcused: boolean | null;
  durationMinutes: number | null;
  notes: string | null;
}

export interface CreateAttendanceSheetRequest {
  groupId: string;
  date: string;
  expectedSnapshotVersion: number;
  records: SaveAttendanceRecord[];
}

export interface UpdateAttendanceSheetRequest {
  expectedVersion: number;
  records: SaveAttendanceRecord[];
}

export interface CandidateListQuery {
  search?: string;
  page: number;
  pageSize: number;
}

export interface RecoveryGroupCandidate {
  id: string;
  code: string;
  name: string;
  status: StudentGroupStatus;
  isDeleted: boolean;
}

export interface RecoveryStudentCandidate {
  id: string;
  studentCode: string;
  fullName: string;
  nickName: string;
  groupCode?: string | null;
  groupName?: string | null;
  responsibleTeacherName?: string | null;
  status: StudentStatus;
  isDeleted: boolean;
  currentGroupId: string | null;
}

export interface RecoveryTeacherCandidate {
  id: string;
  userId: string;
  fullName: string;
  status: UserStatus;
  isDeleted: boolean;
  isCurrentTeacherRole: boolean;
}

export interface HistoricalRecoveryRequest {
  groupId: string;
  date: string;
  responsibleTeacherId: string;
  records: SaveAttendanceRecord[];
  acknowledgeHistoricalSnapshot: true;
  recoveryReason: string;
}


export interface AssessmentListQuery extends ListQuery {
  groupLv1Name?: string;
  groupLv2Name?: string;
  groupLv3Name?: string;
}

export interface Assessment {
  id: string;
  code: string;
  name: string;
  rowIndex: number;
  groupLv1Name: string;
  groupLv2Name: string;
  groupLv3Name: string;
}

export interface AssessmentDetail extends Assessment {
}



export interface AssessmentGroupListQuery extends ListQuery {
  level?: number;
  parentName?: string;
  parentParentName?: string;
}

export interface AssessmentGroup {
  id: string;
  name: string;
  level?: number;
  parentName?: string | null;
}
export interface AssessmentGroupDetail extends AssessmentGroup {
}


export interface SyncAssessmentFromGoogleSheetsRequest {
}

export interface SyncAssessmentFromGoogleSheetsResponse {
  sheetsTotalRows: number;
  databaseTotalRows: number;
  insertedRows: number;
  updatedRows: number;
  deletedRows: number;
}
