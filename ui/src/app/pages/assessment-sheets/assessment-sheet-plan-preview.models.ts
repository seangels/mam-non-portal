import { AssessmentSheetDetail, AssessmentSheetRecord } from '../../core/models/api.models.assessment-sheets';
import { normalizeVietnamese } from '../../core/utils/vietnamese-search';
import {
  AssessmentSheetRecordTableRow,
  assessmentGradeText,
  assessmentGradeBgColor,
  assessmentGradeColor,
  buildAssessmentSheetRecordRows
} from './assessment-sheets-form.component';

export interface AssessmentSheetPlanPreviewModel {
  kind: AssessmentSheetPdfKind;
  documentTitle: string;
  sectionTitle: string;
  tableLabel: string;
  studentName: string;
  studentCode: string;
  studentNickName: string;
  birthDateText: string;
  ageText: string;
  periodText: string;
  fileName: string;
  rows: AssessmentSheetRecordTableRow[];
}

export type AssessmentSheetPdfKind = 'plan' | 'result';

const MISSING_TEXT = 'Chưa có thông tin';

export function buildAssessmentSheetPlanPreview(sheet: AssessmentSheetDetail): AssessmentSheetPlanPreviewModel {
  return buildAssessmentSheetPdfPreview(sheet, 'plan');
}

export function buildAssessmentSheetResultPreview(sheet: AssessmentSheetDetail): AssessmentSheetPlanPreviewModel {
  return buildAssessmentSheetPdfPreview(sheet, 'result');
}

export function buildAssessmentSheetPdfPreview(
  sheet: AssessmentSheetDetail,
  kind: AssessmentSheetPdfKind
): AssessmentSheetPlanPreviewModel {
  const snapshot = sheet.studentSnapshot ?? {};
  const studentCode = snapshot.studentCode || sheet.studentCode || '';
  const isResult = kind === 'result';

  return {
    kind,
    documentTitle: isResult ? 'KẾT QUẢ ĐÁNH GIÁ' : 'KẾ HOẠCH CÁ NHÂN',
    sectionTitle: isResult ? '2. Kết quả đánh giá' : '2. Kế hoạch cá nhân',
    tableLabel: isResult ? 'Kết quả đánh giá' : 'Kế hoạch cá nhân',
    studentName: snapshot.fullName || sheet.studentFullName || MISSING_TEXT,
    studentCode: studentCode || MISSING_TEXT,
    studentNickName: snapshot.nickName || '',
    birthDateText: formatDateText(snapshot.dateOfBirth),
    ageText: calculateAgeText(snapshot.dateOfBirth, sheet.startDate),
    periodText: formatAssessmentPeriod(sheet.startDate, sheet.dueDate),
    fileName: isResult
      ? buildResultPdfFileName(studentCode || sheet.id, snapshot.nickName, sheet.startDate, sheet.dueDate)
      : buildPlanPdfFileName(studentCode || sheet.id, snapshot.nickName, sheet.startDate, sheet.dueDate),
    rows: buildAssessmentSheetRecordRows(sheet.records ?? [])
  };
}

export function planGradeText(record: AssessmentSheetRecord): string {
  return assessmentGradeText(record.planGrade);
}

export function planGradeColor(record: AssessmentSheetRecord): string {
  return assessmentGradeColor(record.planGrade);
}

export function planGradeBgColor(record: AssessmentSheetRecord): string {
  return assessmentGradeBgColor(record.planGrade);
}

export function planNoteText(record: AssessmentSheetRecord): string {
  return record.planNote?.trim() || '';
}

export function resultGradeText(record: AssessmentSheetRecord): string {
  return assessmentGradeText(record.finalGrade);
}

export function resultGradeColor(record: AssessmentSheetRecord): string {
  return assessmentGradeColor(record.finalGrade);
}

export function resultGradeBgColor(record: AssessmentSheetRecord): string {
  return assessmentGradeBgColor(record.finalGrade);
}

export function resultNoteText(record: AssessmentSheetRecord): string {
  return record.finalNote?.trim() || '';
}

export function formatAssessmentPeriod(
  startValue: Date | string | number | null | undefined,
  dueValue: Date | string | number | null | undefined
): string {
  const start = toCalendarDate(startValue);
  const due = toCalendarDate(dueValue);
  if (!start || !due) {
    return 'Chưa có đủ ngày bắt đầu và hạn hoàn thành';
  }
  if (due.getTime() < start.getTime()) {
    return 'Ngày hạn hoàn thành đang trước ngày bắt đầu';
  }

  const months: string[] = [];
  let year = start.getFullYear();
  let month = start.getMonth();
  const dueYear = due.getFullYear();
  const dueMonth = due.getMonth();
  console.log({
    year, month,
    dueYear, dueMonth
  })
  while (year < dueYear || (year === dueYear && month <= dueMonth)) {
    months.push(String(month + 1));
    month += 1;
    if (month > 11) {
      month = 0;
      year += 1;
    }
  }

  const yearSuffix = String(dueYear).slice(-2);
  return `${months.length} tháng ${months.join('.')}.${yearSuffix}`;
}

