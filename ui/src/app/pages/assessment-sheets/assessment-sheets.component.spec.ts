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
  const assessment = (override: any) => ({
    id: 'assessment-1',
    code: 'A01',
    name: 'Ngôn ngữ',
    rowIndex: 1,
    groupLv1Name: '3-4 tuổi',
    groupLv2Name: 'Phát triển nhận thức',
    groupLv3Name: 'Toán',
    ...override
  });

  const createPicker = () => {
    const assessments = {
      list: jasmine.createSpy('list').and.returnValue(of({
        items: [],
        pagination: { page: 1, pageSize: 20, totalItems: 0, totalPages: 0 }
      })),
      get: jasmine.createSpy('get').and.returnValue(of({}))
    };
    const googleSheet = {
      syncFromGoogleSheets: jasmine.createSpy('syncFromGoogleSheets')
    };

    return {
      component: new AssessmentPickerComponent(assessments as any, googleSheet as any),
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

  it('loads all assessments into a client cache and filters locally', async () => {
    const { component, assessments } = createPicker();
    assessments.list.and.returnValues(
      of({
        items: [
          assessment({ id: 'assessment-1', code: 'NN01', name: 'Ngôn ngữ', groupLv1Name: '3-4 tuổi' }),
          assessment({ id: 'assessment-2', code: 'TC01', name: 'Thể chất', groupLv1Name: '4-5 tuổi', groupLv2Name: 'Vận động' })
        ],
        pagination: { page: 1, pageSize: 100, totalItems: 3, totalPages: 2 }
      }),
      of({
        items: [
          assessment({ id: 'assessment-3', code: 'TM01', name: 'Thẩm mỹ', groupLv1Name: '3-4 tuổi', groupLv2Name: 'Nghệ thuật' })
        ],
        pagination: { page: 2, pageSize: 100, totalItems: 3, totalPages: 2 }
      })
    );

    await component.loadAssessmentsFromServer();

    expect(assessments.list.calls.allArgs()).toEqual([
      [{
        page: 1,
        pageSize: 100,
        sortBy: 'rowindex',
        sortOrder: 'asc'
      }],
      [{
        page: 2,
        pageSize: 100,
        sortBy: 'rowindex',
        sortOrder: 'asc'
      }]
    ]);
    expect(component.allAssessments.length).toBe(3);
    expect(component.groupLv1DataSource.map(group => group.name)).toEqual(['3-4 tuổi', '4-5 tuổi']);

    component.search = 'ngon ngu';
    component.groupLv1Name = '3-4 tuổi';
    component.applyFilters();

    expect(assessments.list).toHaveBeenCalledTimes(2);
    expect(component.filteredAssessments.map(item => item.id)).toEqual(['assessment-1']);
  });

  it('updates dependent group filter options from the client cache', () => {
    const { component } = createPicker();
    component.allAssessments = [
      assessment({ id: 'assessment-1', groupLv1Name: '3-4 tuổi', groupLv2Name: 'Nhóm B', groupLv3Name: 'Chủ đề 2' }),
      assessment({ id: 'assessment-2', groupLv1Name: '4-5 tuổi', groupLv2Name: 'Nhóm A', groupLv3Name: 'Chủ đề 1' })
    ];

    component.onGroupLv1Changed();
    component.groupLv1Name = '3-4 tuổi';
    component.onGroupLv1Changed();

    expect(component.groupLv2DataSource.map(group => group.name)).toEqual(['Nhóm B']);
    expect(component.groupLv3DataSource.map(group => group.name)).toEqual(['Chủ đề 2']);
  });

  it('keeps the current selected-only view stable when rows are unchecked', () => {
    const { component, assessments } = createPicker();
    component.allAssessments = [
      assessment({ id: 'assessment-1', code: 'NN01', name: 'Ngôn ngữ' }),
      assessment({ id: 'assessment-2', code: 'TC01', name: 'Thể chất' })
    ];
    component.selectedIds = ['assessment-1', 'assessment-2'];
    component.ngOnChanges({ selectedIds: {} as any });

    component.viewMode = 'selected';
    component.onViewModeChanged();

    expect(component.filteredAssessments.map(item => item.id)).toEqual(['assessment-1', 'assessment-2']);

    component.onSelectCheckboxChanged('assessment-2', { value: false, event: {} });

    expect(component.filteredAssessments.map(item => item.id)).toEqual(['assessment-1', 'assessment-2']);
    expect(component.isSelected('assessment-2')).toBeFalse();

    component.search = 'the chat';
    component.applyFilters();

    expect(component.filteredAssessments.map(item => item.id)).toEqual(['assessment-2']);
    expect(assessments.list).not.toHaveBeenCalled();

    component.onViewModeChanged();

    expect(component.filteredAssessments).toEqual([]);
  });

  it('retries loading the client cache from the server', async () => {
    const { component, assessments } = createPicker();
    assessments.list.and.returnValue(of({
      items: [assessment({ id: 'assessment-1' })],
      pagination: { page: 1, pageSize: 100, totalItems: 1, totalPages: 1 }
    }));

    await component.loadAssessmentsFromServer();
    component.search = 'không khớp';
    component.applyFilters();
    expect(component.filteredAssessments.length).toBe(0);

    component.retryLoad();
    await Promise.resolve();

    expect(assessments.list).toHaveBeenCalledTimes(2);
    expect(component.filteredAssessments.length).toBe(0);
    expect(component.allAssessments.length).toBe(1);
  });
});
