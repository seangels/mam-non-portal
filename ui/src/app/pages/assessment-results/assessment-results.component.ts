import { Component, HostListener, OnDestroy, ViewChild } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import CustomStore from 'devextreme/data/custom_store';
import { confirm } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';
import { DxDataGridComponent } from 'devextreme-angular/ui/data-grid';
import { patchGridBestFit } from '../../core/errors/dx-grid-bestfit-guard';
import { STUDENT_STATUS_LABELS } from '../../core/i18n/ui-labels';
import {
  AssessmentResultItem,
  AssessmentResultsSnapshot,
  AssessmentResultsStudent,
  UpdateAssessmentResultItemRequest
} from '../../core/models/api.models.assessment-results';
import {
  ASSESSMENT_GRADE_OPTIONS,
  AssessmentGrade,
  compareAssessmentByFixedGroupOrder
} from '../../core/models/api.models.assessment-sheets';
import { Student, SyncAssessmentFromGoogleSheetsRequest, SyncAssessmentFromGoogleSheetsResponse } from '../../core/models/api.models';
import { ApiError } from '../../core/models/api-error';
import { asLegacyWidgetDataSource } from '../../core/models/devextreme-legacy.types';
import { AssessmentResultsService } from '../../core/services/assessment-results.service';
import { GoogleSheetsService } from '../../core/services/google-sheets.service';
import { StudentsService } from '../../core/services/students.service';
import { calculateAgeText, formatDateText } from '../assessment-sheets/assessment-sheet-plan-preview.models';

interface AssessmentResultDraft extends AssessmentResultItem {
  ordinal: number;
  invalidMessage?: string;
}

interface LegacyElement {
  get?(index: number): HTMLElement | undefined;
  addClass?(className: string): void;
  removeClass?(className: string): void;
}

interface ToolbarPreparingEvent {
  toolbarOptions?: { items?: Array<Record<string, unknown>> };
}

interface SelectValueChangedEvent {
  value?: unknown;
  component?: ToolbarWidgetInstance;
}

interface WidgetInitializedEvent {
  component?: ToolbarWidgetInstance;
}

interface ToolbarWidgetInstance {
  option(name: string, value: unknown): void;
  option(options: Record<string, unknown>): void;
}

interface EditCellContext {
  value?: AssessmentGrade | string | null;
  setValue(value: AssessmentGrade | string | null): void;
}

interface ValueChangedEvent<T> {
  value?: T | null;
}

const NOTE_MAX_LENGTH = 2000;
const EMPTY_GRADE_OPTION = { value: null, text: 'Chưa có kết quả', color: '#667085', bgcolor: '#f2f4f7' };

@Component({
  selector: 'app-assessment-results',
  templateUrl: './assessment-results.component.html',
  styleUrls: ['./assessment-results.component.scss']
})
export class AssessmentResultsComponent implements OnDestroy {
  @ViewChild(DxDataGridComponent) grid?: DxDataGridComponent;

  readonly noteMaxLength = NOTE_MAX_LENGTH;
  readonly gradeOptions = [EMPTY_GRADE_OPTION, ...ASSESSMENT_GRADE_OPTIONS];
  readonly studentDataSource = asLegacyWidgetDataSource(new CustomStore({
    key: 'id',
    byKey: key => firstValueFrom(this.students.get(String(key))),
    load: options => {
      const pageSize = Math.min(options.take ?? 20, 100);
      const search = typeof options.searchValue === 'string' ? options.searchValue.trim() || undefined : undefined;
      return firstValueFrom(this.students.list({
        page: Math.floor((options.skip ?? 0) / pageSize) + 1,
        pageSize,
        search,
        sortBy: 'fullName',
        sortOrder: 'asc'
      })).then(result => ({ data: result.items, totalCount: result.pagination.totalItems }))
        .catch(error => Promise.reject(ApiError.from(error)));
    }
  }));

