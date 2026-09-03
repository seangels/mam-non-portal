import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
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
import { AssessmentSheetsService } from '../../core/services/assessment-sheets.service';
import { patchGridBestFit } from '../../core/errors/dx-grid-bestfit-guard';
import { formatDateOnly, toDateOnly } from '../../core/utils/date-only';
import { formatAssessmentPeriod } from './assessment-sheet-plan-preview.models';

// Danh sách tải hết về client (giống bảng picker) rồi để lưới tự lọc/sắp/phân trang bằng
// filter row + header filter + column chooser + toolbar của DevExtreme. Không còn panel lọc riêng.
const SHEET_CACHE_PAGE_SIZE = 100;

@Component({
  selector: 'app-assessment-sheets',
  templateUrl: './assessment-sheets.component.html',
  styleUrls: ['./assessment-sheets.component.scss']
})
export class AssessmentSheetsComponent implements OnInit, OnDestroy {
  @ViewChild(DxDataGridComponent) grid?: DxDataGridComponent;
  @ViewChild('importFileInput') importFileInput?: ElementRef<HTMLInputElement>;

  sheets: AssessmentSheet[] = [];
  loading = false;
  private destroyed = false;
  private gridBestFitGuarded = false;
  selectedSheets: AssessmentSheet[] = [];
  importing = false;
  importPreviewLoading = false;
  importPreviewVisible = false;
  importPreview: AssessmentSheetImportExcelPreviewSummaryResult | null = null;
  importPreviewRows: AssessmentSheetImportExcelPreviewRow[] = [];
  importResult: AssessmentSheetImportExcelResult | null = null;
  loadError = '';
  private selectedImportFile: File | null = null;
  private importToolbarButton?: { option: (options: Record<string, unknown>) => void };
  private bulkActionToolbarButton?: { option: (options: Record<string, unknown>) => void };

  readonly statuses = ASSESSMENT_SHEET_STATUS_OPTIONS;
  // Header filter cột "Trạng thái" hiển thị nhãn tiếng Việt thay cho giá trị enum.
  readonly statusHeaderFilter = {
    dataSource: ASSESSMENT_SHEET_STATUS_OPTIONS.map(option => ({ text: option.text, value: option.value }))
  };
  bulkActionInProgress = false;
  readonly bulkActionItems: Array<{ id: string; text: string; kind: 'Plan' | 'Result'; format: 'Pdf' | 'Images' }> = [
    { id: 'plan-pdf', text: 'Tải PDF khcn', kind: 'Plan', format: 'Pdf' },
    { id: 'plan-img', text: 'Tải ảnh khcn', kind: 'Plan', format: 'Images' },
    { id: 'result-pdf', text: 'Tải PDF KQ', kind: 'Result', format: 'Pdf' },
    { id: 'result-img', text: 'Tải ảnh KQ', kind: 'Result', format: 'Images' }
  ];
  readonly rowButtons = [
    {
      hint: 'Chỉnh sửa',
      icon: 'edit',
      onClick: (event: any) => this.openEdit(event.row.data as AssessmentSheet)
    }
  ];

