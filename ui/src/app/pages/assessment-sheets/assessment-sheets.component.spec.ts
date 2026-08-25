import { of } from 'rxjs';
import { ASSESSMENT_GROUP_LV2_CONFIGS } from '../../core/models/api.models.assessment-sheets';
import { AssessmentPickerComponent } from './assessment-picker.component';
import {
  AssessmentSheetFormComponent,
  AssessmentSheetEditor,
  assessmentGradeBgColor,
  assessmentGradeColor,
  assessmentGradeText,
  assessmentGroupLv2Color,
  buildAssessmentSheetRecordRows,
  buildCreateAssessmentSheetRequest,
  buildRemoveAssessmentSheetRecordRequest,
  buildReplaceAssessmentSheetRecordsRequest,
  buildSaveAssessmentSheetRecordsRequest,
  buildUpdateAssessmentSheetRequest,
  canEditAssessmentSheetRecordValues,
  canMutateAssessmentSheetRecords,
  initializeAssessmentSheetRecords
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

  it('builds create request with trimmed optional fields and assessment record seeds', () => {
    const request = buildCreateAssessmentSheetRequest(editor({
      responsibleTeacherId: 'teacher-1',
      note: '  Ghi chú tạo mới  ',
      assessmentIds: ['assessment-1', 'assessment-2', 'assessment-1', '']
    }), [
      { id: 'assessment-1', latestGrade: 'A', latestNote: '  Ghi chú gần nhất  ' },
      { id: 'assessment-2', latestGrade: null, latestNote: '   ' }
    ]);

    expect(request).toEqual({
      studentId: 'student-1',
      responsibleTeacherId: 'teacher-1',
      note: 'Ghi chú tạo mới',
      startDate: '2026-08-25',
      dueDate: '2026-08-30',
      records: [
        { assessmentId: 'assessment-1', latestGrade: 'A', note: 'Ghi chú gần nhất' },
        { assessmentId: 'assessment-2', latestGrade: null, note: null }
      ]
    });
  });

  it('keeps selected create records even when picker cache misses latest data', () => {
    const request = buildCreateAssessmentSheetRequest(editor({
      assessmentIds: ['assessment-1']
    }));

    expect(request.records).toEqual([
      { assessmentId: 'assessment-1', latestGrade: null, note: null }
    ]);
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

  it('builds replace-records request by preserving current records and adding latest data', () => {
    const request = buildReplaceAssessmentSheetRecordsRequest([
      {
        assessment: { code: 'A01', name: 'Ngôn ngữ' },
        planGrade: 'B',
        planNote: 'Kế hoạch cũ',
        finalGrade: 'C',
        finalNote: 'Kết quả cũ'
      } as any
    ], {
      id: 'assessment-2',
      code: 'A02',
      name: 'Vận động',
      latestGrade: 'A',
      latestNote: 'Ghi chú gần nhất'
    } as any, [
      { id: 'assessment-1', code: 'A01' } as any,
      { id: 'assessment-2', code: 'A02' } as any
    ]);

    expect(request).toEqual({
      records: [
        {
          assessmentId: 'assessment-1',
          planGrade: 'B',
          planNote: 'Kế hoạch cũ',
          finalGrade: 'C',
          finalNote: 'Kết quả cũ'
        },
        {
          assessmentId: 'assessment-2',
          planGrade: 'A',
          planNote: 'Ghi chú gần nhất',
          finalGrade: 'A',
          finalNote: null
        }
      ]
    });
  });

  it('stops replace-records request when an existing record cannot be mapped safely', () => {
    expect(() => buildReplaceAssessmentSheetRecordsRequest([
      { assessment: { code: 'MISSING', name: 'Không còn trong cache' } } as any
    ], {
      id: 'assessment-2',
      code: 'A02',
      name: 'Vận động'
    } as any, [
      { id: 'assessment-2', code: 'A02' } as any
    ])).toThrowError(/Không thể xác định assessmentId/);
  });
  it('builds remove-record request and preserves remaining record values', () => {
    const recordToRemove = {
      id: 'record-1',
      assessment: { code: 'A01', name: 'Ngôn ngữ' }
    } as any;
    const request = buildRemoveAssessmentSheetRecordRequest([
      recordToRemove,
      {
        id: 'record-2',
        assessment: { code: 'A02', name: 'Vận động' },
        planGrade: 'A',
        planNote: 'Kế hoạch',
        finalGrade: 'D',
        finalNote: 'Kết quả'
      } as any
    ], recordToRemove, [
      { id: 'assessment-1', code: 'A01' } as any,
      { id: 'assessment-2', code: 'A02' } as any
    ]);

    expect(request).toEqual({
      records: [
        {
          assessmentId: 'assessment-2',
          planGrade: 'A',
          planNote: 'Kế hoạch',
          finalGrade: 'D',
          finalNote: 'Kết quả'
        }
      ]
    });
  });

  it('stops remove-record request when it would remove the last record', () => {
    const recordToRemove = {
      id: 'record-1',
      assessment: { code: 'A01', name: 'Ngôn ngữ' }
    } as any;

    expect(() => buildRemoveAssessmentSheetRecordRequest([
      recordToRemove
    ], recordToRemove, [
      { id: 'assessment-1', code: 'A01' } as any
    ])).toThrowError(/ít nhất một mục/);
  });

  it('builds save-records request for edited current results', () => {
    const request = buildSaveAssessmentSheetRecordsRequest([
      {
        id: 'record-1',
        assessment: { code: 'A01', name: 'Ngôn ngữ' },
        planGrade: 'B',
        planNote: 'Kế hoạch',
        finalGrade: 'A',
        finalNote: 'Đã đạt'
      } as any
    ], [
      { id: 'assessment-1', code: 'A01' } as any
    ]);

    expect(request).toEqual({
      records: [
        {
          assessmentId: 'assessment-1',
          planGrade: 'B',
          planNote: 'Kế hoạch',
          finalGrade: 'A',
          finalNote: 'Đã đạt'
        }
      ]
    });
  });

  it('defaults empty current results from plan grade when initializing and saving records', () => {
    const initialized = initializeAssessmentSheetRecords([
      {
        id: 'record-1',
        assessment: { code: 'A01', name: 'Ngôn ngữ' },
        planGrade: 'B',
        finalGrade: null
      } as any,
      {
        id: 'record-2',
        assessment: { code: 'A02', name: 'Vận động' },
        planGrade: 'A',
        finalGrade: 'C'
      } as any
    ]);

    expect(initialized.map(record => record.finalGrade)).toEqual(['B', 'C']);

    const request = buildSaveAssessmentSheetRecordsRequest(initialized, [
      { id: 'assessment-1', code: 'A01' } as any,
      { id: 'assessment-2', code: 'A02' } as any
    ]);

    expect(request.records.map(record => record.finalGrade)).toEqual(['B', 'C']);
  });
});

describe('Assessment sheet records table layout', () => {
  const record = (id: string, groupLv2Name: string, groupLv3Name: string, rowIndex: number) => ({
    id,
    assessment: {
      code: `A${rowIndex}`,
      name: `Mục ${rowIndex}`,
      groupLv2Name,
      groupLv3Name,
      rowIndex
    }
  } as any);

  it('maps fixed groupLv2 colors with Vietnamese-insensitive names', () => {
    expect(ASSESSMENT_GROUP_LV2_CONFIGS.find(config => config.key === 'Tiền tiểu học')?.bgcolor).toBe('#DCC1CF');
    expect(ASSESSMENT_GROUP_LV2_CONFIGS.find(config => config.key === 'Cá nhân và xã hội')?.bgcolor).toBe('#D0E0E3');
    expect(assessmentGroupLv2Color(' Tiền tiểu học ')).toBe('#DCC1CF');
    expect(assessmentGroupLv2Color('PHAT TRIEN THE CHAT')).toBe('#C9DAF8');
    expect(assessmentGroupLv2Color('Nhóm khác')).toBe('#FFFFFF');
  });

  it('maps grade badge colors from the shared grade options', () => {
    expect(assessmentGradeText('A')).toBe('Đạt +');
    expect(assessmentGradeColor('A')).toBe('#11734b');
    expect(assessmentGradeBgColor('A')).toBe('#d4edbc');
    expect(assessmentGradeText(null)).toBe('Chưa có');
    expect(assessmentGradeColor(null)).toBe('#344054');
    expect(assessmentGradeBgColor(null)).toBe('#E8EAED');
  });

  it('builds grouped table rows with rowspans and row numbers per groupLv3', () => {
    const rows = buildAssessmentSheetRecordRows([
      record('record-1', 'Phát triển thể chất', 'Vận động thô', 1),
      record('record-2', 'Phát triển thể chất', 'Vận động thô', 2),
      record('record-3', 'Phát triển thể chất', 'Vận động tinh', 3),
      record('record-4', 'Phát triển nhận thức', 'Làm quen toán', 4)
    ]);

    expect(rows.map(row => ({
      id: row.record.id,
      showGroupLv2: row.showGroupLv2,
      groupLv2RowSpan: row.groupLv2RowSpan,
      showGroupLv3: row.showGroupLv3,
      groupLv3RowSpan: row.groupLv3RowSpan,
      rowNumber: row.rowNumber,
      groupColor: row.groupColor
    }))).toEqual([
      {
        id: 'record-1',
        showGroupLv2: true,
        groupLv2RowSpan: 3,
        showGroupLv3: true,
        groupLv3RowSpan: 2,
        rowNumber: 1,
        groupColor: '#C9DAF8'
      },
      {
        id: 'record-2',
        showGroupLv2: false,
        groupLv2RowSpan: 1,
        showGroupLv3: false,
        groupLv3RowSpan: 1,
        rowNumber: 2,
        groupColor: '#C9DAF8'
      },
      {
        id: 'record-3',
        showGroupLv2: false,
        groupLv2RowSpan: 1,
        showGroupLv3: true,
        groupLv3RowSpan: 1,
        rowNumber: 1,
        groupColor: '#C9DAF8'
      },
      {
        id: 'record-4',
        showGroupLv2: true,
        groupLv2RowSpan: 1,
        showGroupLv3: true,
        groupLv3RowSpan: 1,
        rowNumber: 1,
        groupColor: '#C7B7D2'
      }
    ]);
  });

  it('sorts rows by the shared groupLv2 display order before grouping', () => {
    const rows = buildAssessmentSheetRecordRows([
      record('record-1', 'Tiền tiểu học', 'Chuẩn bị', 1),
      record('record-2', 'Phát triển ngôn ngữ', 'Ngôn ngữ', 2),
      record('record-3', 'Nhóm ngoài cấu hình', 'Khác', 3),
      record('record-4', 'Phát triển thể chất', 'Vận động', 4),
      record('record-5', 'Cá nhân và xã hội', 'Xã hội', 5)
    ]);

    expect(ASSESSMENT_GROUP_LV2_CONFIGS.map(config => ({
      key: config.key,
      displayOrder: config.displayOrder,
      bgcolor: config.bgcolor
    }))).toEqual([
      { key: 'Phát triển thể chất', displayOrder: 1, bgcolor: '#C9DAF8' },
      { key: 'Phát triển nhận thức', displayOrder: 2, bgcolor: '#C7B7D2' },
      { key: 'Phát triển ngôn ngữ', displayOrder: 3, bgcolor: '#C9DAF8' },
      { key: 'Cá nhân và xã hội', displayOrder: 4, bgcolor: '#D0E0E3' },
      { key: 'Tiền tiểu học', displayOrder: 5, bgcolor: '#DCC1CF' }
    ]);
    expect(rows.map(row => row.record.id)).toEqual([
      'record-4',
      'record-2',
      'record-5',
      'record-1',
      'record-3'
    ]);
  });
});

describe('Assessment sheet edit permissions', () => {
  it('allows record structure changes only while the selected status is Open', () => {
    expect(canMutateAssessmentSheetRecords('Open')).toBeTrue();
    expect(canMutateAssessmentSheetRecords('Planed')).toBeFalse();
    expect(canMutateAssessmentSheetRecords('Done')).toBeFalse();
    expect(canMutateAssessmentSheetRecords(null)).toBeFalse();
  });

  it('keeps final grade and final note editable for Planed but locked for Done', () => {
    expect(canEditAssessmentSheetRecordValues('Open')).toBeTrue();
    expect(canEditAssessmentSheetRecordValues('Planed')).toBeTrue();
    expect(canEditAssessmentSheetRecordValues('Done')).toBeFalse();
    expect(canEditAssessmentSheetRecordValues(null)).toBeFalse();
  });
});

describe('Assessment sheet form DevExtreme option stability', () => {
  const createComponent = (mode = 'create'): AssessmentSheetFormComponent =>
    new AssessmentSheetFormComponent(
      {} as any,
      {} as any,
      { user: { role: 'Admin' } } as any,
      {} as any,
      {} as any,
      { snapshot: { data: { mode }, paramMap: { get: () => null } } } as any,
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

  it('keeps status readonly on create and editable on edit', () => {
    const createMode = createComponent('create');
    createMode.ngOnInit();
    expect(createMode.statusEditorOptions['readOnly']).toBeTrue();

    const editMode = createComponent('edit');
    editMode.ngOnInit();
    expect(editMode.statusEditorOptions['readOnly']).toBeFalse();
  });

  it('reports selected assessment count for create and edit modes', () => {
    const component = createComponent();

    component.editor.assessmentIds = ['assessment-1', 'assessment-2', 'assessment-1', ''];
    expect(component.selectedAssessmentCount).toBe(2);

    component.isCreate = false;
    component.records = [{ id: 'record-1' } as any, { id: 'record-2' } as any, { id: 'record-3' } as any];
    expect(component.selectedAssessmentCount).toBe(3);
  });

  it('locks add and remove immediately from the currently selected status while keeping Planed record values editable', () => {
    const component = createComponent('edit');
    component.isCreate = false;
    component.editor.status = 'Open';
    expect(component.canMutateRecords).toBeTrue();
    expect(component.recordValueControlsDisabled).toBeFalse();

    component.editor.status = 'Planed';
    expect(component.canMutateRecords).toBeFalse();
    expect(component.recordValueControlsDisabled).toBeFalse();
    expect(component.recordMutationLockHint).toContain('Kế hoạch');

    component.editor.status = 'Done';
    expect(component.canMutateRecords).toBeFalse();
    expect(component.recordValueControlsDisabled).toBeTrue();
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

  it('emits add action only for rows not already in the sheet', () => {
    const { component } = createPicker();
    const emitted: string[] = [];
    component.mode = 'add';
    component.existingCodes = ['A01'];
    component.ngOnChanges({ existingCodes: {} as any });
    component.assessmentAdd.subscribe(value => emitted.push(value.id));

    component.onSelectCheckboxChanged('assessment-2', { value: true, event: {} });
    component.onAddAssessmentClick(assessment({ id: 'assessment-1', code: 'A01' }));
    component.onAddAssessmentClick(assessment({ id: 'assessment-2', code: 'A02' }));

    expect(emitted).toEqual(['assessment-2']);
    expect(component.isExistingAssessment(assessment({ code: 'A01' }))).toBeTrue();
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
          assessment({ id: 'assessment-1', code: 'NN01', name: 'Ngôn ngữ', groupLv1Name: '3-4 tuổi', latestGrade: 'A' }),
          assessment({ id: 'assessment-2', code: 'TC01', name: 'Thể chất', groupLv1Name: '4-5 tuổi', groupLv2Name: 'Vận động', latestGrade: 'B' })
        ],
        pagination: { page: 1, pageSize: 100, totalItems: 3, totalPages: 2 }
      }),
      of({
        items: [
          assessment({ id: 'assessment-3', code: 'TM01', name: 'Thẩm mỹ', groupLv1Name: '3-4 tuổi', groupLv2Name: 'Nghệ thuật', latestGrade: 'C' })
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
    component.latestGradeFilters = ['A'];
    component.applyFilters();

    expect(assessments.list).toHaveBeenCalledTimes(2);
    expect(component.filteredAssessments.map(item => item.id)).toEqual(['assessment-1']);

    component.latestGradeFilters = ['B'];
    component.applyFilters();

    expect(assessments.list).toHaveBeenCalledTimes(2);
    expect(component.filteredAssessments).toEqual([]);
  });

  it('returns selected assessment details from the client cache', () => {
    const { component } = createPicker();
    component.allAssessments = [
      assessment({ id: 'assessment-1', latestGrade: 'A', latestNote: 'Ghi chú 1' }),
      assessment({ id: 'assessment-2', latestGrade: 'B', latestNote: 'Ghi chú 2' })
    ];
    component.selectedIds = ['assessment-2', 'missing', 'assessment-1'];
    component.ngOnChanges({ selectedIds: {} as any });

    expect(component.getSelectedAssessments().map(item => ({
      id: item.id,
      latestGrade: item.latestGrade,
      latestNote: item.latestNote
    }))).toEqual([
      { id: 'assessment-2', latestGrade: 'B', latestNote: 'Ghi chú 2' },
      { id: 'assessment-1', latestGrade: 'A', latestNote: 'Ghi chú 1' }
    ]);
  });

  it('passes selected student id when loading the assessment cache', async () => {
    const { component, assessments } = createPicker();
    component.studentId = ' student-1 ';
    assessments.list.and.returnValue(of({
      items: [assessment({ id: 'assessment-1', latestGrade: 'A', latestNote: 'Cáº§n quan sÃ¡t thÃªm' })],
      pagination: { page: 1, pageSize: 100, totalItems: 1, totalPages: 1 }
    }));

    await component.loadAssessmentsFromServer();

    expect(assessments.list).toHaveBeenCalledWith({
      page: 1,
      pageSize: 100,
      sortBy: 'rowindex',
      sortOrder: 'asc',
      studentId: 'student-1'
    });
    expect(component.filteredAssessments[0].latestGrade).toBe('A');
    expect(component.filteredAssessments[0].latestNote).toBe('Cáº§n quan sÃ¡t thÃªm');
    expect(component.latestGradeText('A')).toBe('Đạt +');
    expect(component.latestGradeText(null)).toBe('-');
  });

  it('reloads the assessment cache when the selected student changes', () => {
    const { component } = createPicker();
    (component as any).initialized = true;
    (component as any).loadedStudentId = 'student-1';
    component.studentId = 'student-2';
    spyOn(component, 'loadAssessmentsFromServer').and.returnValue(Promise.resolve());

    component.ngOnChanges({ studentId: {} as any });

    expect(component.loadAssessmentsFromServer).toHaveBeenCalled();
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
      assessment({ id: 'assessment-1', code: 'NN01', name: 'Ngôn ngữ', latestGrade: 'A' }),
      assessment({ id: 'assessment-2', code: 'TC01', name: 'Thể chất', latestGrade: 'B' })
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
    component.latestGradeFilters = ['B'];
    component.applyFilters();

    expect(component.filteredAssessments.map(item => item.id)).toEqual(['assessment-2']);
    expect(assessments.list).not.toHaveBeenCalled();

    component.latestGradeFilters = ['A'];
    component.applyFilters();

    expect(component.filteredAssessments).toEqual([]);

    component.onViewModeChanged();

    expect(component.filteredAssessments).toEqual([]);
  });

  it('clears latest grade filter when resetting filters', () => {
    const { component } = createPicker();
    component.allAssessments = [
      assessment({ id: 'assessment-1', latestGrade: 'A' }),
      assessment({ id: 'assessment-2', latestGrade: 'B' })
    ];
    component.latestGradeFilters = ['A'];
    component.applyFilters();

    expect(component.filteredAssessments.map(item => item.id)).toEqual(['assessment-1']);

    component.resetFilters();

    expect(component.latestGradeFilters).toEqual([]);
    expect(component.filteredAssessments.map(item => item.id)).toEqual(['assessment-1', 'assessment-2']);
  });

  it('filters assessments without latest grade using Chưa có option', () => {
    const { component, assessments } = createPicker();
    component.allAssessments = [
      assessment({ id: 'assessment-1', latestGrade: null }),
      assessment({ id: 'assessment-2', latestGrade: '' }),
      assessment({ id: 'assessment-3', latestGrade: 'A' })
    ];
    component.latestGradeFilters = ['none'];

    component.applyFilters();

    expect(component.latestGradeOptions.map(option => option.text)).toEqual(['Chưa có', 'Đạt +', 'Chưa đạt -', 'Hỗ trợ +', 'Hỗ trợ -']);
    expect(component.filteredAssessments.map(item => item.id)).toEqual(['assessment-1', 'assessment-2']);
    expect(assessments.list).not.toHaveBeenCalled();
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