  selectedStudentId: string | null = null;
  student: AssessmentResultsStudent | null = null;
  drafts: AssessmentResultDraft[] = [];
  loading = false;
  saving = false;
  syncing = false;
  syncDialogVisible = false;
  loadError = '';
  saveError = '';
  traceId = '';
  conflictMessage = '';
  reloadRequired = false;
  syncRequired = false;

  private baseline = new Map<string, string>();
  private conflictIds = new Set<string>();
  private requestSequence = 0;
  private restoringSelection = false;
  private destroyed = false;
  private gridBestFitGuarded = false;
  private studentSelector?: ToolbarWidgetInstance;
  private syncToolbarButton?: ToolbarWidgetInstance;

  constructor(
    private readonly assessmentResults: AssessmentResultsService,
    private readonly students: StudentsService,
    private readonly googleSheets: GoogleSheetsService
  ) {}

  get dirtyCount(): number {
    return this.drafts.filter(item => this.isDirty(item)).length;
  }

  get invalidCount(): number {
    return this.drafts.filter(item => this.isInvalid(item)).length;
  }

  get saveDisabled(): boolean {
    return !this.selectedStudentId || this.loading || this.saving || this.syncing || this.dirtyCount === 0 ||
      this.invalidCount > 0 || this.reloadRequired || this.syncRequired;
  }

  get canEdit(): boolean {
    return !!this.selectedStudentId && !this.loading && !this.saving && !this.syncing &&
      !this.reloadRequired && !this.syncRequired;
  }

  get saveButtonText(): string {
    return this.saving ? 'Đang lưu…' : `Lưu kết quả (${this.dirtyCount})`;
  }

  get syncDisabled(): boolean {
    return this.syncing || this.loading || this.saving;
  }

  get syncButtonText(): string {
    return this.syncing ? 'Đang đồng bộ…' : 'Đồng bộ GGSheet';
  }

  get studentSummary(): string {
    if (!this.student) return 'Chọn học sinh để tải toàn bộ danh mục đánh giá và kết quả hiện tại từ dữ liệu portal.';
    const birthDate = formatDateText(this.student.dateOfBirth);
    const age = calculateAgeText(this.student.dateOfBirth, new Date());
    return `${this.student.studentCode} · ${this.student.fullName} · ${this.student.nickName || 'Chưa có tên gọi'} · Ngày sinh ${birthDate} · ${age} · ${STUDENT_STATUS_LABELS[this.student.status]}`;
  }

  readonly studentDisplay = (student: Student | null): string => student
    ? `${student.studentCode} · ${student.fullName} · ${student.nickName || 'Chưa có tên gọi'} · ${STUDENT_STATUS_LABELS[student.status]}`
    : '';

  onToolbarPreparing(event: ToolbarPreparingEvent): void {
    const items = event.toolbarOptions?.items;
    if (!items) return;
    if (!items.some(item => item['name'] === 'assessmentStudentSelector')) {
      items.unshift({
        name: 'assessmentStudentSelector',
        location: 'before',
        widget: 'dxSelectBox',
        options: {
          dataSource: this.studentDataSource,
          value: this.selectedStudentId,
          valueExpr: 'id',
          displayExpr: this.studentDisplay,
          searchEnabled: true,
          searchExpr: ['studentCode', 'fullName', 'nickName'],
          searchMode: 'contains',
          searchTimeout: 300,
          minSearchLength: 0,
          showClearButton: true,
          disabled: this.syncing,
          placeholder: 'Chọn học sinh (đang học hoặc đã nghỉ)',
          noDataText: 'Không có học sinh phù hợp',
          width: 480,
          inputAttr: { 'aria-label': 'Chọn học sinh để cập nhật kết quả' },
          onInitialized: (initialized: WidgetInitializedEvent) => this.studentSelector = initialized.component,
          onValueChanged: (valueEvent: SelectValueChangedEvent) => this.onStudentChanged(valueEvent)
        }
      });
    }
    if (!items.some(item => item['name'] === 'assessmentResultsSync')) {
      items.splice(1, 0, {
        name: 'assessmentResultsSync',
        location: 'before',
        widget: 'dxButton',
        options: {
          text: this.syncButtonText,
          icon: 'refresh',
          type: 'default',
          stylingMode: 'outlined',
          disabled: this.syncDisabled,
          hint: 'Đồng bộ danh mục và kết quả từ Google Sheets vào portal',
          onInitialized: (initialized: WidgetInitializedEvent) => this.syncToolbarButton = initialized.component,
          onClick: () => this.openSyncDialog()
        }
      });
    }
  }