  // Ô lọc ngày (startDate/dueDate) làm việc ở mức THÁNG: cận dưới = đầu tháng đã chọn,
  // cận trên = hết tháng đã chọn (`< đầu tháng kế tiếp`). Áp cho mọi toán tử của filter row.
  readonly monthGranularFilterExpression = function (
    this: {
      dataField?: string;
      defaultCalculateFilterExpression: (...args: unknown[]) => unknown;
    },
    filterValue: unknown,
    selectedFilterOperation: string | null,
    target: string
  ): unknown {
    const field = this.dataField;
    const monthStart = (value: unknown): Date | null => {
      const date = value instanceof Date ? value : (typeof value === 'string' ? new Date(value) : null);
      return date && !Number.isNaN(date.getTime()) ? new Date(date.getFullYear(), date.getMonth(), 1) : null;
    };
    const nextMonth = (date: Date): Date => new Date(date.getFullYear(), date.getMonth() + 1, 1);

    if (field && target === 'filterRow') {
      if (selectedFilterOperation === 'between' && Array.isArray(filterValue)) {
        const from = monthStart(filterValue[0]);
        const to = monthStart(filterValue[1]);
        const clauses: unknown[] = [];
        if (from) {
          clauses.push([field, '>=', from]);
        }
        if (to) {
          if (clauses.length > 0) {
            clauses.push('and');
          }
          clauses.push([field, '<', nextMonth(to)]);
        }
        return clauses.length > 0 ? clauses : null;
      }

      const month = monthStart(filterValue);
      if (month) {
        switch (selectedFilterOperation) {
          case '=': return [[field, '>=', month], 'and', [field, '<', nextMonth(month)]];
          case '<>': return [[field, '<', month], 'or', [field, '>=', nextMonth(month)]];
          case '>=': return [field, '>=', month];
          case '<=': return [field, '<', nextMonth(month)];
          case '>': return [field, '>=', nextMonth(month)];
          case '<': return [field, '<', month];
          default: break;
        }
      }
    }
    return this.defaultCalculateFilterExpression(filterValue, selectedFilterOperation, target);
  };

  constructor(
    private readonly assessmentSheets: AssessmentSheetsService,
    public readonly router: Router
  ) {}

  ngOnInit(): void {
    void this.loadAllSheets();
  }

  ngOnDestroy(): void {
    this.destroyed = true;
  }

  // DevExtreme 19.2: khi rời màn, event resize lúc route-outlet tháo phần tử làm lưới lên lịch
  // `_synchronizeColumns` (deferRender → `_toggleBestFitMode`); callback chạy sau khi widget đã
  // bị hủy → `_rowsView._getTableElement()` null → `Cannot read properties of null (reading 'css')`.
  // Chốt chặn thật ở `AppErrorHandler`; ở đây vá thêm trên ResizingController cho chắc.
  // LƯU Ý: `_synchronizeColumns`/`_toggleBestFitMode` nằm trên controller `resizing`, KHÔNG phải view.
  onGridInitialized(event: { component?: unknown }): void {
    this.installGridBestFitGuard(event.component);
  }

  onGridContentReady(event: { component?: unknown }): void {
    this.installGridBestFitGuard(event.component);
  }

  private installGridBestFitGuard(component: unknown): void {
    if (this.gridBestFitGuarded) {
      return;
    }
    const guarded = patchGridBestFit(component, '[AssessmentSheets]');
    if (guarded) {
      this.gridBestFitGuarded = true;
    }
  }

  async loadAllSheets(): Promise<void> {
    if (this.loading) {
      return;
    }
    this.loading = true;
    try {
      const loaded: AssessmentSheet[] = [];
      let page = 1;
      let totalPages = 1;
      do {
        const result = await firstValueFrom(this.assessmentSheets.list({
          page,
          pageSize: SHEET_CACHE_PAGE_SIZE,
          sortBy: 'updatedAt',
          sortOrder: 'desc'
        }));
        loaded.push(...result.items);
        totalPages = Math.max(1, result.pagination.totalPages
          || Math.ceil(result.pagination.totalItems / SHEET_CACHE_PAGE_SIZE));
        page += 1;
      } while (page <= totalPages && !this.destroyed);

      if (this.destroyed) {
        return;
      }
      this.sheets = loaded;
      this.loadError = '';
    } catch (error) {
      const apiError = ApiError.from(error);
      this.loadError = this.withTrace(apiError);
      notify(this.loadError, 'error', 3500);
    } finally {
      this.loading = false;
    }
  }

  retryLoad(): void {
    void this.loadAllSheets();
  }

  // Ô lọc ngày ở filter row (kể cả 2 ô của "between") hiển thị dạng chọn tháng M/yyyy.
  onEditorPreparing(event: {
    parentType?: string;
    dataField?: string;
    editorName?: string;
    editorOptions?: { displayFormat?: string; calendarOptions?: unknown };
  }): void {
    if (event.parentType === 'filterRow'
      && event.editorName === 'dxDateBox'
      && (event.dataField === 'startDate' || event.dataField === 'dueDate')
      && event.editorOptions) {
      event.editorOptions.displayFormat = 'M/yyyy';
      event.editorOptions.calendarOptions = { maxZoomLevel: 'year' };
    }
  }

