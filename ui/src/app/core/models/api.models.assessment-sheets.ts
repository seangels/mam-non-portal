import { ListQuery } from "./api.models";

export type AssessmentSheetStatus = 'Open' | 'Done';

export interface AssessmentSheetListQuery extends ListQuery {
  studentId?: string;
  status?: AssessmentSheetStatus;
  dateFrom?: string;
  dateTo?: string;
}


export interface AssessmentSheet {
  id: string;
  name: string;
  assessmentSheetStatus: AssessmentSheetStatus;

  studentCode?: string | null;
  studentFullName?: string | null;
  studentNickName?: string | null;
  studentDateOfBirth?: string | null;
  responsibleTeacherFullName?: string | null;

  note?: string | null;

  startDate?: string | null;
  dueDate?: string | null;
  doneDate?: string | null;
  submissionDate?: string | null;

  feedback?: string | null;
  planFileLinkPdf?: string | null;
  resultFileLinkPdf?: string | null;
  assessmentSheetSpreadsheetId?: string | null;

  updatedByUserName?: string | null;
  createdAt: string;
  updatedAt: string;
}


export interface AssessmentSheetDetail extends AssessmentSheet {
}

export interface CreateAssessmentSheetRequest {
  name: string;
  studentId: string;
  responsibleTeacherId: string;
  startDate?: string | null;
  dueDate?: string | null;
}

export interface UpdateAssessmentSheetRequest {
  name: string;
  assessmentSheetStatus: AssessmentSheetStatus;
  studentId: string;
  responsibleTeacherId: string;
  startDate?: string | null;
  dueDate?: string | null;
}
