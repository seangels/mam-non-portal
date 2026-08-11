export type UserRole = 'SuperAdmin' | 'Admin' | 'Teacher';
export type UserStatus = 'Active' | 'Inactive' | 'Locked';
export type StudentStatus = 'Active' | 'Inactive';
export type Gender = 'Male' | 'Female' | 'Other';
export type SortOrder = 'asc' | 'desc';

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
  role: Exclude<UserRole, 'SuperAdmin'>;
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
  createdAt: string;
  updatedAt: string;
}

export type CreateStudentRequest = Omit<Student, 'id' | 'createdAt' | 'updatedAt'>;
export type UpdateStudentRequest = CreateStudentRequest;

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