  // Toolbar của lưới (DevExtreme 19.2 chưa khai báo được option `toolbar` → dùng event này):
  // đẩy các nút hành động vào toolbar, giữ nút "Chọn cột" + ô tìm kiếm mặc định của datagrid.
  onToolbarPreparing(event: { toolbarOptions?: { items?: any[] } }): void {
    const items = event.toolbarOptions?.items;
    if (!items) {
      return;
    }
    items.unshift(
      {
        location: 'before',
        widget: 'dxButton',
        options: {
          icon: 'plus',
          text: 'Thêm bảng đánh giá',
          type: 'default',
          hint: 'Tạo bảng đánh giá',
          onClick: () => this.openCreate()
        }
      },
      {
        location: 'before',
        widget: 'dxButton',
        options: {
          icon: 'upload',
          text: this.importButtonText,
          stylingMode: 'outlined',
          hint: 'Nhập bảng đánh giá từ file Excel .xlsx',
          disabled: this.importDisabled,
          onInitialized: (e: { component?: unknown }) => {
            this.importToolbarButton = e.component as { option: (options: Record<string, unknown>) => void };
          },
          onClick: () => this.openImportFilePicker(this.importFileInput?.nativeElement)
        }
      },
      {
        location: 'before',
        widget: 'dxDropDownButton',
        options: {
          icon: 'download',
          text: this.bulkActionText,
          stylingMode: 'outlined',
          items: this.bulkActionItems,
          displayExpr: 'text',
          keyExpr: 'id',
          disabled: this.bulkActionDisabled,
          dropDownOptions: { width: 200 },
          hint: 'Chọn nhiều dòng rồi tải PDF/ảnh khcn hoặc kết quả (gộp thành zip)',
          onInitialized: (e: { component?: unknown }) => {
            this.bulkActionToolbarButton = e.component as { option: (options: Record<string, unknown>) => void };
          },
          onItemClick: (e: { itemData?: { id: string } }) => this.onBulkAction(e)
        }
      },
      {
        location: 'before',
        widget: 'dxButton',
        options: {
          icon: 'clearformat',
          text: 'Đặt lại lọc lưới',
          stylingMode: 'outlined',
          hint: 'Xóa filter row + header filter + ô tìm kiếm',
          onClick: () => this.resetGridFilters()
        }
      }
    );
  }

  // Đưa filter row + header filter + ô tìm kiếm của lưới về mặc định (không chạm sort/chọn cột).
  resetGridFilters(): void {
    const grid = this.grid?.instance;
    if (!grid) {
      return;
    }
    grid.clearFilter('row');
    grid.clearFilter('header');
    grid.clearFilter('search');
  }

  resetFilters(): void {
    this.resetGridFilters();
    this.clearSelection();
  }

  onSelectionChanged(event: { selectedRowsData?: AssessmentSheet[] }): void {
    this.selectedSheets = event.selectedRowsData ?? [];
    this.syncToolbar();
  }

  // Bảng đánh giá đã hủy: gạch ngang cả dòng cho dễ phân biệt.
  onRowPrepared(event: { rowType?: string; data?: AssessmentSheet; rowElement?: HTMLElement }): void {
    if (event.rowType === 'data' && event.rowElement?.classList) {
      event.rowElement.classList.toggle('sheet-row-canceled', event.data?.status === 'Canceled');
    }
  }

  get bulkActionText(): string {
    return this.bulkActionInProgress ? 'Đang xử lý...' : `Bulk Action (${this.selectedSheets.length})`;
  }

  get bulkActionDisabled(): boolean {
    return this.bulkActionInProgress || this.selectedSheets.length === 0;
  }

