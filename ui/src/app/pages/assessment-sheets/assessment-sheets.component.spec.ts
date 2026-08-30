import { of } from 'rxjs';
import { ASSESSMENT_GROUP_LV2_CONFIGS } from '../../core/models/api.models.assessment-sheets';
import { AssessmentPickerComponent } from './assessment-picker.component';
import { AssessmentSheetsComponent } from './assessment-sheets.component';
import {
  buildAssessmentSheetPlanPreview,
  buildAssessmentSheetResultPreview,
  buildPlanPdfFileName,
  buildResultPdfFileName,
  calculateAgeText,
  formatAssessmentPeriod,
  planGradeText,
  planNoteText,
  resultGradeText,
  resultNoteText
} from './assessment-sheet-plan-preview.models';
import {
  AssessmentSheetFormComponent,
  AssessmentSheetEditor,
  assessmentGradeBgColor,
  assessmentGradeColor,
  assessmentGradeText,
  assessmentGroupLv2Color,
  buildAssessmentSheetRecordGroupTarget,
  buildAssessmentSheetRecordRows,
  buildCreateAssessmentSheetRequest,
  buildRemoveAssessmentSheetRecordRequest,
  buildReplaceAssessmentSheetRecordsRequest,
  buildSaveAssessmentSheetRecordsRequest,
  buildUpdateAssessmentSheetRequest,
  deriveLoadedGroupLv2Order,
  canEditAssessmentSheetRecordGroups,
  canEditAssessmentSheetRecordValues,
  canMutateAssessmentSheetRecords,
  canUpdateAssessmentCatalogGroups,
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
    planFileLinkPdf: '',
    resultFileLinkPdf: '',
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
      planFileLinkPdf: null,
      resultFileLinkPdf: null,
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
        finalNote: 'Kết quả cũ',
        displayOrder: 5
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
          finalNote: 'Kết quả cũ',
          displayOrder: 5,
          groupLv2Name: null,
          groupLv3Name: null
        },
        {
          assessmentId: 'assessment-2',
          planGrade: 'A',
          planNote: 'Ghi chú gần nhất',
          finalGrade: null,
          finalNote: null,
          displayOrder: null,
          groupLv2Name: null,
          groupLv3Name: null
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
          finalNote: 'Kết quả',
          displayOrder: null,
          groupLv2Name: null,
          groupLv3Name: null
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
          finalNote: 'Đã đạt',
          displayOrder: null,
          groupLv2Name: null,
          groupLv3Name: null
        }
      ]
    });
  });

  it('keeps empty current results empty while preserving plan grade and plan note', () => {
    const initialized = initializeAssessmentSheetRecords([
      {
        id: 'record-1',
        assessment: { code: 'A01', name: 'Ngôn ngữ' },
        planGrade: 'B',
        planNote: 'Kế hoạch',
        finalGrade: null,
        finalNote: null
      } as any,
      {
        id: 'record-2',
        assessment: { code: 'A02', name: 'Vận động' },
        planGrade: 'A',
        planNote: 'Đang luyện',
        finalGrade: 'C'
      } as any
    ]);

    expect(initialized.map(record => record.finalGrade)).toEqual([null, 'C']);
    expect(initialized.map(record => record.planGrade)).toEqual(['B', 'A']);
    expect(initialized.map(record => record.planNote)).toEqual(['Kế hoạch', 'Đang luyện']);

    const request = buildSaveAssessmentSheetRecordsRequest(initialized, [
      { id: 'assessment-1', code: 'A01' } as any,
      { id: 'assessment-2', code: 'A02' } as any
    ]);

    expect(request.records.map(record => record.finalGrade)).toEqual([null, 'C']);
    expect(request.records.map(record => record.planGrade)).toEqual(['B', 'A']);
    expect(request.records.map(record => record.planNote)).toEqual(['Kế hoạch', 'Đang luyện']);
  });
});

