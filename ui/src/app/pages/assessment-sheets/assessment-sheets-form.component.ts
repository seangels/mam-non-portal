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
  AssessmentSheetDetail,
  AssessmentSheetRecord,
  AssessmentSheetStatus,
  ASSESSMENT_GRADE_OPTIONS,
  ASSESSMENT_SHEET_STATUS_OPTIONS,
  CreateAssessmentSheetRequest,
  UpdateAssessmentSheetRequest
} from '../../core/models/api.models.assessment-sheets';
import { AssessmentSheetsService } from '../../core/services/assessment-sheets.service';
import { AssessmentsService } from '../../core/services/assessments.service';
import { AuthStateService } from '../../core/services/auth-state.service';
import { StudentsService } from '../../core/services/students.service';
import { TeachersService } from '../../core/services/teachers.service';
import { toDateOnly } from '../../core/utils/date-only';

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

export function buildCreateAssessmentSheetRequest(editor: AssessmentSheetEditor): CreateAssessmentSheetRequest {
  return {
    studentId: editor.studentId,
    responsibleTeacherId: editor.responsibleTeacherId || null,
    note: editor.note.trim() || null,
    startDate: toDateOnly(editor.startDate) ?? null,
    dueDate: toDateOnly(editor.dueDate) ?? null,
    assessmentIds: Array.from(new Set(editor.assessmentIds.filter(Boolean)))
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

@Component({
  selector: 'app-assessment-sheets-form',
  templateUrl: './assessment-sheets-form.component.html',
  styleUrls: ['./assessment-sheets-form.component.scss']
})
export class AssessmentSheetFormComponent implements OnInit {
  @ViewChild(DxFormComponent) form?: DxFormComponent;

  readonly statuses = ASSESSMENT_SHEET_STATUS_OPTIONS;
  readonly grades = ASSESSMENT_GRADE_OPTIONS;
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
  readonly assessmentDataSource = asLegacyWidgetDataSource(new CustomStore({
    key: 'id',
    byKey: key => firstValueFrom(this.assessments.get(String(key))),
    load: options => {
      const pageSize = Math.min(options.take ?? 100, 100);
      return firstValueFrom(this.assessments.list({
        page: Math.floor((options.skip ?? 0) / pageSize) + 1,
        pageSize,
        search: typeof options.searchValue === 'string' ? options.searchValue.trim() || undefined : undefined,
        sortBy: 'rowindex',
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
  loading = false;
  saving = false;
  loadError = '';
  formError = '';
  conflict = false;
  private baseline = this.serialize(this.editor);

  readonly studentDisplay = (student: Student | null): string => student
    ? `${student.studentCode} · ${student.fullName}${student.nickName ? ` (${student.nickName})` : ''}`
    : '';
  readonly teacherDisplay = (teacher: Teacher | null): string => teacher
    ? `${teacher.teacherCode} · ${teacher.fullName}`
    : '';
  readonly assessmentDisplay = (assessment: Assessment | null): string => assessment
    ? `${assessment.code} · ${assessment.name}`
    : '';

  readonly formColCountByScreen = { xs: 1, sm: 1, md: 2, lg: 2 };
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
    inputAttr: { 'aria-label': 'Trạng thái' }
  };
  readonly assessmentEditorOptions: Record<string, unknown> = {
    dataSource: this.assessmentDataSource,
    valueExpr: 'id',
    displayExpr: this.assessmentDisplay,
    searchEnabled: true,
    searchMode: 'contains',
    showSelectionControls: true,
    applyValueMode: 'useButtons',
    placeholder: 'Chọn mục đánh giá',
    noDataText: 'Không có mục đánh giá phù hợp',
    selectAllText: 'Chọn tất cả',
    maxDisplayedTags: 4,
    showMultiTagOnly: false,
    inputAttr: { 'aria-label': 'Mục đánh giá' }
  };
  readonly dateEditorOptions: Record<string, unknown> = {
    type: 'date',
    displayFormat: 'dd/MM/yyyy',
    showClearButton: true
  };
  readonly noteEditorOptions: Record<string, unknown> = {
    maxLength: 2000,
    minHeight: 110,
    autoResizeEnabled: true,
    valueChangeEvent: 'input'
  };

  get title(): string {
    return this.isCreate ? 'Thêm bảng đánh giá' : 'Chỉnh sửa bảng đánh giá';
  }

  get subtitle(): string {
    return this.isCreate
      ? 'Chọn học sinh và các mục đánh giá để tạo kế hoạch cá nhân.'
      : 'Cập nhật thông tin chung, ghi chú và trạng thái của bảng đánh giá.';
  }

  get dirty(): boolean {
    return !this.loading && this.serialize(this.editor) !== this.baseline;
  }

  get hasRecords(): boolean {
    return this.records.length > 0;
  }

  get canUseManagerLookups(): boolean {
    const role = this.auth.user?.role;
    return role === 'SuperAdmin' || role === 'Admin';
  }

  constructor(
    private readonly assessmentSheets: AssessmentSheetsService,
    private readonly assessments: AssessmentsService,
    private readonly auth: AuthStateService,
    private readonly students: StudentsService,
    private readonly teachers: TeachersService,
    private readonly route: ActivatedRoute,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.isCreate = this.route.snapshot.data['mode'] === 'create';
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
    if (this.dirty && !this.saving) {
      event.preventDefault();
      event.returnValue = '';
    }
  }

  async canLeave(): Promise<boolean> {
    if (!this.dirty || this.saving) {
      return true;
    }
    return confirm('Bạn có thay đổi chưa lưu. Rời trang và bỏ các thay đổi này?', 'Xác nhận rời trang');
  }

  async save(event: Event): Promise<void> {
    event.preventDefault();
    if (this.saving || this.loading) {
      return;
    }

    const validation = this.form?.instance.validate();
    if (validation && !validation.isValid) {
      this.formError = 'Vui lòng kiểm tra các trường được đánh dấu.';
      const firstRule = validation.brokenRules?.[0] as unknown as { validator?: { focus?: () => void } } | undefined;
      firstRule?.validator?.focus?.();
      return;
    }
    if (this.isCreate && buildCreateAssessmentSheetRequest(this.editor).assessmentIds.length === 0) {
      this.formError = 'Vui lòng chọn ít nhất một mục đánh giá.';
      this.form?.instance.getEditor('assessmentIds')?.focus();
      return;
    }

    this.saving = true;
    this.formError = '';
    this.conflict = false;
    try {
      const saved = this.isCreate
        ? await firstValueFrom(this.assessmentSheets.create(buildCreateAssessmentSheetRequest(this.editor)))
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

  private async saveExisting(): Promise<AssessmentSheetDetail> {
    let saved: AssessmentSheetDetail;
    if (this.originalStatus === 'Done' && this.editor.status !== 'Done') {
      saved = await firstValueFrom(this.assessmentSheets.updateStatus(this.assessmentSheetId, { status: this.editor.status }));
      this.originalStatus = saved.status;
    }

    saved = await firstValueFrom(this.assessmentSheets.update(
      this.assessmentSheetId,
      buildUpdateAssessmentSheetRequest(this.editor)
    ));

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
    this.records = sheet.records ?? [];
    this.baseline = this.serialize(this.editor);
  }

  private focusFirstServerField(error: ApiError): void {
    const key = Object.keys(error.fieldErrors)[0];
    if (!key) {
      return;
    }
    const field = key.charAt(0).toLowerCase() + key.slice(1);
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
      assessmentIds: this.isCreate ? Array.from(new Set(editor.assessmentIds.filter(Boolean))).sort() : []
    });
  }

  gradeText(value: string | null | undefined): string {
    return this.grades.find(item => item.value === value)?.text ?? '—';
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
