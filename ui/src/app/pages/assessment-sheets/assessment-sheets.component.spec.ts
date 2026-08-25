import {
  AssessmentSheetFormComponent,
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

describe('Assessment sheet form DevExtreme option stability', () => {
  it('keeps editor option object references stable across change detection reads', () => {
    const component = new AssessmentSheetFormComponent(
      {} as any,
      {} as any,
      { user: { role: 'Admin' } } as any,
      {} as any,
      {} as any,
      { snapshot: { data: { mode: 'create' }, paramMap: { get: () => null } } } as any,
      {} as any
    );

    expect(component.studentEditorOptions).toBe(component.studentEditorOptions);
    expect(component.teacherEditorOptions).toBe(component.teacherEditorOptions);
    expect(component.statusEditorOptions).toBe(component.statusEditorOptions);
    expect(component.assessmentEditorOptions).toBe(component.assessmentEditorOptions);
    expect(component.dateEditorOptions).toBe(component.dateEditorOptions);
    expect(component.noteEditorOptions).toBe(component.noteEditorOptions);
    expect(component.formColCountByScreen).toBe(component.formColCountByScreen);
  });
});
