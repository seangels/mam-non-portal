import {
  AssessmentSheetEditor,
  buildCreateAssessmentSheetRequest,
  buildUpdateAssessmentSheetRequest
} from './assessment-sheets-form.component';

describe('Assessment sheet form request mapping', () => {
  const editor = (override: Partial<AssessmentSheetEditor> = {}): AssessmentSheetEditor => ({
    studentId: 'student-1',
    responsibleTeacherId: null,
    startDate: '2026-08-25',
    dueDate: '2026-08-30',
    status: 'Open',
    note: '',
    feedback: '',
    assessmentIds: [],
    ...override
  });

  it('builds create request with trimmed optional fields and distinct assessment ids', () => {
    const request = buildCreateAssessmentSheetRequest(editor({
      responsibleTeacherId: 'teacher-1',
      note: '  Ghi chú tạo mới  ',
      assessmentIds: ['assessment-1', 'assessment-2', 'assessment-1', '']
    }));

    expect(request).toEqual({
      studentId: 'student-1',
      responsibleTeacherId: 'teacher-1',
      note: 'Ghi chú tạo mới',
      startDate: '2026-08-25',
      dueDate: '2026-08-30',
      assessmentIds: ['assessment-1', 'assessment-2']
    });
  });

  it('builds update request without student id or assessment ids', () => {
    const request = buildUpdateAssessmentSheetRequest(editor({
      responsibleTeacherId: '',
      note: '   ',
      feedback: '  Nhận xét cuối kỳ  ',
      assessmentIds: ['ignored']
    }));

    expect(request).toEqual({
      responsibleTeacherId: null,
      note: null,
      startDate: '2026-08-25',
      dueDate: '2026-08-30',
      feedback: 'Nhận xét cuối kỳ'
    });
  });
});
