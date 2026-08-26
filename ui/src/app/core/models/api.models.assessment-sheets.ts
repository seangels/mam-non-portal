import { Gender, ListQuery } from './api.models';

export type AssessmentSheetStatus = 'Open' | 'Planed' | 'Done';
export type AssessmentGrade = 'A' | 'B' | 'C' | 'D';

export const ASSESSMENT_SHEET_STATUS_OPTIONS: { value: AssessmentSheetStatus; text: string }[] = [
  { value: 'Open', text: 'Đang mở' },
  { value: 'Planed', text: 'Đã lập kế hoạch' },
  { value: 'Done', text: 'Hoàn tất' }
];

export const ASSESSMENT_GRADE_OPTIONS: { value: AssessmentGrade; text: string, color: string, bgcolor: string }[] = [
  { value: 'A', text: 'Đạt +', color: '#11734b', bgcolor: '#d4edbc' },
  { value: 'B', text: 'Chưa đạt -', color: '#b10202', bgcolor: '#ffcfc9' },
  { value: 'C', text: 'Hỗ trợ +', color: '#473821', bgcolor: '#ffe5a0' },
  { value: 'D', text: 'Hỗ trợ -', color: '#000', bgcolor: '#e8eaed' }
];

export const ASSESSMENT_GROUP_LV2_CONFIGS: { key: string; displayOrder: number; bgcolor: string }[] = [
  { key: 'Phát triển thể chất', displayOrder: 1, bgcolor: '#EAD1DC' },
  { key: 'Phát triển nhận thức', displayOrder: 2, bgcolor: '#D9D2E9' },
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

export interface ReplaceAssessmentSheetRecordsRequest {
  records: AssessmentSheetRecordRequest[];
}

export interface AssessmentSheetRecordRequest {
  assessmentId: string;
  planGrade?: AssessmentGrade | null;
  planNote?: string | null;
  finalGrade?: AssessmentGrade | null;
  finalNote?: string | null;
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