describe('Assessment sheets Excel import', () => {
  const xlsxFile = () => new File(['excel'], 'bang-danh-gia.xlsx', {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
  });

  const createComponent = (previewResult: any, importResult: any) => {
    const assessmentSheets = {
      list: jasmine.createSpy('list'),
      previewImportExcel: jasmine.createSpy('previewImportExcel').and.returnValue(of(previewResult)),
      importExcel: jasmine.createSpy('importExcel').and.returnValue(of(importResult))
    };
    const component = new AssessmentSheetsComponent(
      assessmentSheets as any,
      {} as any,
      { navigate: jasmine.createSpy('navigate') } as any
    );
    const refresh = jasmine.createSpy('refresh').and.returnValue(Promise.resolve());
    component.grid = {
      instance: {
        refresh,
        pageIndex: jasmine.createSpy('pageIndex')
      }
    } as any;
    return { component, assessmentSheets, refresh };
  };

  it('previews an xlsx file before submitting and imports only after confirmation action', async () => {
    const previewResult = {
      summary: {
        canImport: true,
        validRowCount: 2,
        errorCount: 0,
        warningCount: 1,
        skippedDuplicateRowCount: 0,
        groups: 1
      },
      rows: [
        {
          rowNumber: 2,
          assessmentCode: 'A01',
          studentCode: 'S101',
          studentName: 'Bé An',
          startDate: '2026-08-01',
          dueDate: '2026-08-31',
          stt: 1,
          groupLv2Name: 'PHÁT TRIỂN THỂ CHẤT',
          groupLv3Name: 'vận động thô',
          action: 'Created',
          errors: [],
          warnings: ['Sẽ tạo mới']
        }
      ]
    };
    const importResult = {
      createdSheetCount: 1,
      updatedSheetCount: 1,
      importedRecordCount: 5,
      skippedDuplicateRowCount: 0,
      warnings: ['Một dòng có ghi chú dài'],
      sheets: [{ id: 'sheet-1' }]
    };
    const { component, assessmentSheets, refresh } = createComponent(previewResult, importResult);
    const file = xlsxFile();

    await component.previewImportExcel({ target: { files: [file], value: 'C:\\fakepath\\bang-danh-gia.xlsx' } } as any);

    expect(assessmentSheets.previewImportExcel).toHaveBeenCalledWith(file);
    expect(component.importPreviewVisible).toBeTrue();
    expect(component.importPreviewRows.length).toBe(1);
    expect(component.importPreviewRows[0].stt).toBe(1);
    expect(component.importPreviewRows[0].groupLv2Name).toBe('PHÁT TRIỂN THỂ CHẤT');
    expect(component.importPreviewRows[0].groupLv3Name).toBe('vận động thô');
    expect(component.importActionText('Created')).toBe('Tạo mới');
    expect(component.importMessagesText(['A', 'B'])).toBe('A; B');
    expect(component.canSubmitImport).toBeTrue();

    await component.submitImportExcel();

    expect(assessmentSheets.importExcel).toHaveBeenCalledWith(file);
    expect(component.importPreviewVisible).toBeFalse();
    expect(component.importResult?.createdSheetCount).toBe(1);
    expect(component.importSuccessText(component.importResult as any)).toContain('tạo mới 1');
    expect(refresh).toHaveBeenCalled();
  });

  it('keeps submit disabled when preview reports validation errors', async () => {
    const previewResult = {
      summary: {
        canImport: false,
        validRowCount: 0,
        errorCount: 1,
        warningCount: 0,
        skippedDuplicateRowCount: 1,
        groups: 0
      },
      rows: [
        {
          rowNumber: 2,
          assessmentCode: '',
          studentCode: 'S101',
          studentName: 'Bé An',
          startDate: null,
          dueDate: null,
          action: 'SkippedDuplicate',
          errors: ['Thiếu mã đánh giá'],
          warnings: []
        }
      ]
    };
    const { component, assessmentSheets } = createComponent(previewResult, {
      createdSheetCount: 0,
      updatedSheetCount: 0,
      importedRecordCount: 0,
      skippedDuplicateRowCount: 0,
      warnings: [],
      sheets: []
    });

    await component.previewImportExcel({ target: { files: [xlsxFile()], value: 'C:\\fakepath\\loi.xlsx' } } as any);

    expect(component.importRowHasErrors(component.importPreviewRows[0])).toBeTrue();
    expect(component.importDuplicateText(component.importPreviewRows[0])).toBe('Trùng');
    expect(component.canSubmitImport).toBeFalse();

    await component.submitImportExcel();

    expect(assessmentSheets.importExcel).not.toHaveBeenCalled();
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

  it('groups by the snapshot groupLv2/groupLv3 names even when they differ from the assessment catalog casing', () => {
    // Import khcn ghi tên nhóm từ file vào snapshot (thường viết HOA); form phải nhóm/tô màu theo snapshot.
    const rows = buildAssessmentSheetRecordRows([
      record('record-1', 'PHÁT TRIỂN THỂ CHẤT', 'VẬN ĐỘNG THÔ', 10),
      record('record-2', 'PHÁT TRIỂN THỂ CHẤT', 'VẬN ĐỘNG THÔ', 11),
      record('record-3', 'PHÁT TRIỂN THỂ CHẤT', 'VẬN ĐỘNG TINH', 12)
    ]);

    expect(rows.map(row => row.record.id)).toEqual(['record-1', 'record-2', 'record-3']);
    expect(rows[0].groupColor).toBe('#C9DAF8');
    expect(rows[0].groupLv2RowSpan).toBe(3);
    expect(rows[0].groupLv3RowSpan).toBe(2);
    expect(rows[2].showGroupLv3).toBeTrue();
    expect(rows[2].rowNumber).toBe(1);
  });

  it('maps grade badge colors from the shared grade options', () => {
    expect(assessmentGradeText('A')).toBe('Đạt +');
    expect(assessmentGradeColor('A')).toBe('#11734b');
    expect(assessmentGradeBgColor('A')).toBe('#d4edbc');
    expect(assessmentGradeText(null)).toBe('');
    expect(assessmentGradeColor(null)).toBe('');
    expect(assessmentGradeBgColor(null)).toBe('');
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

  it('targets only the contiguous records represented by the clicked merged group cell', () => {
    const rows = buildAssessmentSheetRecordRows([
      record('record-1', 'Phát triển thể chất', 'Vận động', 1),
      record('record-2', 'Phát triển thể chất', 'Thăng bằng', 2),
      record('record-3', 'Phát triển thể chất', 'Vận động', 3)
    ]);

    expect(buildAssessmentSheetRecordGroupTarget(rows, rows[0], 2).recordIds).toEqual([
      'record-1',
      'record-2',
      'record-3'
    ]);
    expect(buildAssessmentSheetRecordGroupTarget(rows, rows[0], 3).recordIds).toEqual(['record-1']);
    expect(buildAssessmentSheetRecordGroupTarget(rows, rows[2], 3).recordIds).toEqual(['record-3']);
  });

  it('exposes distinct assessment codes for the clicked merged group cell (used by the catalog update button)', () => {
    const rows = buildAssessmentSheetRecordRows([
      record('record-1', 'Phát triển ngôn ngữ', 'Nghe và nói', 1),
      record('record-2', 'Phát triển ngôn ngữ', 'Nghe và nói', 2)
    ]);
    const target = buildAssessmentSheetRecordGroupTarget(rows, rows[0], 3);

    expect(target.level).toBe(3);
    expect(target.recordIds).toEqual(['record-1', 'record-2']);
    expect(target.assessmentCodes).toEqual(['A1', 'A2']);
    expect(target.expectedGroupLv2Name).toBe('Phát triển ngôn ngữ');
    expect(target.expectedGroupLv3Name).toBe('Nghe và nói');
    expect(target.currentName).toBe('Nghe và nói');
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

  it('allows snapshot group edits for all portal roles in Open or Planed, but locks Done', () => {
    expect(canEditAssessmentSheetRecordGroups('Open', 'Teacher')).toBeTrue();
    expect(canEditAssessmentSheetRecordGroups('Planed', 'Admin')).toBeTrue();
    expect(canEditAssessmentSheetRecordGroups('Planed', 'SuperAdmin')).toBeTrue();
    expect(canEditAssessmentSheetRecordGroups('Done', 'SuperAdmin')).toBeFalse();
    expect(canEditAssessmentSheetRecordGroups('Open', null)).toBeFalse();
  });

  it('allows updating original assessments only for Admin and SuperAdmin', () => {
    expect(canUpdateAssessmentCatalogGroups('Teacher')).toBeFalse();
    expect(canUpdateAssessmentCatalogGroups('Admin')).toBeTrue();
    expect(canUpdateAssessmentCatalogGroups('SuperAdmin')).toBeTrue();
  });
});

describe('Assessment sheet plan PDF preview mapping', () => {
  it('calculates assessment period from start and due dates', () => {
    expect(formatAssessmentPeriod('2026-06-01', '2026-08-31')).toBe('3 tháng 6.7.8.26');
    expect(formatAssessmentPeriod(null, '2026-08-31')).toContain('Chưa có đủ');
    expect(formatAssessmentPeriod('2026-09-01', '2026-08-31')).toContain('đang trước');
  });

  it('calculates student age at the start date', () => {
    expect(calculateAgeText('2019-12-05', '2026-08-01')).toBe('6 tuổi, 7 tháng');
    expect(calculateAgeText('2026-08-01', null)).toBe('Chưa có thông tin');
  });

  it('maps sheet detail to printable plan rows using plan fields', () => {
    const preview = buildAssessmentSheetPlanPreview({
      id: 'sheet-1',
      studentId: 'student-1',
      studentSnapshot: {
        studentCode: 'S 101',
        fullName: 'Bé An',
        nickName: 'An',
        dateOfBirth: '2020-01-15'
      },
      startDate: '2026-06-01',
      dueDate: '2026-08-31',
      records: [
        {
          id: 'record-1',
          assessment: {
            code: 'B97',
            name: 'Đứng một chân',
            groupLv2Name: 'Tiền tiểu học',
            groupLv3Name: 'Vận động'
          },
          planGrade: 'B',
          planNote: '  Cần luyện thêm  ',
          finalGrade: 'A',
          finalNote: 'Không dùng cho kế hoạch'
        } as any
      ]
    } as any);

    expect(preview.studentName).toBe('Bé An');
    expect(preview.periodText).toBe('3 tháng 6.7.8.26');
    expect(preview.fileName).toBe('khcn - s-101.an_6.7.8.26.pdf');
    expect(planGradeText(preview.rows[0].record)).toBe('Hỗ trợ +');
    expect(planNoteText(preview.rows[0].record)).toBe('Cần luyện thêm');
    expect(preview.rows[0].groupLv2Name).toBe('Tiền tiểu học');
  });

  it('maps sheet detail to printable result rows using final fields', () => {
    const preview = buildAssessmentSheetResultPreview({
      id: 'sheet-1',
      studentId: 'student-1',
      studentSnapshot: {
        studentCode: 'S 101',
        fullName: 'Bé An',
        nickName: 'An',
        dateOfBirth: '2020-01-15'
      },
      startDate: '2026-03-01',
      dueDate: '2026-06-26',
      records: [
        {
          id: 'record-1',
          assessment: {
            code: 'B97',
            name: 'Đứng một chân',
            groupLv2Name: 'Tiền tiểu học',
            groupLv3Name: 'Vận động'
          },
          planGrade: 'B',
          planNote: 'Không dùng cho kết quả',
          finalGrade: 'A',
          finalNote: '  Đã đạt mục tiêu  '
        } as any
      ]
    } as any);

    expect(preview.kind).toBe('result');
    expect(preview.documentTitle).toBe('KẾT QUẢ ĐÁNH GIÁ');
    expect(preview.sectionTitle).toBe('2. Kết quả đánh giá');
    expect(preview.tableLabel).toBe('Kết quả đánh giá');
    expect(preview.fileName).toBe('kq - s-101.an_3.4.5.26.pdf');
    expect(resultGradeText(preview.rows[0].record)).toBe('Đạt +');
    expect(resultNoteText(preview.rows[0].record)).toBe('Đã đạt mục tiêu');
  });

  it('builds safe PDF file names', () => {
    expect(buildPlanPdfFileName('Số 01', 'Bé An', '2026-03-01', '2026-06-26')).toBe('khcn - so-01.be-an_3.4.5.26.pdf');
    expect(buildPlanPdfFileName('S101', null, '2026-06-01', '2026-08-31')).toBe('khcn - s101_6.7.8.26.pdf');
    expect(buildPlanPdfFileName('', '', null, null)).toBe('khcn - hoc-sinh.pdf');
    expect(buildResultPdfFileName('Số 01', 'Bé An', '2026-03-01', '2026-06-26')).toBe('kq - so-01.be-an_3.4.5.26.pdf');
    expect(buildResultPdfFileName('S101', null, '2026-06-01', '2026-08-31')).toBe('kq - s101_6.7.8.26.pdf');
    expect(buildResultPdfFileName('', '', null, null)).toBe('kq - hoc-sinh.pdf');
  });
});

describe('Assessment sheet form DevExtreme option stability', () => {
  const createComponent = (mode = 'create', role = 'Admin'): AssessmentSheetFormComponent =>
    new AssessmentSheetFormComponent(
      {} as any,
      {} as any,
      { user: { role } } as any,
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

  it('allows opening the plan PDF preview only for saved edit sheets past Open status with records', () => {
    const component = createComponent('edit');
    component.isCreate = false;
    component.loading = false;
    component.saving = false;
    component.records = [{ id: 'record-1' } as any];

    component.originalStatus = 'Open';
    expect(component.canOpenPlanPdfPreview()).toBeFalse();

    component.originalStatus = 'Planed';
    expect(component.canOpenPlanPdfPreview()).toBeTrue();

    component.originalStatus = 'Done';
    expect(component.canOpenPlanPdfPreview()).toBeTrue();

    component.records = [];
    expect(component.canOpenPlanPdfPreview()).toBeFalse();
  });

  it('allows opening the result PDF preview only for saved edit sheets past Open status with records', () => {
    const component = createComponent('edit');
    component.isCreate = false;
    component.loading = false;
    component.saving = false;
    component.records = [{ id: 'record-1' } as any];

    component.originalStatus = 'Open';
    expect(component.canOpenResultPdfPreview()).toBeFalse();

    component.originalStatus = 'Planed';
    expect(component.canOpenResultPdfPreview()).toBeTrue();

    component.originalStatus = 'Done';
    expect(component.canOpenResultPdfPreview()).toBeTrue();

    component.records = [];
    expect(component.canOpenResultPdfPreview()).toBeFalse();
  });

  it('shows submit results only for Done sheets and disables it for Teacher role', () => {
    const component = createComponent('edit');
    component.isCreate = false;
    component.assessmentSheetId = 'sheet-1';
    component.records = [{ id: 'record-1' } as any];

    component.originalStatus = 'Planed';
    expect(component.canShowSubmitResults).toBeFalse();
    expect(component.canSubmitResults).toBeFalse();

    component.originalStatus = 'Done';
    expect(component.canShowSubmitResults).toBeTrue();
    expect(component.canSubmitResults).toBeTrue();

    component.submittingResults = true;
    expect(component.canShowSubmitResults).toBeTrue();
    expect(component.canSubmitResults).toBeFalse();

    component.submittingResults = false;
    component.saving = true;
    expect(component.canSubmitResults).toBeFalse();

    component.saving = false;
    component.records = [];
    expect(component.canSubmitResults).toBeFalse();

    component.records = [{ id: 'record-1' } as any];
    component.isCreate = true;
    expect(component.canShowSubmitResults).toBeFalse();
    expect(component.canSubmitResults).toBeFalse();

    const teacherComponent = createComponent('edit', 'Teacher');
    teacherComponent.isCreate = false;
    teacherComponent.assessmentSheetId = 'sheet-1';
    teacherComponent.records = [{ id: 'record-1' } as any];
    teacherComponent.originalStatus = 'Done';
    expect(teacherComponent.canShowSubmitResults).toBeTrue();
    expect(teacherComponent.canSubmitResults).toBeFalse();
  });

  const buildSubmitPreview = (overrides: any = {}) => ({
    gradeSummary: [
      { grade: 'A', label: 'Đạt +', count: 1 },
      { grade: 'B', label: 'Hỗ trợ +', count: 0 },
      { grade: 'C', label: 'Hỗ trợ -', count: 0 },
      { grade: 'D', label: 'Chưa đạt -', count: 0 },
      { grade: null, label: 'Chưa có kết quả', count: 0 }
    ],
    totalRecords: 1,
    totalChangedCells: 1,
    changes: [
      { cell: 'H45', kind: 'FinalGrade', assessmentCode: 'A01', assessmentName: 'Ngôn ngữ', currentValue: 'Hỗ trợ +', newValue: 'Đạt +' }
    ],
    ...overrides
  });

  const buildSubmitComponent = (assessmentSheets: any) => {
    const component = new AssessmentSheetFormComponent(
      assessmentSheets as any,
      {} as any,
      { user: { role: 'Admin' } } as any,
      {} as any,
      {} as any,
      { snapshot: { data: { mode: 'edit' }, paramMap: { get: () => 'sheet-1' } } } as any,
      {} as any
    );
    component.isCreate = false;
    component.assessmentSheetId = 'sheet-1';
    component.originalStatus = 'Done';
    component.records = [{ id: 'record-1', assessment: { code: 'A01', name: 'Ngôn ngữ' } } as any];
    (component as any).baseline = (component as any).serialize(component.editor);
    return component;
  };

  it('opens the confirm popup from the dry-run preview instead of submitting immediately', async () => {
    const preview = buildSubmitPreview();
    const assessmentSheets = {
      previewSubmitResults: jasmine.createSpy('previewSubmitResults').and.returnValue(of(preview)),
      submitResults: jasmine.createSpy('submitResults')
    };
    const component = buildSubmitComponent(assessmentSheets);

    await component.submitResults();

    expect(assessmentSheets.previewSubmitResults).toHaveBeenCalledWith('sheet-1');
    expect(assessmentSheets.submitResults).not.toHaveBeenCalled();
    expect(component.submitConfirmVisible).toBeTrue();
    expect(component.submitPreview).toEqual(preview);
    expect(component.submitPreview!.gradeSummary.map(s => s.label))
      .toEqual(['Đạt +', 'Hỗ trợ +', 'Hỗ trợ -', 'Chưa đạt -', 'Chưa có kết quả']);
  });

  it('does not open the popup when the dry-run reports no changed cells', async () => {
    const assessmentSheets = {
      previewSubmitResults: jasmine.createSpy('previewSubmitResults')
        .and.returnValue(of(buildSubmitPreview({ totalChangedCells: 0, changes: [] }))),
      submitResults: jasmine.createSpy('submitResults')
    };
    const component = buildSubmitComponent(assessmentSheets);

    await component.submitResults();

    expect(component.submitConfirmVisible).toBeFalse();
    expect(component.submitPreview).toBeNull();
    expect(assessmentSheets.submitResults).not.toHaveBeenCalled();
  });

  it('calls submit-results and applies the returned sheet detail after the popup is confirmed', async () => {
    const savedSheet = {
      id: 'sheet-1',
      status: 'Done',
      studentId: 'student-1',
      studentSnapshot: { studentCode: 'S101', fullName: 'Bé An', nickName: 'An' },
      responsibleTeacherFullName: 'Cô Lan',
      note: null,
      feedback: null,
      records: [
        {
          id: 'record-1',
          assessment: { code: 'A01', name: 'Ngôn ngữ' },
          planGrade: 'B',
          finalGrade: 'A',
          finalNote: 'Đã đạt',
          createdAt: '',
          updatedAt: ''
        }
      ],
      createdAt: '',
      updatedAt: '',
      submissionDate: '2026-08-27T06:00:00Z'
    } as any;
    const assessmentSheets = {
      previewSubmitResults: jasmine.createSpy('previewSubmitResults').and.returnValue(of(buildSubmitPreview())),
      submitResults: jasmine.createSpy('submitResults').and.returnValue(of(savedSheet))
    };
    const component = buildSubmitComponent(assessmentSheets);

    await component.submitResults();
    await component.confirmSubmitResults();

    expect(assessmentSheets.submitResults).toHaveBeenCalledWith('sheet-1');
    expect(component.submitConfirmVisible).toBeFalse();
    expect(component.submitPreview).toBeNull();
    expect(component.originalStatus).toBe('Done');
    expect(component.studentSummary).toBe('S101 · Bé An (An)');
    expect(component.records[0].finalGrade).toBe('A');
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

  const groupRecord = (id: string, code: string, groupLv2Name: string, groupLv3Name: string) => ({
    id,
    assessment: { code, name: `Mục ${code}`, groupLv2Name, groupLv3Name }
  } as any);

  it('opens the group popup with the current snapshot name for Teacher on a Planed sheet', () => {
    const component = createComponent('edit', 'Teacher');
    const records = [groupRecord('record-1', 'A01', 'Phát triển ngôn ngữ', 'Nghe và nói')];
    component.isCreate = false;
    component.originalStatus = 'Planed';
    component.editor.status = 'Planed';
    component.records = records;
    component.recordRows = buildAssessmentSheetRecordRows(records);
    (component as any).baseline = (component as any).serialize(component.editor);

    component.openGroupEdit(component.recordRows[0], 3);

    expect(component.groupEditVisible).toBeTrue();
    expect(component.groupEditTarget?.recordIds).toEqual(['record-1']);
    expect(component.groupEditName).toBe('Nghe và nói');
    expect(component.canUpdateAssessmentGroups).toBeFalse();
  });

  it('applyGroupEdit only mutates the UI snapshot, marks the form dirty, and does not call the API', () => {
    const component = createComponent('edit', 'Teacher');
    const records = [
      groupRecord('record-1', 'A01', 'Phát triển ngôn ngữ', 'Nghe và nói'),
      groupRecord('record-2', 'A02', 'Phát triển ngôn ngữ', 'Nghe và nói')
    ];
    component.isCreate = false;
    component.originalStatus = 'Open';
    component.editor.status = 'Open';
    (component as any).applyAssessmentSheet({
      id: 'sheet-1',
      status: 'Open',
      studentId: 's-1',
      studentSnapshot: {},
      records
    } as any);

    expect(component.dirty).toBeFalse();
    component.openGroupEdit(component.recordRows[0], 3);
    component.groupEditName = '  Giao tiếp  ';
    component.applyGroupEdit();

    expect(component.groupEditVisible).toBeFalse();
    expect(component.records.every(r => r.assessment.groupLv3Name === 'Giao tiếp')).toBeTrue();
    expect(component.recordRows.every(r => r.groupLv3Name === 'Giao tiếp')).toBeTrue();
    expect(component.dirty).toBeTrue();
  });

  it('resetGroupCell reverts a merged cell to the loaded baseline group name', () => {
    const component = createComponent('edit', 'Teacher');
    const records = [
      groupRecord('record-1', 'A01', 'Phát triển ngôn ngữ', 'Nghe và nói'),
      groupRecord('record-2', 'A02', 'Phát triển ngôn ngữ', 'Nghe và nói')
    ];
    component.isCreate = false;
    component.originalStatus = 'Open';
    component.editor.status = 'Open';
    (component as any).applyAssessmentSheet({
      id: 'sheet-1', status: 'Open', studentId: 's-1', studentSnapshot: {}, records
    } as any);

    component.openGroupEdit(component.recordRows[0], 3);
    component.groupEditName = 'Giao tiếp';
    component.applyGroupEdit();
    expect(component.canResetGroupCell(component.recordRows[0], 3)).toBeTrue();

    component.resetGroupCell(component.recordRows[0], 3);
    expect(component.records.every(r => r.assessment.groupLv3Name === 'Nghe và nói')).toBeTrue();
    expect(component.canResetGroupCell(component.recordRows[0], 3)).toBeFalse();
    expect(component.dirty).toBeFalse();
  });

  it('keeps all group edit actions locked for Done sheets', () => {
    const component = createComponent('edit', 'SuperAdmin');
    component.isCreate = false;
    component.originalStatus = 'Done';
    component.editor.status = 'Done';
    (component as any).baseline = (component as any).serialize(component.editor);

    expect(component.canShowGroupEditAction).toBeTrue();
    expect(component.canUpdateAssessmentGroups).toBeTrue();
    expect(component.groupEditActionDisabled).toBeTrue();
    expect(component.groupEditActionHint).toContain('hoàn tất');
  });

  it('does not lock the group edit action just because the form is dirty', () => {
    const component = createComponent('edit', 'Teacher');
    const records = [groupRecord('record-1', 'A01', 'Phát triển ngôn ngữ', 'Nghe và nói')];
    component.isCreate = false;
    component.originalStatus = 'Open';
    component.editor.status = 'Open';
    (component as any).applyAssessmentSheet({
      id: 'sheet-1', status: 'Open', studentId: 's-1', studentSnapshot: {}, records
    } as any);
    component.openGroupEdit(component.recordRows[0], 3);
    component.groupEditName = 'Giao tiếp';
    component.applyGroupEdit();

    expect(component.dirty).toBeTrue();
    expect(component.groupEditActionDisabled).toBeFalse();
  });

  it('moves a whole Lv3 group within its Lv2 parent and renumbers display order', () => {
    const component = createComponent('edit', 'Teacher');
    const records = [
      groupRecord('r1', 'A01', 'Phát triển thể chất', 'Vận động thô'),
      groupRecord('r2', 'A02', 'Phát triển thể chất', 'Vận động thô'),
      groupRecord('r3', 'A03', 'Phát triển thể chất', 'Vận động tinh')
    ];
    component.isCreate = false;
    component.records = records;
    component.recordRows = buildAssessmentSheetRecordRows(records);

    expect(component.canMoveGroupLv3(component.recordRows[0], 1)).toBeTrue();
    expect(component.canMoveGroupLv3(component.recordRows[0], -1)).toBeFalse();

    component.moveGroupLv3(component.recordRows[0], 1);

    expect(component.recordRows.map(row => row.record.id)).toEqual(['r3', 'r1', 'r2']);
    expect(component.records.map(record => record.id)).toEqual(['r3', 'r1', 'r2']);
    expect(component.recordRows.map(row => row.record.displayOrder)).toEqual([1, 2, 3]);
  });

  it('does not move a Lv3 group past its Lv2 group boundary', () => {
    const component = createComponent('edit', 'Teacher');
    const records = [
      groupRecord('r1', 'A01', 'Phát triển thể chất', 'Vận động'),
      groupRecord('r2', 'A02', 'Phát triển ngôn ngữ', 'Nghe')
    ];
    component.isCreate = false;
    component.records = records;
    component.recordRows = buildAssessmentSheetRecordRows(records);

    expect(component.canMoveGroupLv3(component.recordRows[0], 1)).toBeFalse();
    expect(component.canMoveGroupLv3(component.recordRows[1], -1)).toBeFalse();
  });

  it('moves a whole Lv2 group and keeps the custom order on rebuild', () => {
    const component = createComponent('edit', 'Teacher');
    const records = [
      groupRecord('r1', 'A01', 'Phát triển thể chất', 'Vận động'),
      groupRecord('r2', 'A02', 'Phát triển ngôn ngữ', 'Nghe')
    ];
    component.isCreate = false;
    component.records = records;
    (component as any).rebuildRecordRows();

    expect(component.recordRows.map(row => row.record.id)).toEqual(['r1', 'r2']);
    expect(component.canMoveGroupLv2(component.recordRows[0], 1)).toBeTrue();
    expect(component.canMoveGroupLv2(component.recordRows[0], -1)).toBeFalse();

    component.moveGroupLv2(component.recordRows[0], 1);

    expect(component.recordRows.map(row => row.record.id)).toEqual(['r2', 'r1']);
    expect(component.groupLv2Order).toEqual(['Phát triển ngôn ngữ', 'Phát triển thể chất']);
    expect(component.recordRows.map(row => row.record.displayOrder)).toEqual([1, 2]);
  });

  it('deriveLoadedGroupLv2Order returns null for config order and the appearance order when it deviates', () => {
    const canonical = [
      groupRecord('r1', 'A01', 'Phát triển thể chất', 'x'),
      groupRecord('r2', 'A02', 'Phát triển ngôn ngữ', 'y')
    ] as any;
    expect(deriveLoadedGroupLv2Order(canonical)).toBeNull();

    const moved = [
      groupRecord('r2', 'A02', 'Phát triển ngôn ngữ', 'y'),
      groupRecord('r1', 'A01', 'Phát triển thể chất', 'x')
    ] as any;
    expect(deriveLoadedGroupLv2Order(moved)).toEqual(['Phát triển ngôn ngữ', 'Phát triển thể chất']);
  });

  it('keeps a previously reordered Lv2 group order when the sheet reloads', () => {
    const component = createComponent('edit', 'Teacher');
    const records = [
      groupRecord('r2', 'A02', 'Phát triển ngôn ngữ', 'y'),
      groupRecord('r1', 'A01', 'Phát triển thể chất', 'x')
    ];
    component.isCreate = false;
    (component as any).applyAssessmentSheet({
      id: 'sheet-1', status: 'Open', studentId: 's-1', studentSnapshot: {}, records
    } as any);

    expect(component.groupLv2Order).toEqual(['Phát triển ngôn ngữ', 'Phát triển thể chất']);
    expect(component.recordRows.map(row => row.record.id)).toEqual(['r2', 'r1']);
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
    return {
      component: new AssessmentPickerComponent(assessments as any),
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

    expect(component.latestGradeOptions.map(option => option.text)).toEqual(['Chưa có', 'Đạt +', 'Hỗ trợ +', 'Hỗ trợ -', 'Chưa đạt -']);
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