  async onBulkAction(event: { itemData?: { id: string } }): Promise<void> {
    const action = this.bulkActionItems.find(item => item.id === event.itemData?.id);
    if (!action || this.bulkActionDisabled) {
      return;
    }
    const ids = this.selectedSheets.map(sheet => sheet.id);
    this.bulkActionInProgress = true;
    this.loadError = '';
    this.syncToolbar();
    try {
      const blob = await firstValueFrom(this.assessmentSheets.downloadPdfArchive(ids, action.kind, action.format));
      this.downloadBlob(blob, `${action.text} ${this.timestamp()}.zip`);
      notify(`Đã tải ${action.text.toLowerCase()} cho ${ids.length} bảng đánh giá. Xem file _bo-qua.txt trong zip nếu có dòng bị bỏ qua.`, 'success', 4000);
    } catch (error) {
      const apiError = ApiError.from(error);
      this.loadError = this.withTrace(apiError);
      notify(this.loadError, 'error', 3500);
    } finally {
      this.bulkActionInProgress = false;
      this.syncToolbar();
    }
  }

  private syncToolbar(): void {
    this.importToolbarButton?.option({ text: this.importButtonText, disabled: this.importDisabled });
    this.bulkActionToolbarButton?.option({ text: this.bulkActionText, disabled: this.bulkActionDisabled });
  }

  private timestamp(): string {
    const now = new Date();
    const pad = (value: number): string => `${value}`.padStart(2, '0');
    return `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`;
  }

  private downloadBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.style.display = 'none';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    setTimeout(() => URL.revokeObjectURL(url), 0);
  }

  private clearSelection(): void {
    this.selectedSheets = [];
    this.grid?.instance.clearSelection();
    this.syncToolbar();
  }

  openCreate(): void {
    this.leaveTo(['/assessment-sheets/new']);
  }

  openEdit(sheet: AssessmentSheet): void {
    this.leaveTo(['/assessment-sheets', sheet.id, 'edit']);
  }

  // Cho lưới flush nốt các deferRender (resize/best-fit) đang chờ TRƯỚC khi component bị hủy,
  // để không còn callback nào chạy trên lưới đã destroy (kèm với vá ở `onGridInitialized`).
  private leaveTo(commands: string[]): void {
    if (this.destroyed) {
      return;
    }
    // Hoãn 1 nhịp cho lưới flush deferRender trước khi component bị hủy (kèm với guard ở
    // `patchGridBestFit`, giảm khả năng chạm lỗi best-fit của DevExtreme 19.2).
    requestAnimationFrame(() => window.setTimeout(() => {
      if (!this.destroyed) {
        void this.router.navigate(commands);
      }
    }, 0));
  }

  openImportFilePicker(input?: HTMLInputElement | null): void {
    if (this.importDisabled || !input) {
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
    this.syncToolbar();
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
      this.syncToolbar();
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
    this.syncToolbar();
    try {
      const result = await firstValueFrom(this.assessmentSheets.importExcel(this.selectedImportFile));
      this.importResult = this.normalizeImportResult(result);
      await this.loadAllSheets();
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
      this.syncToolbar();
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

  statusText(status: AssessmentSheetStatus): string {
    return this.statuses.find(item => item.value === status)?.text ?? 'Không xác định';
  }

  dateText(value: string | null | undefined): string {
    return value ? formatDateOnly(toDateOnly(value) ?? value.substring(0, 10)) : '—';
  }

  // Cột "Bắt đầu" hiển thị cả khoảng kế hoạch (giống tên kế hoạch): "3 tháng 10.11.12.26".
  periodText(sheet: AssessmentSheet): string {
    return formatAssessmentPeriod(sheet.startDate, sheet.dueDate);
  }

  // startDate/dueDate chỉ cần độ chính xác tới tháng: hiển thị M/yyyy (tháng không đệm số 0).
  monthText(value: string | null | undefined): string {
    const iso = value ? toDateOnly(value) ?? value.substring(0, 10) : undefined;
    if (!iso) {
      return '—';
    }
    const [year, month] = iso.split('-');
    return `${Number(month)}/${year}`;
  }

  private withTrace(error: ApiError): string {
    return error.traceId ? `${error.message} Mã tra cứu: ${error.traceId}` : error.message;
  }

  get importDisabled(): boolean {
    return this.importing || this.importPreviewLoading;
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
