import { StudentStatus } from './api.models';
import { AssessmentGrade } from './api.models.assessment-sheets';

export interface AssessmentResultsStudent {
  id: string;
  studentCode: string;
  fullName: string;
  nickName: string;
  dateOfBirth: string;
  status: StudentStatus;
}

export interface AssessmentResultItem {
  assessmentId: string;
  code: string;
  name: string;
  groupLv1Name?: string | null;
  groupLv2Name?: string | null;
  groupLv3Name?: string | null;
  rowIndex?: number | null;
  grade?: AssessmentGrade | null;
  note?: string | null;
  version: string;
}

export interface AssessmentResultsSnapshot {
  student: AssessmentResultsStudent;
  items: AssessmentResultItem[];
}

export interface UpdateAssessmentResultItemRequest {
  assessmentId: string;
  expectedVersion: string;
  grade: AssessmentGrade | null;
  note: string | null;
}

export interface UpdateAssessmentResultsRequest {
  items: UpdateAssessmentResultItemRequest[];
}

export interface AssessmentResultConflict {
  assessmentId: string;
  currentVersion: string;
  currentGrade?: AssessmentGrade | null;
  currentNote?: string | null;
}