export function calculateAgeText(
  birthValue: Date | string | number | null | undefined,
  atValue: Date | string | number | null | undefined
): string {
  const birthDate = toCalendarDate(birthValue);
  const atDate = toCalendarDate(atValue);
  if (!birthDate || !atDate) {
    return MISSING_TEXT;
  }
  if (atDate.getTime() < birthDate.getTime()) {
    return 'Ngày bắt đầu đang trước ngày sinh';
  }

  let years = atDate.getFullYear() - birthDate.getFullYear();
  let months = atDate.getMonth() - birthDate.getMonth();
  if (atDate.getDate() < birthDate.getDate()) {
    months -= 1;
  }
  if (months < 0) {
    years -= 1;
    months += 12;
  }

  if (years <= 0) {
    return `${months} tháng`;
  }
  return months > 0 ? `${years} tuổi, ${months} tháng` : `${years} tuổi`;
}

export function formatDateText(value: Date | string | number | null | undefined): string {
  const date = toCalendarDate(value);
  if (!date) {
    return MISSING_TEXT;
  }

  return [
    String(date.getDate()).padStart(2, '0'),
    String(date.getMonth() + 1).padStart(2, '0'),
    String(date.getFullYear())
  ].join('/');
}

export function buildPlanPdfFileName(
  studentCode: string | null | undefined,
  studentNickName: string | null | undefined,
  startValue: Date | string | number | null | undefined,
  dueValue: Date | string | number | null | undefined
): string {
  const namePart = buildSafeStudentNamePart(studentCode, studentNickName);
  const assessmentName = buildAssessmentNameForFileName(startValue, dueValue);
  const suffix = assessmentName ? `_${assessmentName}` : '';

  return `khcn - ${namePart}${suffix}.pdf`;
}

export function buildResultPdfFileName(
  studentCode: string | null | undefined,
  studentNickName: string | null | undefined,
  startValue: Date | string | number | null | undefined,
  dueValue: Date | string | number | null | undefined
): string {
  const namePart = buildSafeStudentNamePart(studentCode, studentNickName);
  const assessmentName = buildAssessmentNameForFileName(startValue, dueValue);
  const suffix = assessmentName ? `_${assessmentName}` : '';

  return `kq - ${namePart}${suffix}.pdf`;
}

function isLastDayOfMonth(date: Date): boolean {
  const nextDay = new Date(date.getFullYear(), date.getMonth(), date.getDate() + 1);
  return nextDay.getMonth() !== date.getMonth();
}

function buildAssessmentNameForFileName(
  startValue: Date | string | number | null | undefined,
  dueValue: Date | string | number | null | undefined
): string {
  const start = toCalendarDate(startValue);
  const due = toCalendarDate(dueValue);
  if (!start || !due || due.getTime() < start.getTime()) {
    return '';
  }

  const months: number[] = [];
  let year = start.getFullYear();
  let month = start.getMonth();
  const dueYear = due.getFullYear();
  const dueMonth = due.getMonth();
  console.log({
    year, month,
    dueYear, dueMonth
  })
  while (year < dueYear || (year === dueYear && month < dueMonth)) {
    months.push(month + 1);
    month += 1;
    if (month > 11) {
      month = 0;
      year += 1;
    }
  }
  if (months.length === 0 || isLastDayOfMonth(due)) {
    months.push(dueMonth + 1);
  }

  const yearSuffix = String(dueYear).slice(-2);
  return `${months.join('.')}.${yearSuffix}`;
}

function buildSafeStudentNamePart(
  studentCode: string | null | undefined,
  studentNickName: string | null | undefined
): string {
  const codePart = slugifyFileNamePart(studentCode) || 'hoc-sinh';
  const nickPart = slugifyFileNamePart(studentNickName);
  return nickPart ? `${codePart}.${nickPart}` : codePart;
}

function slugifyFileNamePart(value: string | null | undefined): string {
  return normalizeVietnamese(value || '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

function toCalendarDate(value: Date | string | number | null | undefined): Date | null {
  if (value === null || value === undefined || value === '') {
    return null;
  }
  if (typeof value === 'string') {
    const date = new Date(value);
    if(!isNaN(date.getTime())) {
      return date;
    }
    const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value.trim());
    if (match) {
      return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
    }
  }
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return null;
  }
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}
