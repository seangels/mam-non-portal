import { of } from 'rxjs';
import { AssessmentPickerComponent } from './assessment-picker.component';
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
  const createComponent = (): AssessmentSheetFormComponent =>
    new AssessmentSheetFormComponent(
      {} as any,
      { user: { role: 'Admin' } } as any,
      {} as any,
      {} as any,
      { snapshot: { data: { mode: 'create' }, paramMap: { get: () => null } } } as any,
      {} as any
    );

  it('keeps editor option object references stable across change detection reads', () => {
    const component = createComponent();

    expect(component.studentEditorOptions).toBe(component.studentEditorOptions);
    expect(component.teacherEditorOptions).toBe(component.teacherEditorOptions);
    expect(component.statusEditorOptions).toBe(component.statusEditorOptions);
    expect(component.dateEditorOptions).toBe(component.dateEditorOptions);
    expect(component.noteEditorOptions).toBe(component.noteEditorOptions);
    expect(component.formColCountByScreen).toBe(component.formColCountByScreen);
  });

  it('reports selected assessment count for create and edit modes', () => {
    const component = createComponent();

    component.editor.assessmentIds = ['assessment-1', 'assessment-2', 'assessment-1', ''];
    expect(component.selectedAssessmentCount).toBe(2);

    component.isCreate = false;
    component.records = [{ id: 'record-1' } as any, { id: 'record-2' } as any, { id: 'record-3' } as any];
    expect(component.selectedAssessmentCount).toBe(3);
  });

});

describe('Assessment picker filter and selection', () => {
  const createPicker = () => {
    const assessments = {
      list: jasmine.createSpy('list').and.returnValue(of({
        items: [],
        pagination: { page: 1, pageSize: 20, totalItems: 0, totalPages: 0 }
      })),
      get: jasmine.createSpy('get').and.returnValue(of({}))
    };
    const groups = {
      list: jasmine.createSpy('list').and.returnValue(of({
        items: [],
        pagination: { page: 1, pageSize: 100, totalItems: 0, totalPages: 0 }
      })),
      get: jasmine.createSpy('get').and.returnValue(of({}))
    };
    const googleSheet = {
      syncFromGoogleSheets: jasmine.createSpy('syncFromGoogleSheets')
    };

    return {
      component: new AssessmentPickerComponent(assessments as any, groups as any, googleSheet as any),
      assessments
    };
  };

  it('updates selected assessment ids from custom checkbox changes', () => {
    const { component } = createPicker();
    const emitted: string[][] = [];
    component.selectedIds = ['assessment-1'];
    component.ngOnChanges({ selectedIds: {} as any });
    component.selectedIdsChange.subscribe(value => emitted.push(value));

    component.onSelectCheckboxChanged('assessment-2', { value: true, event: {} });
    component.onSelectCheckboxChanged('assessment-1', { value: false, event: {} });

    expect(emitted).toEqual([['assessment-1', 'assessment-2'], ['assessment-2']]);
    expect(component.isSelected('assessment-2')).toBeTrue();
    expect(component.isSelected('assessment-1')).toBeFalse();
  });

  it('ignores programmatic checkbox value changes from grid rendering', () => {
    const { component } = createPicker();
    const emitted: string[][] = [];
    component.selectedIdsChange.subscribe(value => emitted.push(value));

    component.onSelectCheckboxChanged('assessment-1', { value: true });

    expect(emitted).toEqual([]);
  });

  it('reports select-all checkbox state for visible rows', () => {
    const { component } = createPicker();
    component.visibleAssessmentIds = ['assessment-1', 'assessment-2'];

    expect(component.selectAllVisibleValue).toBeFalse();

    component.selectedIds = ['assessment-1'];
    component.ngOnChanges({ selectedIds: {} as any });
    expect(component.selectAllVisibleValue).toBeNull();
    expect(component.selectAllVisibleText).toBe('Chọn tất cả (1/2)');

    component.selectedIds = ['assessment-1', 'assessment-2'];
    component.ngOnChanges({ selectedIds: {} as any });
    expect(component.selectAllVisibleValue).toBeTrue();
  });

  it('selects and clears all visible rows from the panel checkbox', () => {
    const { component } = createPicker();
    const emitted: string[][] = [];
    component.visibleAssessmentIds = ['assessment-1', 'assessment-2'];
    component.selectedIds = ['assessment-3'];
    component.ngOnChanges({ selectedIds: {} as any });
    component.selectedIdsChange.subscribe(value => emitted.push(value));

    component.setAllVisibleSelected(true);
    component.setAllVisibleSelected(false);

    expect(emitted).toEqual([
      ['assessment-3', 'assessment-1', 'assessment-2'],
      ['assessment-3']
    ]);
  });

  it('highlights selected rows and clears highlight from unselected rows', () => {
    const { component } = createPicker();
    const selectedRow = document.createElement('tr');
    const unselectedRow = document.createElement('tr');
    unselectedRow.classList.add('assessment-picker-selected-row');
    component.selectedIds = ['assessment-1'];
    component.ngOnChanges({ selectedIds: {} as any });

    component.onRowPrepared({
      rowType: 'data',
      data: { id: 'assessment-1' } as any,
      rowElement: selectedRow
    });
    component.onRowPrepared({
      rowType: 'data',
      data: { id: 'assessment-2' } as any,
      rowElement: unselectedRow
    });

    expect(selectedRow.classList.contains('assessment-picker-selected-row')).toBeTrue();
    expect(unselectedRow.classList.contains('assessment-picker-selected-row')).toBeFalse();
  });

  it('loads assessments with text and group filters like the assessment list', async () => {
    const { component, assessments } = createPicker();
    component.search = 'ngôn ngữ';
    component.groupLv1Name = '3-4 tuổi';
    component.groupLv2Name = 'Phát triển nhận thức';
    component.groupLv3Name = 'Toán';

    await (component.dataSource as any).load({
      skip: 20,
      take: 20,
      sort: [{ selector: 'rowIndex', desc: true }]
    });

    expect(assessments.list).toHaveBeenCalledWith({
      page: 2,
      pageSize: 20,
      search: 'ngôn ngữ',
      groupLv1Name: '3-4 tuổi',
      groupLv2Name: 'Phát triển nhận thức',
      groupLv3Name: 'Toán',
      sortBy: 'rowindex',
      sortOrder: 'desc'
    });
  });
});