  onStudentChanged(event: SelectValueChangedEvent): void {
    if (this.restoringSelection) return;
    const value = typeof event.value === 'string' && event.value ? event.value : null;
    void this.changeStudent(value, event.component);
  }

  async changeStudent(
    studentId: string | null,
    selector?: ToolbarWidgetInstance
  ): Promise<void> {
    if (studentId === this.selectedStudentId) return;
    const previous = this.selectedStudentId;
    if (this.saving || this.syncing || !(await this.confirmDiscard())) {
      this.restoreSelector(selector, previous);
      return;
    }

    this.selectedStudentId = studentId;
    this.clearSnapshot();
    if (studentId) await this.loadSnapshot(studentId);
  }

  async reloadLatest(): Promise<void> {
    if (!this.selectedStudentId || this.loading || this.saving || this.syncing) return;
    if (!(await this.confirmDiscard())) return;
    await this.loadSnapshot(this.selectedStudentId);
  }

  async save(): Promise<void> {
    if (this.saveDisabled || !this.selectedStudentId || !this.validate()) return;
    const changedRows = this.drafts.filter(item => this.isDirty(item));
    const requestItems: UpdateAssessmentResultItemRequest[] = changedRows.map(item => ({
      assessmentId: item.assessmentId,
      expectedVersion: item.version,
      grade: item.grade ?? null,
      note: this.normalizeNote(item.note)
    }));

    this.saving = true;
    this.syncToolbarState();
    this.saveError = '';
    this.traceId = '';
    this.conflictMessage = '';
    this.conflictIds.clear();
    try {
      const snapshot = await firstValueFrom(this.assessmentResults.update(this.selectedStudentId, { items: requestItems }));
      if (this.destroyed) return;
      this.applySnapshot(snapshot);
      notify(`Đã lưu ${requestItems.length} dòng kết quả vào Google Sheet và cập nhật dữ liệu portal.`, 'success', 3000);
    } catch (error) {
      this.handleSaveError(error, requestItems);
    } finally {
      this.saving = false;
      this.syncToolbarState();
    }
  }

  openSyncDialog(): void {
    if (this.syncDisabled) return;
    this.syncDialogVisible = true;
  }

  async onSyncConfirmed(request: SyncAssessmentFromGoogleSheetsRequest): Promise<void> {
    if (this.syncDisabled) return;
    this.syncDialogVisible = false;
    if (this.hasPendingChanges() && !(await this.confirmSyncWithDraft())) return;
    this.syncing = true;
    this.saveError = '';
    this.traceId = '';
    this.syncToolbarState();
    try {
      const result = await firstValueFrom(this.googleSheets.syncFromGoogleSheets(request));
      if (this.destroyed) return;
      const selectedStudentId = this.selectedStudentId;
      if (selectedStudentId) this.prepareForPostSyncReload();
      const reloaded = selectedStudentId ? await this.loadSnapshot(selectedStudentId) : true;
      if (this.destroyed) return;
      if (reloaded) {
        notify(this.syncSuccessMessage(result), 'success', 3500);
      } else {
        notify('Đã đồng bộ Google Sheets vào portal nhưng chưa tải lại được kết quả học sinh.', 'warning', 4000);
      }
    } catch (error) {
      if (this.destroyed) return;
      const apiError = ApiError.from(error);
      this.saveError = apiError.message;
      this.traceId = apiError.traceId ?? '';
      this.conflictMessage = '';
      notify(this.withTrace(apiError), 'error', 4000);
    } finally {
      this.syncing = false;
      this.syncToolbarState();
    }
  }

