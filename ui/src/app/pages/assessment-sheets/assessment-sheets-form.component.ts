import { Component, HostListener, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import CustomStore from 'devextreme/data/custom_store';
import { confirm } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';
import { DxFormComponent } from 'devextreme-angular/ui/form';
import { ApiError } from '../../core/models/api-error';
import { Assessment, Student, Teacher } from '../../core/models/api.models';
import { asLegacyWidgetDataSource } from '../../core/models/devextreme-legacy.types';
import {
  AssessmentGrade,
  AssessmentSheetDetail,
  AssessmentSheetRecord,
  AssessmentSheetStatus,
  ASSESSMENT_GRADE_OPTIONS,
  ASSESSMENT_GROUP_LV2_CONFIGS,
  ASSESSMENT_SHEET_STATUS_OPTIONS,
  CreateAssessmentSheetRequest,
  AssessmentSheetRecordRequest,
  ReplaceAssessmentSheetRecordsRequest,
  UpdateAssessmentSheetRequest
} from '../../core/models/api.models.assessment-sheets';
import { AssessmentSheetsService } from '../../core/services/assessment-sheets.service';
import { AssessmentsService } from '../../core/services/assessments.service';
import { AuthStateService } from '../../core/services/auth-state.service';
import { StudentsService } from '../../core/services/students.service';
import { TeachersService } from '../../core/services/teachers.service';
import { toDateOnly } from '../../core/utils/date-only';
import { normalizeVietnamese } from '../../core/utils/vietnamese-search';
import { AssessmentPickerComponent } from './assessment-picker.component';

const ASSESSMENT_CACHE_PAGE_SIZE = 100;
const UNGROUPED_LABEL = 'Chưa phân nhóm';
const EMPTY_GRADE_OPTION: { value: null; text: string; color: string; bgcolor: string } = {
  value: null,
  text: 'Chưa có',
  color: '#344054',
  bgcolor: '#E8EAED'
};
const GROUP_LV2_COLORS = new Map(
  ASSESSMENT_GROUP_LV2_CONFIGS.map(config => [normalizeVietnamese(config.key), config.bgcolor] as const)
);
const GROUP_LV2_DISPLAY_ORDER_INDEX = new Map(
  ASSESSMENT_GROUP_LV2_CONFIGS.map(config => [normalizeVietnamese(config.key), config.displayOrder] as const)
);

export interface AssessmentSheetEditor {
  studentId: string;
  responsibleTeacherId: string | null;
  startDate: Date | string | number | null;
  dueDate: Date | string | number | null;
  status: AssessmentSheetStatus;
  note: string;
  feedback: string;
  assessmentIds: string[];
}

export interface AssessmentSheetCreateRecordSeed {
  id: string;
  latestGrade?: string | null;
  latestNote?: string | null;
}

export interface AssessmentSheetRecordTableRow {
  record: AssessmentSheetRecord;
  groupLv2Name: string;
  groupLv3Name: string;
  groupLv3NameSubstring: string;
  groupColor: string;
  showGroupLv2: boolean;
  showGroupLv3: boolean;
  groupLv2RowSpan: number;
  groupLv3RowSpan: number;
  rowNumber: number;
}

export function buildCreateAssessmentSheetRequest(
  editor: AssessmentSheetEditor,
  selectedAssessments: AssessmentSheetCreateRecordSeed[] = []
): CreateAssessmentSheetRequest {
  const assessmentById = new Map(selectedAssessments.map(assessment => [assessment.id, assessment]));
  const assessmentIds = Array.from(new Set(editor.assessmentIds.filter(Boolean)));

  return {
    studentId: editor.studentId,
    responsibleTeacherId: editor.responsibleTeacherId || null,
    note: editor.note.trim() || null,
    startDate: toDateOnly(editor.startDate) ?? null,
    dueDate: toDateOnly(editor.dueDate) ?? null,
    records: assessmentIds.map(assessmentId => {
      const assessment = assessmentById.get(assessmentId);
      return {
        assessmentId,
        latestGrade: normalizeAssessmentGrade(assessment?.latestGrade),
        note: normalizeOptional(assessment?.latestNote)
      };
    })
  };
}

export function buildUpdateAssessmentSheetRequest(editor: AssessmentSheetEditor): UpdateAssessmentSheetRequest {
  return {
    responsibleTeacherId: editor.responsibleTeacherId || null,
    note: editor.note.trim() || null,
    startDate: toDateOnly(editor.startDate) ?? null,
    dueDate: toDateOnly(editor.dueDate) ?? null,
    feedback: editor.feedback.trim() || null
  };
}

export function canMutateAssessmentSheetRecords(status: AssessmentSheetStatus | null | undefined): boolean {
  return status === 'Open';
}

export function canEditAssessmentSheetRecordValues(status: AssessmentSheetStatus | null | undefined): boolean {
  return status === 'Open' || status === 'Planed';
}

export function buildReplaceAssessmentSheetRecordsRequest(
  currentRecords: AssessmentSheetRecord[],
  assessmentToAdd: Assessment,
  availableAssessments: Assessment[]
): ReplaceAssessmentSheetRecordsRequest {
  const assessmentByCode = new Map(
    availableAssessments
      .map(assessment => [normalizeCode(assessment.code), assessment] as const)
      .filter((entry): entry is readonly [string, Assessment] => !!entry[0])
  );
  const existingCodes = new Set(
    currentRecords
      .map(record => normalizeCode(record.assessment.code))
      .filter((code): code is string => !!code)
  );
  const addedCode = normalizeCode(assessmentToAdd.code);
  if (!addedCode) {
    throw new Error('Mục đánh giá được chọn chưa có mã hợp lệ.');
  }
  if (existingCodes.has(addedCode)) {
    throw new Error('Mục đánh giá này đã có trong bảng đánh giá.');
  }

  const records = currentRecords.map(record => {
    const code = normalizeCode(record.assessment.code);
    const assessment = code ? assessmentByCode.get(code) : null;
    if (!assessment) {
      throw new Error(`Không thể xác định assessmentId cho mục ${record.assessment.code || record.assessment.name}. Vui lòng đồng bộ/tải lại danh sách mục đánh giá trước khi thêm.`);
    }
    return {
      assessmentId: assessment.id,
      planGrade: record.planGrade ?? null,
      planNote: record.planNote ?? null,
      finalGrade: recordFinalGrade(record),
      finalNote: record.finalNote ?? null
    };
  });
  const addedPlanGrade = normalizeAssessmentGrade(assessmentToAdd.latestGrade);

  records.push({
    assessmentId: assessmentToAdd.id,
    planGrade: addedPlanGrade,
    planNote: normalizeOptional(assessmentToAdd.latestNote),
    finalGrade: addedPlanGrade,
    finalNote: null
  });

  return { records };
}

export function buildRemoveAssessmentSheetRecordRequest(
  currentRecords: AssessmentSheetRecord[],
  recordToRemove: AssessmentSheetRecord,
  availableAssessments: Assessment[]
): ReplaceAssessmentSheetRecordsRequest {
  const recordsToKeep = currentRecords.filter(record => record.id !== recordToRemove.id);
  if (recordsToKeep.length === currentRecords.length) {
    throw new Error('Không tìm thấy mục đánh giá cần xóa.');
  }
  if (recordsToKeep.length === 0) {
    throw new Error('Bảng đánh giá cần có ít nhất một mục đánh giá.');
  }

  const assessmentByCode = new Map(
    availableAssessments
      .map(assessment => [normalizeCode(assessment.code), assessment] as const)
      .filter((entry): entry is readonly [string, Assessment] => !!entry[0])
  );
  const records: AssessmentSheetRecordRequest[] = recordsToKeep.map(record => {
    const code = normalizeCode(record.assessment.code);
    const assessment = code ? assessmentByCode.get(code) : null;
    if (!assessment) {
      throw new Error(`Không thể xác định assessmentId cho mục ${record.assessment.code || record.assessment.name}. Vui lòng đồng bộ/tải lại danh sách mục đánh giá trước khi xóa.`);
    }
    return {
      assessmentId: assessment.id,
      planGrade: record.planGrade ?? null,
      planNote: record.planNote ?? null,
      finalGrade: recordFinalGrade(record),
      finalNote: record.finalNote ?? null
    };
  });

  return { records };
}

export function buildSaveAssessmentSheetRecordsRequest(
  currentRecords: AssessmentSheetRecord[],
  availableAssessments: Assessment[]
): ReplaceAssessmentSheetRecordsRequest {
  const assessmentByCode = buildAssessmentByCode(availableAssessments);
  const records: AssessmentSheetRecordRequest[] = currentRecords.map(record => buildRecordRequestFromRecord(
    record,
    assessmentByCode,
    'lưu'
  ));

  return { records };
}

export function assessmentGroupLv2Color(groupLv2Name: string | null | undefined): string {
  const key = normalizeVietnamese(groupLv2Name ?? '');
  return GROUP_LV2_COLORS.get(key) ?? '#FFFFFF';
}

export function assessmentGradeText(value: string | null | undefined): string {
  return ASSESSMENT_GRADE_OPTIONS.find(item => item.value === value)?.text ?? '';
}

export function assessmentGradeColor(value: string | null | undefined): string {
  return ASSESSMENT_GRADE_OPTIONS.find(item => item.value === value)?.color ?? '';
}

export function assessmentGradeBgColor(value: string | null | undefined): string {
  return ASSESSMENT_GRADE_OPTIONS.find(item => item.value === value)?.bgcolor ?? '';
}

export function initializeAssessmentSheetRecords(records: AssessmentSheetRecord[]): AssessmentSheetRecord[] {
  return records.map(record => ({
    ...record,
    finalGrade: recordFinalGrade(record)
  }));
}

export function buildAssessmentSheetRecordRows(records: AssessmentSheetRecord[]): AssessmentSheetRecordTableRow[] {
  const rows = records
    .map((record, originalIndex) => ({
      row: {
        record,
        groupLv2Name: normalizeGroupName(record.assessment.groupLv2Name),
        groupLv3Name: normalizeGroupName(record.assessment.groupLv3Name),
        groupLv3NameSubstring: normalizeGroupName(record.assessment.groupLv3Name),
        groupColor: assessmentGroupLv2Color(record.assessment.groupLv2Name),
        showGroupLv2: false,
        showGroupLv3: false,
        groupLv2RowSpan: 1,
        groupLv3RowSpan: 1,
        rowNumber: 1
      },
      originalIndex
    }))
    .sort((left, right) => {
      const orderDelta = groupLv2DisplayOrder(left.row.groupLv2Name) - groupLv2DisplayOrder(right.row.groupLv2Name);
      return orderDelta || left.originalIndex - right.originalIndex;
    })
    .map(item => item.row);

  let groupLv3Counter = 0;
  rows.forEach((row, index) => {
    const previous = rows[index - 1];
    const startsLv2Group = !previous || previous.groupLv2Name !== row.groupLv2Name;
    const startsLv3Group = startsLv2Group || previous.groupLv3Name !== row.groupLv3Name;

    row.showGroupLv2 = startsLv2Group;
    row.showGroupLv3 = startsLv3Group;
    groupLv3Counter = startsLv3Group ? 1 : groupLv3Counter + 1;
    row.rowNumber = groupLv3Counter;

    if (startsLv2Group) {
      row.groupLv2RowSpan = countFollowingRows(rows, index, current => current.groupLv2Name === row.groupLv2Name);
    }
    if (startsLv3Group) {
      row.groupLv3RowSpan = countFollowingRows(rows, index, current =>
        current.groupLv2Name === row.groupLv2Name && current.groupLv3Name === row.groupLv3Name
      );
    }
    if (row.groupLv3RowSpan > 3) {
      row.groupLv3NameSubstring = row.groupLv3Name;
    } else if (row.groupLv3Name.length > 40) {
      // if (row.groupLv3Name.indexOf(":") > 0) {
      //   row.groupLv3NameSubstring = row.groupLv3Name.split(":")[0];
      // } else {
      // }
      row.groupLv3NameSubstring = row.groupLv3Name.substring(0, 40) + "..."
    }


  });

  return rows;
}

@Component({
  selector: 'app-assessment-sheets-form',
  templateUrl: './assessment-sheets-form.component.html',
  styleUrls: ['./assessment-sheets-form.component.scss']
})
export class AssessmentSheetFormComponent implements OnInit {
  @ViewChild(DxFormComponent) form?: DxFormComponent;
  @ViewChild(AssessmentPickerComponent) assessmentPicker?: AssessmentPickerComponent;

  readonly statuses = ASSESSMENT_SHEET_STATUS_OPTIONS;
  readonly grades = ASSESSMENT_GRADE_OPTIONS;
  readonly gradeSelectOptions = [EMPTY_GRADE_OPTION, ...ASSESSMENT_GRADE_OPTIONS];
  readonly studentDataSource = asLegacyWidgetDataSource(new CustomStore({
    key: 'id',
    byKey: key => firstValueFrom(this.students.get(String(key))),
    load: options => {
      const pageSize = Math.min(options.take ?? 20, 100);
      return firstValueFrom(this.students.list({
        page: Math.floor((options.skip ?? 0) / pageSize) + 1,
        pageSize,
        search: typeof options.searchValue === 'string' ? options.searchValue.trim() || undefined : undefined,
        status: 'Active',
        sortBy: 'fullName',
        sortOrder: 'asc'
      })).then(result => ({ data: result.items, totalCount: result.pagination.totalItems }))
        .catch(error => this.rejectPickerLoad(error));
    }
  }));
  readonly teacherDataSource = asLegacyWidgetDataSource(new CustomStore({
    key: 'id',
    byKey: key => firstValueFrom(this.teachers.get(String(key))),
    load: options => {
      const pageSize = Math.min(options.take ?? 20, 100);
      return firstValueFrom(this.teachers.list({
        page: Math.floor((options.skip ?? 0) / pageSize) + 1,
        pageSize,
        search: typeof options.searchValue === 'string' ? options.searchValue.trim() || undefined : undefined,
        status: 'Active',
        sortBy: 'fullName',
        sortOrder: 'asc'
      })).then(result => ({ data: result.items, totalCount: result.pagination.totalItems }))
        .catch(error => this.rejectPickerLoad(error));
    }
  }));
  isCreate = true;
  assessmentSheetId = '';
  originalStatus: AssessmentSheetStatus = 'Open';
  editor: AssessmentSheetEditor = this.emptyEditor();
  studentSummary = '';
  responsibleTeacherSummary = '';
  records: AssessmentSheetRecord[] = [];
  recordRows: AssessmentSheetRecordTableRow[] = [];
  existingAssessmentCodes: string[] = [];
  showAddAssessmentPicker = false;
  showCodeColumn = false;
  showPlanColumn = false;
  loading = false;
  saving = false;
  addingRecord = false;
  removingRecordId: string | null = null;
  submittingResults = false;
  loadError = '';
  formError = '';
  conflict = false;
  private assessmentCache: Assessment[] = [];
  private assessmentCacheStudentId: string | null = null;
  private baseline = this.serialize(this.editor);
  private allowPreviewNavigationOnce = false;

  readonly studentDisplay = (student: Student | null): string => student
    ? `${student.studentCode} · ${student.fullName}${student.nickName ? ` (${student.nickName})` : ''}`
    : '';
  readonly teacherDisplay = (teacher: Teacher | null): string => teacher
    ? `${teacher.teacherCode} · ${teacher.fullName}`
    : '';
  readonly formColCountByScreen = { xs: 1, sm: 2, md: 4, lg: 6 };
  readonly studentEditorOptions: Record<string, unknown> = {
    dataSource: this.studentDataSource,
    valueExpr: 'id',
    displayExpr: this.studentDisplay,
    searchEnabled: true,
    searchMode: 'contains',
    showClearButton: false,
    placeholder: 'Chọn học sinh',
    noDataText: 'Không có học sinh phù hợp',
    inputAttr: { 'aria-label': 'Học sinh' }
  };
  readonly teacherEditorOptions: Record<string, unknown> = {
    dataSource: this.teacherDataSource,
    valueExpr: 'id',
    displayExpr: this.teacherDisplay,
    searchEnabled: true,
    searchMode: 'contains',
    showClearButton: true,
    placeholder: 'Không chọn',
    noDataText: 'Không có giáo viên phù hợp',
    inputAttr: { 'aria-label': 'Giáo viên phụ trách' }
  };
  readonly statusEditorOptions: Record<string, unknown> = {
    items: this.statuses,
    valueExpr: 'value',
    displayExpr: 'text',
    searchEnabled: false,
    readOnly: true,
    inputAttr: { 'aria-label': 'Trạng thái' }
  };
  readonly dateEditorOptions: Record<string, unknown> = {
    type: 'date',
    displayFormat: 'dd/MM/yyyy',
    showClearButton: true,
    pickerType: "calendar",
  };
  readonly noteEditorOptions: Record<string, unknown> = {
    maxLength: 2000,
    autoResizeEnabled: true,
    valueChangeEvent: 'input'
  };
  readonly standaloneNgModelOptions = { standalone: true };
  get showPlan(): boolean {
    return this.showPlanColumn || this.originalStatus === 'Open'
  }
  get title(): string {
    return this.isCreate ? 'Tạo' : 'Chỉnh sửa';
  }

  get subtitle(): string {
    return this.isCreate
      ? 'Chọn học sinh và các mục đánh giá để tạo kế hoạch cá nhân.'
      : 'Cập nhật thông tin chung, ghi chú và trạng thái của bảng đánh giá.';
  }
  get showResult(): boolean {
    return this.originalStatus === 'Planed' || this.originalStatus === 'Done'
  }
  get dirty(): boolean {
    return !this.loading && this.serialize(this.editor) !== this.baseline;
  }

  get hasRecords(): boolean {
    return this.records.length > 0;
  }

  get selectedAssessmentCount(): number {
    return this.isCreate
      ? new Set(this.editor.assessmentIds.filter(Boolean)).size
      : this.records.length;
  }

  get canUseManagerLookups(): boolean {
    const role = this.auth.user?.role;
    return role === 'SuperAdmin' || role === 'Admin';
  }

  get recordMutationInProgress(): boolean {
    return this.addingRecord || !!this.removingRecordId;
  }

  get canSubmitResults(): boolean {
    return !this.isCreate
      && !!this.assessmentSheetId
      && !this.loading
      && !this.saving
      && !this.recordMutationInProgress
      && !this.submittingResults
      && this.hasRecords;
  }

  get canMutateRecords(): boolean {
    return !this.loading
      && !this.saving
      && !this.recordMutationInProgress
      && !this.submittingResults
      && canMutateAssessmentSheetRecords(this.editor.status);
  }

  get recordValueControlsDisabled(): boolean {
    return this.saving
      || this.recordMutationInProgress
      || this.submittingResults
      || !canEditAssessmentSheetRecordValues(this.editor.status);
  }

  get recordMutationLockHint(): string {
    return this.recordStructureLockedMessage();
  }

  constructor(
    private readonly assessmentSheets: AssessmentSheetsService,
    private readonly assessments: AssessmentsService,
    private readonly auth: AuthStateService,
    private readonly students: StudentsService,
    private readonly teachers: TeachersService,
    private readonly route: ActivatedRoute,
    private readonly router: Router
  ) { }

  ngOnInit(): void {
    this.isCreate = this.route.snapshot.data['mode'] === 'create';
    this.statusEditorOptions['readOnly'] = this.isCreate;
    if (this.isCreate) {
      this.editor = this.emptyEditor();
      this.baseline = this.serialize(this.editor);
      return;
    }

    this.assessmentSheetId = this.route.snapshot.paramMap.get('id') ?? '';
    if (this.assessmentSheetId) {
      void this.load(this.assessmentSheetId);
    } else {
      this.loadError = 'Không tìm thấy mã bảng đánh giá trong đường dẫn.';
    }
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.dirty && !this.saving && !this.recordMutationInProgress && !this.submittingResults) {
      event.preventDefault();
      event.returnValue = '';
    }
  }

  async canLeave(): Promise<boolean> {
    if (this.allowPreviewNavigationOnce) {
      return true;
    }
    if (!this.dirty || this.saving || this.recordMutationInProgress || this.submittingResults) {
      return true;
    }
    return confirm('Bạn có thay đổi chưa lưu. Rời trang và bỏ các thay đổi này?', 'Xác nhận rời trang');
  }

  async save(event?: Event): Promise<void> {
    event?.preventDefault();
    if (this.saving || this.recordMutationInProgress || this.submittingResults || this.loading) {
      return;
    }

    const validation = this.form?.instance.validate();
    if (validation && !validation.isValid) {
      this.formError = 'Vui lòng kiểm tra các trường được đánh dấu.';
      const firstRule = validation.brokenRules?.[0] as unknown as { validator?: { focus?: () => void } } | undefined;
      firstRule?.validator?.focus?.();
      return;
    }
    const createRequest = this.isCreate
      ? buildCreateAssessmentSheetRequest(this.editor, this.assessmentPicker?.getSelectedAssessments() ?? [])
      : null;
    if (this.isCreate && createRequest?.records.length === 0) {
      this.formError = 'Vui lòng chọn ít nhất một mục đánh giá.';
      this.assessmentPicker?.focus();
      return;
    }

    this.saving = true;
    this.formError = '';
    this.conflict = false;
    try {
      const saved = this.isCreate
        ? await firstValueFrom(this.assessmentSheets.create(createRequest!))
        : await this.saveExisting();

      this.applyAssessmentSheet(saved);
      notify(this.isCreate ? 'Đã tạo bảng đánh giá.' : 'Đã cập nhật bảng đánh giá.', 'success', 2000);
      if (this.isCreate) {
        await this.router.navigate(['/assessment-sheets', saved.id, 'edit']);
      }
    } catch (error) {
      const apiError = ApiError.from(error);
      this.formError = this.withTrace(apiError);
      this.conflict = apiError.code === 'AssessmentSheetDone' || apiError.code === 'AssessmentSheetVersionConflict';
      this.focusFirstServerField(apiError);
    } finally {
      this.saving = false;
    }
  }

  async reloadLatest(): Promise<void> {
    if (!this.assessmentSheetId) {
      return;
    }
    if (this.dirty) {
      const accepted = await confirm('Tải dữ liệu mới nhất và bỏ toàn bộ thay đổi đang nhập?', 'Xác nhận tải lại');
      if (!accepted) {
        return;
      }
    }
    await this.load(this.assessmentSheetId);
  }

  cancel(): void {
    void this.router.navigate(['/assessment-sheets']);
  }

  async openPlanPdfPreview(): Promise<void> {
    await this.openPdfPreview('plan');
  }

  async openResultPdfPreview(): Promise<void> {
    await this.openPdfPreview('result');
  }

  async submitResults(): Promise<void> {
    if (!this.canSubmitResults) {
      return;
    }
    if (this.dirty) {
      const accepted = await confirm(
        'Bạn có thay đổi chưa lưu. Hệ thống sẽ cập nhật kết quả bằng dữ liệu đã lưu hiện tại. Nếu muốn cập nhật dữ liệu mới nhất, hãy lưu thay đổi trước.',
        'Cập nhật Kết Quả'
      );
      if (!accepted) {
        return;
      }
    }

    this.submittingResults = true;
    this.formError = '';
    this.conflict = false;
    try {
      const saved = await firstValueFrom(this.assessmentSheets.submitResults(this.assessmentSheetId));
      this.applyAssessmentSheet(saved);
      notify('Đã cập nhật kết quả vào Google Sheet.', 'success', 2500);
    } catch (error) {
      const apiError = ApiError.from(error);
      this.formError = this.withTrace(apiError);
      this.conflict = apiError.code === 'AssessmentSheetVersionConflict';
    } finally {
      this.submittingResults = false;
    }
  }

  private async openPdfPreview(kind: 'plan' | 'result'): Promise<void> {
    const canOpen = kind === 'result'
      ? this.canOpenResultPdfPreview()
      : this.canOpenPlanPdfPreview();
    if (this.isCreate || !this.assessmentSheetId || !canOpen) {
      return;
    }
    if (this.dirty) {
      const label = kind === 'result' ? 'kết quả' : 'kế hoạch';
      const accepted = await confirm(
        `Bạn có thay đổi chưa lưu. Trang preview sẽ dùng dữ liệu đã lưu trong hệ thống. Nếu muốn PDF ${label} phản ánh bản mới nhất, hãy lưu thay đổi trước.`,
        kind === 'result' ? 'Mở preview kết quả PDF' : 'Mở preview kế hoạch PDF'
      );
      if (!accepted) {
        return;
      }
      this.allowPreviewNavigationOnce = true;
    }
    try {
      await this.router.navigate([
        '/assessment-sheets',
        this.assessmentSheetId,
        kind === 'result' ? 'result-pdf-preview' : 'plan-pdf-preview'
      ]);
    } finally {
      this.allowPreviewNavigationOnce = false;
    }
  }

  canOpenPlanPdfPreview(): boolean {
    return this.canUseSavedRecordAction() && this.originalStatus !== 'Open';
  }

  canOpenResultPdfPreview(): boolean {
    return this.canUseSavedRecordAction() && this.originalStatus !== 'Open';
  }

  private canUseSavedRecordAction(): boolean {
    return !this.loading
      && !this.saving
      && !this.recordMutationInProgress
      && !this.submittingResults
      && this.hasRecords;
  }

  toggleAddAssessmentPicker(): void {
    if (!canMutateAssessmentSheetRecords(this.editor.status)) {
      this.formError = this.recordStructureLockedMessage();
      return;
    }
    this.showAddAssessmentPicker = !this.showAddAssessmentPicker;
  }

  async addAssessmentToSheet(assessment: Assessment): Promise<void> {
    if (!this.canMutateRecords) {
      return;
    }
    const accepted = await confirm(
      `Thêm mục đánh giá "${assessment.code} · ${assessment.name}" vào bảng đánh giá này?`,
      'Xác nhận thêm'
    );
    if (!accepted) {
      return;
    }

    let request: ReplaceAssessmentSheetRecordsRequest;
    try {
      request = buildReplaceAssessmentSheetRecordsRequest(
        this.records,
        assessment,
        this.assessmentPicker?.getCachedAssessments() ?? []
      );
    } catch (error) {
      this.formError = error instanceof Error ? error.message : 'Không thể thêm mục đánh giá. Vui lòng thử lại.';
      return;
    }

    this.addingRecord = true;
    this.formError = '';
    this.conflict = false;
    try {
      const saved = await firstValueFrom(this.assessmentSheets.replaceRecords(this.assessmentSheetId, request));
      this.applyAssessmentSheet(saved);
      this.showAddAssessmentPicker = true;
      notify('Đã thêm mục đánh giá.', 'success', 2000);
    } catch (error) {
      const apiError = ApiError.from(error);
      this.formError = this.withTrace(apiError);
      this.conflict = apiError.code === 'AssessmentSheetDone' || apiError.code === 'AssessmentSheetVersionConflict';
    } finally {
      this.addingRecord = false;
    }
  }

  removeRecordHint(record: AssessmentSheetRecord): string {
    const lockHint = this.recordStructureLockedMessage();
    if (lockHint) {
      return lockHint;
    }
    if (this.records.length <= 1) {
      return 'Bảng đánh giá cần giữ ít nhất một mục đánh giá';
    }
    return `Xóa mục ${record.assessment.code} · ${record.assessment.name}`;
  }

  async removeAssessmentRecord(record: AssessmentSheetRecord): Promise<void> {
    if (!this.canMutateRecords) {
      return;
    }
    if (this.records.length <= 1) {
      this.formError = 'Bảng đánh giá cần có ít nhất một mục đánh giá.';
      return;
    }

    const accepted = await confirm(
      `Xóa mục đánh giá "${record.assessment.code} · ${record.assessment.name}" khỏi bảng đánh giá này?`,
      'Xác nhận xóa'
    );
    if (!accepted) {
      return;
    }

    let request: ReplaceAssessmentSheetRecordsRequest;
    try {
      const availableAssessments = await this.loadAssessmentCache();
      request = buildRemoveAssessmentSheetRecordRequest(this.records, record, availableAssessments);
    } catch (error) {
      this.formError = error instanceof Error
        ? error.message
        : this.withTrace(ApiError.from(error));
      return;
    }

    this.removingRecordId = record.id;
    this.formError = '';
    this.conflict = false;
    try {
      const saved = await firstValueFrom(this.assessmentSheets.replaceRecords(this.assessmentSheetId, request));
      this.applyAssessmentSheet(saved);
      notify('Đã xóa mục đánh giá.', 'success', 2000);
    } catch (error) {
      const apiError = ApiError.from(error);
      this.formError = this.withTrace(apiError);
      this.conflict = apiError.code === 'AssessmentSheetDone' || apiError.code === 'AssessmentSheetVersionConflict';
    } finally {
      this.removingRecordId = null;
    }
  }

  updateRecordFinalGrade(record: AssessmentSheetRecord, value: AssessmentGrade | null): void {
    record.finalGrade = value ?? null;
  }

  updateRecordFinalNote(record: AssessmentSheetRecord, value: string | null): void {
    record.finalNote = value ?? '';
  }

  private recordStructureLockedMessage(): string {
    if (this.editor.status === 'Planed') {
      return 'Bảng đánh giá đang ở trạng thái Kế hoạch nên không thể thêm hoặc xóa mục đánh giá.';
    }
    if (this.editor.status === 'Done') {
      return 'Bảng đánh giá đã hoàn tất. Vui lòng chuyển trạng thái khỏi Hoàn tất trước khi thêm hoặc xóa mục đánh giá.';
    }
    return '';
  }

  private async loadAssessmentCache(): Promise<Assessment[]> {
    const pickerCache = this.assessmentPicker?.getCachedAssessments() ?? [];
    if (pickerCache.length > 0) {
      this.assessmentCache = pickerCache;
      this.assessmentCacheStudentId = normalizeOptional(this.editor.studentId);
      return pickerCache;
    }

    const requestedStudentId = normalizeOptional(this.editor.studentId);
    if (this.assessmentCache.length > 0 && this.assessmentCacheStudentId === requestedStudentId) {
      return this.assessmentCache;
    }

    const loaded: Assessment[] = [];
    let page = 1;
    let totalPages = 1;
    do {
      const query = {
        page,
        pageSize: ASSESSMENT_CACHE_PAGE_SIZE,
        sortBy: 'rowindex',
        sortOrder: 'asc'
      } as const;
      const result = await firstValueFrom(this.assessments.list(requestedStudentId
        ? { ...query, studentId: requestedStudentId }
        : query));
      loaded.push(...result.items);
      totalPages = Math.max(
        1,
        result.pagination.totalPages || Math.ceil(result.pagination.totalItems / ASSESSMENT_CACHE_PAGE_SIZE)
      );
      page += 1;
    } while (page <= totalPages);

    this.assessmentCache = loaded;
    this.assessmentCacheStudentId = requestedStudentId;
    return loaded;
  }

  private async saveExisting(): Promise<AssessmentSheetDetail> {
    let saved: AssessmentSheetDetail;
    const shouldReplaceRecords = this.recordsDirty();
    if (this.originalStatus === 'Done' && this.editor.status !== 'Done') {
      saved = await firstValueFrom(this.assessmentSheets.updateStatus(this.assessmentSheetId, { status: this.editor.status }));
      this.originalStatus = saved.status;
    }

    saved = await firstValueFrom(this.assessmentSheets.update(
      this.assessmentSheetId,
      buildUpdateAssessmentSheetRequest(this.editor)
    ));

    if (shouldReplaceRecords) {
      const availableAssessments = await this.loadAssessmentCache();
      saved = await firstValueFrom(this.assessmentSheets.replaceRecords(
        this.assessmentSheetId,
        buildSaveAssessmentSheetRecordsRequest(this.records, availableAssessments)
      ));
    }

    if (saved.status !== this.editor.status) {
      saved = await firstValueFrom(this.assessmentSheets.updateStatus(this.assessmentSheetId, { status: this.editor.status }));
    }
    return saved;
  }

  private async load(id: string): Promise<void> {
    this.loading = true;
    this.loadError = '';
    this.formError = '';
    this.conflict = false;
    try {
      this.applyAssessmentSheet(await firstValueFrom(this.assessmentSheets.get(id)));
    } catch (error) {
      this.loadError = this.withTrace(ApiError.from(error));
    } finally {
      this.loading = false;
    }
  }

  private applyAssessmentSheet(sheet: AssessmentSheetDetail): void {
    this.assessmentSheetId = sheet.id;
    this.originalStatus = sheet.status;
    this.editor = {
      studentId: sheet.studentId,
      responsibleTeacherId: sheet.responsibleTeacherId ?? null,
      startDate: sheet.startDate ?? null,
      dueDate: sheet.dueDate ?? null,
      status: sheet.status,
      note: sheet.note ?? '',
      feedback: sheet.feedback ?? '',
      assessmentIds: []
    };
    this.studentSummary = this.buildStudentSummary(sheet);
    this.responsibleTeacherSummary = sheet.responsibleTeacherFullName ?? 'Chưa chọn giáo viên phụ trách';
    this.records = initializeAssessmentSheetRecords(sheet.records ?? []);
    this.recordRows = buildAssessmentSheetRecordRows(this.records);
    this.existingAssessmentCodes = this.records
      .map(record => record.assessment.code)
      .filter(Boolean);
    this.baseline = this.serialize(this.editor);
  }

  private focusFirstServerField(error: ApiError): void {
    const key = Object.keys(error.fieldErrors)[0];
    if (!key) {
      return;
    }
    const field = key.charAt(0).toLowerCase() + key.slice(1);
    if (field === 'assessmentIds' || field === 'records') {
      this.assessmentPicker?.focus();
      return;
    }
    this.form?.instance.getEditor(field)?.focus();
  }

  private emptyEditor(): AssessmentSheetEditor {
    return {
      studentId: '',
      responsibleTeacherId: null,
      startDate: null,
      dueDate: null,
      status: 'Open',
      note: '',
      feedback: '',
      assessmentIds: []
    };
  }

  private serialize(editor: AssessmentSheetEditor): string {
    return JSON.stringify({
      studentId: editor.studentId,
      responsibleTeacherId: editor.responsibleTeacherId,
      startDate: toDateOnly(editor.startDate) ?? null,
      dueDate: toDateOnly(editor.dueDate) ?? null,
      status: editor.status,
      note: editor.note,
      feedback: editor.feedback,
      assessmentIds: this.isCreate ? Array.from(new Set(editor.assessmentIds.filter(Boolean))).sort() : [],
      records: this.isCreate ? [] : this.serializeRecords(this.records)
    });
  }

  private recordsDirty(): boolean {
    const parsedBaseline = JSON.parse(this.baseline) as { records?: unknown };
    return JSON.stringify(parsedBaseline.records ?? []) !== JSON.stringify(this.serializeRecords(this.records));
  }

  private serializeRecords(records: AssessmentSheetRecord[]): unknown[] {
    return records.map(record => ({
      id: record.id,
      code: record.assessment.code,
      planGrade: record.planGrade ?? null,
      planNote: record.planNote ?? null,
      finalGrade: recordFinalGrade(record),
      finalNote: record.finalNote ?? ''
    }));
  }

  gradeText(value: string | null | undefined): string {
    return assessmentGradeText(value);
  }

  gradeColor(value: string | null | undefined): string {
    return assessmentGradeColor(value);
  }

  gradeBgColor(value: string | null | undefined): string {
    return assessmentGradeBgColor(value);
  }

  recordGroupText(record: AssessmentSheetRecord): string {
    return [record.assessment.groupLv1Name, record.assessment.groupLv2Name, record.assessment.groupLv3Name]
      .filter(Boolean)
      .join(' / ') || 'Chưa phân nhóm';
  }

  private buildStudentSummary(sheet: AssessmentSheetDetail): string {
    const snapshot = sheet.studentSnapshot;
    const code = snapshot?.studentCode || sheet.studentCode || 'Chưa có mã';
    const name = snapshot?.fullName || sheet.studentFullName || 'Chưa có tên';
    const nickName = snapshot?.nickName ? ` (${snapshot.nickName})` : '';
    return `${code} · ${name}${nickName}`;
  }

  private rejectPickerLoad(error: unknown): Promise<never> {
    const apiError = ApiError.from(error);
    return Promise.reject(apiError);
  }

  private withTrace(error: ApiError): string {
    return error.traceId ? `${error.message} Mã tra cứu: ${error.traceId}` : error.message;
  }
}

