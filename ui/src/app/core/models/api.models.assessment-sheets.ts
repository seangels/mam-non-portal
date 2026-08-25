import { Gender, ListQuery } from './api.models';

export type AssessmentSheetStatus = 'Open' | 'Planed' | 'Done';
export type AssessmentGrade = 'A' | 'B' | 'C' | 'D';

export const ASSESSMENT_SHEET_STATUS_OPTIONS: { value: AssessmentSheetStatus; text: string }[] = [
  { value: 'Open', text: 'Đang mở' },
  { value: 'Planed', text: 'Đã lập kế hoạch' },
  { value: 'Done', text: 'Hoàn tất' }
];

export const ASSESSMENT_GRADE_OPTIONS: { value: AssessmentGrade; text: string }[] = [
  { value: 'A', text: 'Đạt +' },
  { value: 'B', text: 'Chưa đạt -' },
  { value: 'C', text: 'Hỗ trợ +' },
  { value: 'D', text: 'Hỗ trợ -' }
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
  assessmentSheetSpreadsheetId?: string | null;
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
  assessment: AssessmentSnapshot;
  planGrade?: AssessmentGrade | null;
  planNote?: string | null;
  finalGrade?: AssessmentGrade | null;
  finalNote?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface AssessmentSheetDetail extends AssessmentSheet {
  studentSnapshot: AssessmentSheetStudentSnapshot;
  note?: string | null;
  feedback?: string | null;
  records: AssessmentSheetRecord[];
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
}

export interface UpdateAssessmentSheetStatusRequest {
  status: AssessmentSheetStatus;
}
