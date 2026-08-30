import { Gender, ListQuery } from './api.models';

export type AssessmentSheetStatus = 'Open' | 'Planed' | 'Done';
export type AssessmentGrade = 'A' | 'B' | 'C' | 'D';

export const ASSESSMENT_SHEET_STATUS_OPTIONS: { value: AssessmentSheetStatus; text: string }[] = [
  { value: 'Open', text: 'Đang mở' },
  { value: 'Planed', text: 'Đã lập kế hoạch' },
  { value: 'Done', text: 'Hoàn tất' }
];

// Thứ tự/định nghĩa đã chốt với người dùng (2026-08-30): A = "Đạt +" (rank 3, cao nhất) >
// B = "Hỗ trợ +" (rank 2) > C = "Hỗ trợ -" (rank 1) > D = "Chưa đạt" (rank 0, thấp nhất).
// Màu đi theo ngữ nghĩa nhãn, không cố định theo chữ cái enum.
export const ASSESSMENT_GRADE_OPTIONS: { value: AssessmentGrade; text: string, color: string, bgcolor: string }[] = [
  { value: 'A', text: 'Đạt +', color: '#11734b', bgcolor: '#d4edbc' },
  { value: 'B', text: 'Hỗ trợ +', color: '#473821', bgcolor: '#ffe5a0' },
  { value: 'C', text: 'Hỗ trợ -', color: '#000', bgcolor: '#e8eaed' },
  { value: 'D', text: 'Chưa đạt', color: '#b10202', bgcolor: '#ffcfc9' }
];

export const ASSESSMENT_GROUP_LV2_CONFIGS: { key: string; displayOrder: number; bgcolor: string }[] = [
  { key: 'Phát triển thể chất', displayOrder: 1, bgcolor: '#C9DAF8' },
  { key: 'Phát triển nhận thức', displayOrder: 2, bgcolor: '#C7B7D2' },
  { key: 'Phát triển ngôn ngữ', displayOrder: 3, bgcolor: '#C9DAF8' },
  { key: 'Cá nhân và xã hội', displayOrder: 4, bgcolor: '#D0E0E3' },
  { key: 'Tiền tiểu học', displayOrder: 5, bgcolor: '#DCC1CF' }
];

export interface AssessmentSheetListQuery extends ListQuery {
  studentId?: string;
  status?: AssessmentSheetStatus;
  dateFrom?: string;
  dateTo?: string;
}

export interface AssessmentSheet {
  id: string;
  status: AssessmentSheetStatus;
  studentId: string;
  studentCode?: string | null;
  studentFullName?: string | null;
  responsibleTeacherId?: string | null;
  responsibleTeacherFullName?: string | null;
  startDate?: string | null;
  dueDate?: string | null;
  doneDate?: string | null;
  submissionDate?: string | null;
  planFileLinkPdf?: string | null;
  resultFileLinkPdf?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface AssessmentSheetStudentSnapshot {
  studentCode?: string | null;
  fullName?: string | null;
  nickName?: string | null;
  dateOfBirth?: string | null;
  gender?: Gender | null;
}

export interface AssessmentSnapshot {
  code: string;
  name: string;
  groupLv1Name?: string | null;
  groupLv2Name?: string | null;
  groupLv3Name?: string | null;
  rowIndex?: number | null;
}

export interface AssessmentSheetRecord {
  id: string;
  assessmentSheetId: string;
  assessmentRowIndex?: number | null;
  displayOrder?: number | null;
  assessment: AssessmentSnapshot;
  planGrade?: AssessmentGrade | null;
  planNote?: string | null;
  finalGrade?: AssessmentGrade | null;
  finalNote?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface ReplaceAssessmentSheetRecordsRequest {
  records: AssessmentSheetRecordRequest[];
}

export interface AssessmentSheetRecordRequest {
  assessmentId: string;
  planGrade?: AssessmentGrade | null;
  planNote?: string | null;
  finalGrade?: AssessmentGrade | null;
  finalNote?: string | null;
  displayOrder?: number | null;
  groupLv2Name?: string | null;
  groupLv3Name?: string | null;
}

export interface AssessmentSheetDetail extends AssessmentSheet {
  studentSnapshot: AssessmentSheetStudentSnapshot;
  note?: string | null;
  feedback?: string | null;
  records: AssessmentSheetRecord[];
}

export interface AssessmentSheetImportExcelResult {
  createdSheetCount: number;
  updatedSheetCount: number;
  importedRecordCount: number;
  skippedDuplicateRowCount: number;
  warnings: string[];
  sheets: AssessmentSheet[];
}

export interface AssessmentSheetImportExcelPreviewSummaryResult {
  validRowCount: number;
  errorCount: number;
  warningCount: number;
  skippedDuplicateRowCount: number;
  groups: number;
  canImport: boolean;
}
export interface AssessmentSheetImportExcelPreviewResult {
  summary: AssessmentSheetImportExcelPreviewSummaryResult;
  rows: AssessmentSheetImportExcelPreviewRow[];
}

export interface AssessmentSheetImportExcelPreviewRow {
  rowNumber: number;
  planGrade?: string | null;
  planNote?: string | null;
  assessmentCode?: string | null;
  studentCode?: string | null;
  studentName?: string | null;
  startDate?: string | null;
  dueDate?: string | null;
  stt?: number | null;
  groupLv2Name?: string | null;
  groupLv3Name?: string | null;
  action?: string | null;
  isDuplicate?: boolean | null;
  errors: string[];
  warnings: string[];
}

export interface CreateAssessmentSheetRequest {
  studentId: string;
  responsibleTeacherId?: string | null;
  note?: string | null;
  startDate?: string | null;
  dueDate?: string | null;
  records: CreateAssessmentSheetRecordRequest[];
}

export interface CreateAssessmentSheetRecordRequest {
  assessmentId: string;
  latestGrade?: AssessmentGrade | null;
  note?: string | null;
}

export interface UpdateAssessmentSheetRequest {
  responsibleTeacherId?: string | null;
  note?: string | null;
  startDate?: string | null;
  dueDate?: string | null;
  feedback?: string | null;
  planFileLinkPdf?: string | null;
  resultFileLinkPdf?: string | null;
}

export interface UpdateAssessmentSheetStatusRequest {
  status: AssessmentSheetStatus;
}

export type AssessmentSheetRecordGroupLevel = 2 | 3;

/**
 * Cập nhật nhóm trên bảng Assessment danh mục (PATCH /assessments/group).
 * Không đụng snapshot của bất kỳ AssessmentSheet nào — snapshot đổi qua PUT .../records.
 */
export interface UpdateAssessmentGroupRequest {
  level: AssessmentSheetRecordGroupLevel;
  assessmentCodes: string[];
  name: string;
}

export interface UpdateAssessmentGroupResult {
  updatedCount: number;
}
