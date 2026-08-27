import { Component, OnDestroy, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import CustomStore from 'devextreme/data/custom_store';
import { custom } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';
import { DxDataGridComponent } from 'devextreme-angular/ui/data-grid';
import { ApiError } from '../../core/models/api-error';
import {
  AssessmentSheet,
  AssessmentSheetImportExcelResult,
  AssessmentSheetImportExcelPreviewResult,
  AssessmentSheetImportExcelPreviewRow,
  AssessmentSheetStatus,
  ASSESSMENT_SHEET_STATUS_OPTIONS,
  AssessmentSheetImportExcelPreviewSummaryResult
} from '../../core/models/api.models.assessment-sheets';
import { Student } from '../../core/models/api.models';
import { asLegacyWidgetDataSource } from '../../core/models/devextreme-legacy.types';
import { AssessmentSheetsService } from '../../core/services/assessment-sheets.service';
import { GoogleSheetsService } from '../../core/services/google-sheets.service';
import { StudentsService } from '../../core/services/students.service';
import { formatDateOnly, toDateOnly } from '../../core/utils/date-only';

const ASSESSMENT_SHEET_SORT_FIELDS = new Set([
  'status',
  'startDate',
  'dueDate',
  'updatedAt',
  'createdAt'
]);

@Component({
  selector: 'app-assessment-sheets',
  templateUrl: './assessment-sheets.component.html',
  styleUrls: ['./assessment-sheets.component.scss']
})
export class AssessmentSheetsComponent implements OnDestroy {
  @ViewChild(DxDataGridComponent) grid?: DxDataGridComponent;

  search = '';
  syncing = false;
  status: AssessmentSheetStatus | null = null;
  studentId: string | null = null;
  dateFrom: Date | string | number = '';
  dateTo: Date | string | number = '';
  filtersExpanded = true;
  importing = false;
  importPreviewLoading = false;
  importPreviewVisible = false;
  importPreview: AssessmentSheetImportExcelPreviewSummaryResult | null = null;
  importPreviewRows: AssessmentSheetImportExcelPreviewRow[] = [];
  importResult: AssessmentSheetImportExcelResult | null = null;
  loadError = '';
  private searchTimer?: number;
  private selectedImportFile: File | null = null;

  readonly statuses = ASSESSMENT_SHEET_STATUS_OPTIONS;
  readonly rowButtons = [
    {
      hint: 'Chỉnh sửa',
      icon: 'edit',
      onClick: (event: any) => this.openEdit(event.row.data as AssessmentSheet)
    }
  ];

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
        .catch(error => this.rejectLoad(error));
    }
  }));

  readonly dataSource = asLegacyWidgetDataSource(new CustomStore({
    key: 'id',
    load: options => {
      const pageSize = Math.min(options.take ?? 20, 100);
      const sort = this.readSort(options.sort);
      return firstValueFrom(this.assessmentSheets.list({
        page: Math.floor((options.skip ?? 0) / pageSize) + 1,
        pageSize,
        search: this.search.trim() || undefined,
        studentId: this.studentId ?? undefined,
        status: this.status ?? undefined,
        dateFrom: toDateOnly(this.dateFrom),
        dateTo: toDateOnly(this.dateTo),
        sortBy: sort.field,
        sortOrder: sort.order
      })).then(result => {
        this.loadError = '';
        return { data: result.items, totalCount: result.pagination.totalItems };
      }).catch(error => this.rejectLoad(error));
    }
  }));

  readonly studentDisplay = (student: Student | null): string => student
    ? `${student.studentCode} · ${student.fullName}${student.nickName ? ` (${student.nickName})` : ''}`
    : '';

  constructor(
    private readonly assessmentSheets: AssessmentSheetsService,
    private readonly students: StudentsService,
    private readonly googleSheet: GoogleSheetsService,
    public readonly router: Router
  ) {}

  ngOnDestroy(): void {
    if (this.searchTimer !== undefined) {
      window.clearTimeout(this.searchTimer);
    }
  }

  scheduleSearch(): void {
    if (this.searchTimer !== undefined) {
      window.clearTimeout(this.searchTimer);
    }
    this.searchTimer = window.setTimeout(() => this.applyFilters(), 300);
  }

  applyFilters(): void {
    if (this.searchTimer !== undefined) {
      window.clearTimeout(this.searchTimer);
      this.searchTimer = undefined;
    }
    this.grid?.instance.pageIndex(0);
    void this.grid?.instance.refresh();
  }

  retryLoad(): void {
    void this.grid?.instance.refresh();
  }

  resetFilters(): void {
    this.search = '';
    this.status = null;
    this.studentId = null;
    this.dateFrom = '';
    this.dateTo = '';
    this.applyFilters();
  }

  openCreate(): void {
    void this.router.navigate(['/assessment-sheets/new']);
  }

  openEdit(sheet: AssessmentSheet): void {
    void this.router.navigate(['/assessment-sheets', sheet.id, 'edit']);
  }

  showDialogColumnChooser(): void {
    this.grid?.instance.option('columnChooser.mode', 'select');
    const gridInstance: any = this.grid?.instance;
    const chooser = gridInstance?.getController?.('columnChooser');
    const visible = chooser?.component?._views?.columnChooserView?._popupContainer?._options?.visible;
    if (visible) {
      this.grid?.instance.hideColumnChooser();
    } else {
      this.grid?.instance.showColumnChooser();
    }
  }

  openImportFilePicker(input: HTMLInputElement): void {
    if (this.importDisabled) {
      return;
    }
    input.value = '';
    input.click();
  }

  async previewImportExcel(event: Event): Promise<void> {
    if (this.importDisabled) {
      return;
    }

    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.[0] ?? null;
    if (!file) {
      return;
    }

    if (!this.isXlsxFile(file)) {
      notify('Chỉ hỗ trợ import file Excel định dạng .xlsx.', 'warning', 3000);
      if (input) {
        input.value = '';
      }
      return;
    }

    this.selectedImportFile = file;
    this.importPreviewLoading = true;
    this.importPreviewVisible = false;
    this.importPreview = null;
    this.importPreviewRows = [];
    this.importResult = null;
    this.loadError = '';
    try {
      const result = this.normalizePreviewResult(await firstValueFrom(this.assessmentSheets.previewImportExcel(file)));
      this.importPreview = result.summary;
      this.importPreviewRows = result.rows;
      this.importPreviewVisible = true;
    } catch (error) {
      const apiError = ApiError.from(error);
      this.loadError = this.withTrace(apiError);
      notify(this.loadError, 'error', 3500);
      this.selectedImportFile = null;
    } finally {
      this.importPreviewLoading = false;
      if (input) {
        input.value = '';
      }
    }
  }

  async submitImportExcel(): Promise<void> {
    if (!this.canSubmitImport || !this.selectedImportFile) {
      return;
    }

    this.importing = true;
    this.importResult = null;
    this.loadError = '';
    try {
      const result = await firstValueFrom(this.assessmentSheets.importExcel(this.selectedImportFile));
      this.importResult = this.normalizeImportResult(result);
      await this.grid?.instance.refresh();
      this.importing = false;
      this.importPreviewVisible = false;
      this.importPreview = null;
      this.importPreviewRows = [];
      this.selectedImportFile = null;
      notify(this.importSuccessText(this.importResult), 'success', 3500);
    } catch (error) {
      const apiError = ApiError.from(error);
      this.loadError = this.withTrace(apiError);
      notify(this.loadError, 'error', 3500);
    } finally {
      this.importing = false;
    }
  }

  closeImportPreview(): void {
    if (this.importing) {
      return;
    }
    this.importPreviewVisible = false;
    this.importPreview = null;
    this.importPreviewRows = [];
    this.selectedImportFile = null;
  }

  onImportPreviewHiding(event: { cancel?: boolean }): void {
    if (this.importing) {
      event.cancel = true;
      return;
    }
    this.importPreview = null;
    this.importPreviewRows = [];
    this.selectedImportFile = null;
  }

  onImportPreviewRowPrepared(event: any): void {
    if (event?.rowType !== 'data' || !event.rowElement) {
      return;
    }
    if (this.importRowHasErrors(event.data)) {
      event.rowElement.classList.add('import-row-error');
    } else if (this.importRowHasWarnings(event.data)) {
      event.rowElement.classList.add('import-row-warning');
    }
  }

  async syncAssessmentsFromGoogleSheets(): Promise<void> {
    if (this.syncing) {
      return;
    }

    const accepted = await custom({
      title: 'Xác nhận',
      messageHtml: '<i>Đồng bộ dữ liệu đánh giá từ Google Sheets?</i>',
      buttons: [
        { text: 'Không', onClick: () => false },
        { text: 'Có', onClick: () => true }
      ]
    }).show();
    if (!accepted) {
      return;
    }

    this.syncing = true;
    this.loadError = '';
    try {
      const result = await firstValueFrom(this.googleSheet.syncFromGoogleSheets({}));
      await this.grid?.instance.refresh();
      notify(`Đã đồng bộ Google Sheets. Thêm mới ${result.insertedRows} dòng.`, 'success', 2500);
    } catch (error) {
      const apiError = ApiError.from(error);
      this.loadError = this.withTrace(apiError);
      notify(this.loadError, 'error', 3500);
    } finally {
      this.syncing = false;
    }
  }

  statusText(status: AssessmentSheetStatus): string {
    return this.statuses.find(item => item.value === status)?.text ?? 'Không xác định';
  }

  dateText(value: string | null | undefined): string {
    return value ? formatDateOnly(toDateOnly(value) ?? value.substring(0, 10)) : '—';
  }

  private readSort(sortValue: unknown): { field: string; order: 'asc' | 'desc' } {
    const sort = Array.isArray(sortValue) ? sortValue[0] : sortValue;
    const config = sort && typeof sort === 'object' ? sort as { selector?: unknown; desc?: boolean } : undefined;
    const requested = typeof config?.selector === 'string' ? config.selector : 'updatedAt';
    return {
      field: ASSESSMENT_SHEET_SORT_FIELDS.has(requested) ? requested : 'updatedAt',
      order: config?.desc ? 'desc' : 'asc'
    };
  }

  private rejectLoad(error: unknown): Promise<never> {
    const apiError = ApiError.from(error);
    this.loadError = this.withTrace(apiError);
    notify(this.loadError, 'error', 3500);
    return Promise.reject(apiError);
  }

  private withTrace(error: ApiError): string {
    return error.traceId ? `${error.message} Mã tra cứu: ${error.traceId}` : error.message;
  }

  get syncDisabled(): boolean {
    return this.syncing || this.importing || this.importPreviewLoading;
  }

  get syncButtonText(): string {
    return this.syncing ? 'Đang đồng bộ...' : 'Đồng bộ Google Sheets';
  }

  get importDisabled(): boolean {
    return this.importing || this.importPreviewLoading || this.syncing;
  }

  get importButtonText(): string {
    if (this.importPreviewLoading) {
      return 'Đang đọc file...';
    }
    return this.importing ? 'Đang nhập...' : 'Nhập Excel';
  }

  get canSubmitImport(): boolean {
    return !!this.selectedImportFile
      && !!this.importPreview
      && this.importPreview.canImport
      && !this.importing
      && !this.importPreviewLoading;
  }

  importSuccessText(result: AssessmentSheetImportExcelResult): string {
    return `Đã nhập Excel: tạo mới ${result.createdSheetCount}, cập nhật ${result.updatedSheetCount}, nhập ${result.importedRecordCount} dòng.`;
  }

  importSheetCountText(result: AssessmentSheetImportExcelResult): string {
    return result.sheets.length > 0
      ? `Có ${result.sheets.length} bảng đánh giá được trả về sau import.`
      : 'Không có bảng đánh giá nào được trả về sau import.';
  }

  importActionText(action: string | null | undefined): string {
    switch (action) {
      case 'Created':
        return 'Tạo mới';
      case 'Updated':
        return 'Cập nhật';
      case 'Invalid':
        return 'Không hợp lệ';
      case 'SkippedDuplicate':
        return 'Bỏ qua';
      default:
        return action || '—';
    }
  }

  importMessagesText(messages: string[] | null | undefined): string {
    const text = (messages ?? []).filter(Boolean).join('; ');
    return text || '—';
  }

  importDuplicateText(row: AssessmentSheetImportExcelPreviewRow | null | undefined): string {
    return row?.action === 'SkippedDuplicate' || row?.isDuplicate ? 'Trùng' : 'Không';
  }

  importRowHasErrors(row: AssessmentSheetImportExcelPreviewRow | null | undefined): boolean {
    return (row?.errors?.length ?? 0) > 0;
  }

  importRowHasWarnings(row: AssessmentSheetImportExcelPreviewRow | null | undefined): boolean {
    return (row?.warnings?.length ?? 0) > 0;
  }

  private isXlsxFile(file: File): boolean {
    return /\.xlsx$/i.test(file.name);
  }

  private normalizeImportResult(result: AssessmentSheetImportExcelResult): AssessmentSheetImportExcelResult {
    return {
      ...result,
      warnings: result.warnings ?? [],
      sheets: result.sheets ?? []
    };
  }

  private normalizePreviewResult(result: AssessmentSheetImportExcelPreviewResult): AssessmentSheetImportExcelPreviewResult {
    return {
      ...result,
      rows: (result.rows ?? []).map(row => ({
        ...row,
        isDuplicate: row.action === 'SkippedDuplicate' || !!row.isDuplicate,
        errors: row.errors ?? [],
        warnings: row.warnings ?? []
      }))
    };
  }
}