function normalizeOptional(value: string | null | undefined): string | null {
  const normalized = value?.trim();
  return normalized || null;
}

function normalizeAssessmentGrade(value: string | null | undefined): AssessmentGrade | null {
  return value === 'A' || value === 'B' || value === 'C' || value === 'D' ? value : null;
}

function normalizeCode(value: string | null | undefined): string | null {
  const code = value?.trim().toLocaleLowerCase('vi');
  return code || null;
}

function buildAssessmentByCode(availableAssessments: Assessment[]): Map<string, Assessment> {
  return new Map(
    availableAssessments
      .map(assessment => [normalizeCode(assessment.code), assessment] as const)
      .filter((entry): entry is readonly [string, Assessment] => !!entry[0])
  );
}

function buildRecordRequestFromRecord(
  record: AssessmentSheetRecord,
  assessmentByCode: Map<string, Assessment>,
  action: 'thêm' | 'xóa' | 'lưu'
): AssessmentSheetRecordRequest {
  const code = normalizeCode(record.assessment.code);
  const assessment = code ? assessmentByCode.get(code) : null;
  if (!assessment) {
    throw new Error(`Không thể xác định assessmentId cho mục ${record.assessment.code || record.assessment.name}. Vui lòng đồng bộ/tải lại danh sách mục đánh giá trước khi ${action}.`);
  }
  return {
    assessmentId: assessment.id,
    planGrade: record.planGrade ?? null,
    planNote: record.planNote ?? null,
    finalGrade: recordFinalGrade(record),
    finalNote: record.finalNote ?? null
  };
}

function recordFinalGrade(record: AssessmentSheetRecord): AssessmentGrade | null {
  return record.finalGrade ?? record.planGrade ?? null;
}

function normalizeGroupName(value: string | null | undefined): string {
  return value?.trim() || UNGROUPED_LABEL;
}

function groupLv2DisplayOrder(groupLv2Name: string): number {
  return GROUP_LV2_DISPLAY_ORDER_INDEX.get(normalizeVietnamese(groupLv2Name)) ?? Number.MAX_SAFE_INTEGER;
}

function countFollowingRows(
  rows: AssessmentSheetRecordTableRow[],
  startIndex: number,
  predicate: (row: AssessmentSheetRecordTableRow) => boolean
): number {
  let count = 0;
  for (let index = startIndex; index < rows.length; index += 1) {
    if (!predicate(rows[index])) {
      break;
    }
    count += 1;
  }
  return count;
}