  canLeave(): boolean | Promise<boolean> {
    return !this.hasPendingChanges() || confirm(
      'Các kết quả đang sửa chưa được lưu. Rời trang và bỏ toàn bộ thay đổi?',
      'Bỏ thay đổi?'
    );
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasPendingChanges()) {
      event.preventDefault();
      event.returnValue = '';
    }
  }

  onContentReady(): void {
    if (!this.gridBestFitGuarded && patchGridBestFit(this.grid?.instance, '[AssessmentResults]')) {
      this.gridBestFitGuarded = true;
    }
  }

  onRowUpdated(event: { rowIndex?: number }): void {
    if (typeof event.rowIndex === 'number') this.grid?.instance.repaintRows([event.rowIndex]);
  }

  onCellPrepared(event: {
    rowType?: string;
    data?: AssessmentResultDraft;
    column?: { dataField?: string };
    cellElement?: HTMLElement | LegacyElement;
  }): void {
    if (event.rowType !== 'data' || !event.data) return;
    const dataField = event.column?.dataField;
    const element = this.htmlElement(event.cellElement);
    if (!element) return;
    const fieldDirty = dataField === 'grade'
      ? this.normalizeGrade(event.data.grade) !== this.baselineGrade(event.data.assessmentId)
      : dataField === 'note'
        ? this.normalizeNote(event.data.note) !== this.baselineNote(event.data.assessmentId)
        : false;
    element.classList.toggle('result-cell--dirty', fieldDirty);
    element.classList.toggle('result-cell--conflict', this.conflictIds.has(event.data.assessmentId));
  }

  onRowPrepared(event: {
    rowType?: string;
    data?: AssessmentResultDraft;
    rowElement?: HTMLElement | LegacyElement;
  }): void {
    if (event.rowType !== 'data' || !event.data) return;
    this.toggleClass(event.rowElement, 'result-row--dirty', this.isDirty(event.data));
    this.toggleClass(event.rowElement, 'result-row--conflict', this.conflictIds.has(event.data.assessmentId));
  }

  onGradeChanged(cell: EditCellContext, event: ValueChangedEvent<AssessmentGrade>): void {
    cell.setValue(event.value ?? null);
  }

  onNoteChanged(cell: EditCellContext, event: ValueChangedEvent<string>): void {
    cell.setValue(event.value ?? '');
  }

  gradeText(value: AssessmentGrade | null | undefined): string {
    return ASSESSMENT_GRADE_OPTIONS.find(option => option.value === value)?.text ?? 'Chưa có kết quả';
  }

  gradeColor(value: AssessmentGrade | null | undefined): string {
    return ASSESSMENT_GRADE_OPTIONS.find(option => option.value === value)?.color ?? '#667085';
  }

  gradeBackground(value: AssessmentGrade | null | undefined): string {
    return ASSESSMENT_GRADE_OPTIONS.find(option => option.value === value)?.bgcolor ?? '#f2f4f7';
  }

  noteCounter(item: AssessmentResultDraft): string {
    return `${item.note?.length ?? 0}/${NOTE_MAX_LENGTH}`;
  }

  isDirty(item: AssessmentResultDraft): boolean {
    return this.baseline.get(item.assessmentId) !== this.serialize(item);
  }

  isInvalid(item: AssessmentResultDraft): boolean {
    return (item.note?.length ?? 0) > NOTE_MAX_LENGTH || !!item.invalidMessage;
  }

  scrollToTop(): void {
    document.getElementById('assessment-results-title')
      ?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  scrollToBottom(): void {
    document.querySelector('app-footer')
      ?.scrollIntoView({ behavior: 'smooth', block: 'end' });
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    ++this.requestSequence;
  }

  private async loadSnapshot(studentId: string): Promise<boolean> {
    const request = ++this.requestSequence;
    this.loading = true;
    this.syncToolbarState();
    this.loadError = '';
    this.saveError = '';
    this.traceId = '';
    try {
      const snapshot = await firstValueFrom(this.assessmentResults.get(studentId));
      if (this.destroyed || request !== this.requestSequence || studentId !== this.selectedStudentId) return false;
      this.applySnapshot(snapshot);
      return true;
    } catch (error) {
      if (request !== this.requestSequence || studentId !== this.selectedStudentId) return false;
      const apiError = ApiError.from(error);
      this.loadError = apiError.message;
      this.traceId = apiError.traceId ?? '';
      return false;
    } finally {
      if (request === this.requestSequence) {
        this.loading = false;
        this.syncToolbarState();
      }
    }
  }

  private applySnapshot(snapshot: AssessmentResultsSnapshot): void {
    this.student = snapshot.student;
    this.selectedStudentId = snapshot.student.id;
    this.drafts = [...snapshot.items]
      .sort(compareAssessmentByFixedGroupOrder)
      .map((item, index) => ({ ...item, grade: item.grade ?? null, note: item.note ?? null, ordinal: index + 1 }));
    this.baseline = new Map(this.drafts.map(item => [item.assessmentId, this.serialize(item)]));
    this.conflictIds.clear();
    this.loadError = '';
    this.saveError = '';
    this.traceId = '';
    this.conflictMessage = '';
    this.reloadRequired = false;
    this.syncRequired = false;
    setTimeout(() => this.grid?.instance.pageIndex(0));
  }

  private clearSnapshot(): void {
    ++this.requestSequence;
    this.student = null;
    this.drafts = [];
    this.baseline.clear();
    this.conflictIds.clear();
    this.loadError = '';
    this.saveError = '';
    this.traceId = '';
    this.conflictMessage = '';
    this.reloadRequired = false;
    this.syncRequired = false;
  }

  private validate(): boolean {
    let firstInvalid = -1;
    this.drafts.forEach((item, index) => {
      item.invalidMessage = (item.note?.length ?? 0) > NOTE_MAX_LENGTH
        ? `Ghi chú không được vượt quá ${NOTE_MAX_LENGTH} ký tự.`
        : undefined;
      if (item.invalidMessage && firstInvalid < 0) firstInvalid = index;
    });
    if (firstInvalid < 0) return true;
    this.grid?.instance.pageIndex(Math.floor(firstInvalid / 100));
    this.grid?.instance.repaint();
    return false;
  }

  private handleSaveError(error: unknown, requestItems: UpdateAssessmentResultItemRequest[]): void {
    const apiError = ApiError.from(error);
    this.saveError = apiError.message;
    this.traceId = apiError.traceId ?? '';
    if (apiError.code === 'AssessmentResultsVersionConflict') {
      this.reloadRequired = true;
      this.syncRequired = false;
      this.conflictIds = new Set(apiError.conflicts.map(item => item.assessmentId));
      this.conflictMessage = `${apiError.message} ${apiError.conflicts.length} dòng đang xung đột; tải lại trước khi sửa tiếp.`;
    } else if (apiError.code === 'AssessmentResultsSourceOutOfSync') {
      this.reloadRequired = false;
      this.syncRequired = true;
      this.conflictIds = new Set(apiError.sourceConflicts.map(item => item.assessmentId));
      const count = apiError.sourceConflicts.length;
      this.conflictMessage = count > 0
        ? `${apiError.message} ${count} dòng đang lệch nguồn; đồng bộ trước khi sửa tiếp.`
        : apiError.message;
    } else if (apiError.code === 'AssessmentResultsPostWriteFailed' && apiError.googleWriteSucceeded) {
      this.reloadRequired = false;
      this.syncRequired = true;
      this.conflictMessage = apiError.message;
    }
    this.applyFieldErrors(apiError, requestItems);
    this.grid?.instance.repaint();
  }

  private applyFieldErrors(apiError: ApiError, requestItems: UpdateAssessmentResultItemRequest[]): void {
    Object.entries(apiError.fieldErrors).forEach(([field, messages]) => {
      const match = /^items\[(\d+)](?:\.|$)/i.exec(field);
      if (!match) return;
      const requestItem = requestItems[Number(match[1])];
      const draft = requestItem && this.drafts.find(item => item.assessmentId === requestItem.assessmentId);
      if (draft) draft.invalidMessage = messages[0] || 'Kết quả chưa hợp lệ.';
    });
  }

  private serialize(item: Pick<AssessmentResultDraft, 'grade' | 'note'>): string {
    return JSON.stringify({ grade: this.normalizeGrade(item.grade), note: this.normalizeNote(item.note) });
  }

  private normalizeGrade(value: AssessmentGrade | null | undefined): AssessmentGrade | null {
    return value ?? null;
  }

  private normalizeNote(value: string | null | undefined): string | null {
    return value?.trim() || null;
  }

  private baselineGrade(assessmentId: string): AssessmentGrade | null {
    const value = this.baselineValue(assessmentId) as { grade?: AssessmentGrade | null };
    return value.grade ?? null;
  }

  private baselineNote(assessmentId: string): string | null {
    const value = this.baselineValue(assessmentId) as { note?: string | null };
    return value.note ?? null;
  }

  private baselineValue(assessmentId: string): object {
    const serialized = this.baseline.get(assessmentId);
    return serialized ? JSON.parse(serialized) as object : {};
  }

  private hasPendingChanges(): boolean {
    return this.dirtyCount > 0;
  }

  private async confirmDiscard(): Promise<boolean> {
    return !this.hasPendingChanges() || await confirm(
      'Các kết quả đang sửa chưa được lưu sẽ bị mất.',
      'Bỏ thay đổi?'
    );
  }

  private async confirmSyncWithDraft(): Promise<boolean> {
    return await confirm(
      'Bạn đang có kết quả chưa lưu. Nếu đồng bộ thành công, dữ liệu mới từ portal sẽ thay thế các thay đổi này. Tiếp tục đồng bộ?',
      'Đồng bộ và tải lại dữ liệu?'
    );
  }

  private syncToolbarState(): void {
    this.studentSelector?.option({ disabled: this.syncing });
    this.syncToolbarButton?.option({ text: this.syncButtonText, disabled: this.syncDisabled });
  }

  private prepareForPostSyncReload(): void {
    this.drafts = [];
    this.baseline.clear();
    this.conflictIds.clear();
    this.saveError = '';
    this.traceId = '';
    this.syncRequired = false;
    this.reloadRequired = true;
    this.conflictMessage = 'Đã đồng bộ Google Sheets vào portal. Cần tải lại dữ liệu học sinh trước khi tiếp tục chỉnh sửa.';
  }

  private syncSuccessMessage(result: SyncAssessmentFromGoogleSheetsResponse): string {
    return `Đã đồng bộ Google Sheets vào portal: thêm ${result.insertedRows}, cập nhật ${result.updatedRows}, xóa ${result.deletedRows} dòng.`;
  }

  private withTrace(error: ApiError): string {
    return error.traceId ? `${error.message} Mã tra cứu: ${error.traceId}` : error.message;
  }

  private restoreSelector(
    selector: ToolbarWidgetInstance | undefined,
    value: string | null
  ): void {
    if (!selector) return;
    this.restoringSelection = true;
    selector.option('value', value);
    this.restoringSelection = false;
  }

  private htmlElement(value: HTMLElement | LegacyElement | undefined): HTMLElement | null {
    if (!value) return null;
    if (value instanceof HTMLElement) return value;
    return value.get?.(0) ?? null;
  }

  private toggleClass(value: HTMLElement | LegacyElement | undefined, className: string, active: boolean): void {
    const element = this.htmlElement(value);
    if (element) {
      element.classList.toggle(className, active);
    } else if (active) {
      (value as LegacyElement | undefined)?.addClass?.(className);
    } else {
      (value as LegacyElement | undefined)?.removeClass?.(className);
    }
  }
}
