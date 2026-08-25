import { Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, SimpleChanges, ViewChild } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import CustomStore from 'devextreme/data/custom_store';
import { custom } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';
import { DxDataGridComponent } from 'devextreme-angular/ui/data-grid';
import { ApiError } from '../../core/models/api-error';
import { Assessment, AssessmentGroup } from '../../core/models/api.models';
import { asLegacyWidgetDataSource, LegacyWidgetDataSource } from '../../core/models/devextreme-legacy.types';
import { AssessmentGroupsService } from '../../core/services/assessment-groups.service';
import { AssessmentsService } from '../../core/services/assessments.service';
import { GoogleSheetsService } from '../../core/services/google-sheets.service';

const ASSESSMENT_PICKER_SORT_FIELDS = new Set(['code', 'name', 'rowindex']);
const SELECTED_ROW_CLASS = 'assessment-picker-selected-row';

@Component({
  selector: 'app-assessment-picker',
  templateUrl: './assessment-picker.component.html',
  styleUrls: ['./assessment-picker.component.scss']
})
export class AssessmentPickerComponent implements OnChanges, OnInit, OnDestroy {
  @ViewChild(DxDataGridComponent) grid?: DxDataGridComponent;
  @Input() selectedIds: string[] = [];
  @Output() selectedIdsChange = new EventEmitter<string[]>();

  search = '';
  groupLv1Name: string | null = null;
  groupLv2Name: string | null = null;
  groupLv3Name: string | null = null;
  groupLv1Placeholder = 'Nhóm tuổi';
  groupLv2Placeholder = 'Nhóm 2';
  groupLv3Placeholder = 'Nhóm 3';
  filtersExpanded = true;
  loadError = '';
  saving = false;
  groupLv2DataSource: LegacyWidgetDataSource | [] = [];
  groupLv3DataSource: LegacyWidgetDataSource | [] = [];
  visibleAssessmentIds: string[] = [];
  private searchTimer?: number;
  private selectedIdSet = new Set<string>();
  readonly gridRemoteOperations = { paging: true, sorting: true };
  readonly gridPageSizes = [20, 50, 100];
  readonly searchInputAttr = { 'aria-label': 'Tìm mục đánh giá theo mã, tên' };
  readonly groupLv1InputAttr = { 'aria-label': 'Lọc theo nhóm tuổi' };
  readonly groupLv2InputAttr = { 'aria-label': 'Lọc theo nhóm 2' };
  readonly groupLv3InputAttr = { 'aria-label': 'Lọc theo nhóm 3' };

  readonly groupLv1DataSource = asLegacyWidgetDataSource(new CustomStore({
    key: 'id',
    byKey: key => firstValueFrom(this.groups.get(String(key))),
    load: options => {
      const pageSize = Math.min(options.take ?? 100, 100);
      return firstValueFrom(this.groups.list({
        level: 1,
        page: Math.floor((options.skip ?? 0) / pageSize) + 1,
        pageSize,
        search: typeof options.searchValue === 'string' ? options.searchValue.trim() || undefined : undefined,
        sortBy: 'name',
        sortOrder: 'asc'
      })).then(result => {
        this.groupLv1Placeholder = `Nhóm tuổi (${result.pagination.totalItems})`;
        return { data: result.items, totalCount: result.pagination.totalItems };
      }).catch(error => {
        this.groupLv1Placeholder = 'Nhóm tuổi: Lỗi tải dữ liệu';
        return this.rejectLoad(error);
      });
    }
  }));

  readonly dataSource = asLegacyWidgetDataSource(new CustomStore({
    key: 'id',
    byKey: key => firstValueFrom(this.assessments.get(String(key))),
    load: options => {
      const pageSize = Math.min(options.take ?? 20, 100);
      const sort = this.readSort(options.sort);
      return firstValueFrom(this.assessments.list({
        page: Math.floor((options.skip ?? 0) / pageSize) + 1,
        pageSize,
        search: this.search.trim() || undefined,
        groupLv1Name: this.groupLv1Name ?? undefined,
        groupLv2Name: this.groupLv2Name ?? undefined,
        groupLv3Name: this.groupLv3Name ?? undefined,
        sortBy: sort.field,
        sortOrder: sort.order
      })).then(result => {
        this.loadError = '';
        return { data: result.items, totalCount: result.pagination.totalItems };
      }).catch(error => this.rejectLoad(error));
    }
  }));

  readonly groupDisplay = (group: AssessmentGroup | null): string => group ? `${group.name}` : '';

  constructor(
    private readonly assessments: AssessmentsService,
    private readonly groups: AssessmentGroupsService,
    private readonly googleSheet: GoogleSheetsService
  ) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['selectedIds']) {
      this.refreshSelectedSet();
    }
  }

  ngOnInit(): void {
    this.refreshSelectedSet();
    this.loadLv2DataSource();
    this.loadLv3DataSource();
  }

  ngOnDestroy(): void {
    if (this.searchTimer !== undefined) {
      window.clearTimeout(this.searchTimer);
    }
  }

  focus(): void {
    (this.grid?.instance as unknown as { focus?: () => void } | undefined)?.focus?.();
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
    this.groupLv1Name = null;
    this.groupLv2Name = null;
    this.groupLv3Name = null;
    this.loadLv2DataSource();
    this.loadLv3DataSource();
    this.applyFilters();
  }

  showDialogColumnChooser(): void {
    this.grid?.instance.option('columnChooser.mode', 'select');
    if (this.isColumnChooserOpen()) {
      this.grid?.instance.hideColumnChooser();
    } else {
      this.grid?.instance.showColumnChooser();
    }
  }

  onGroupLv1Changed(): void {
    this.groupLv2Name = null;
    this.groupLv3Name = null;
    this.loadLv2DataSource();
    this.loadLv3DataSource();
    this.applyFilters();
  }

  onGroupLv2Changed(): void {
    this.groupLv3Name = null;
    this.loadLv3DataSource();
    this.applyFilters();
  }

  onGroupLv3Changed(): void {
    this.applyFilters();
  }

  isSelected(id: unknown): boolean {
    const normalizedId = this.normalizeSelectedId(id);
    return normalizedId ? this.selectedIdSet.has(normalizedId) : false;
  }

  selectCheckboxHint(assessment: Assessment | null | undefined): string {
    return assessment ? `Chọn mục ${assessment.code} · ${assessment.name}` : 'Chọn mục đánh giá';
  }

  onSelectCheckboxChanged(id: unknown, event: { value?: boolean; event?: unknown }): void {
    if (!event.event) {
      return;
    }
    const normalizedId = this.normalizeSelectedId(id);
    if (!normalizedId) {
      return;
    }
    const next = new Set(this.selectedIdSet);
    if (event.value === true) {
      next.add(normalizedId);
    } else {
      next.delete(normalizedId);
    }
    this.emitSelectedIds(next);
  }

  onSelectAllVisibleChanged(event: Event): void {
    if (this.visibleAssessmentIds.length === 0) {
      return;
    }
    const input = event.target instanceof HTMLInputElement ? event.target : null;
    this.setAllVisibleSelected(input?.checked === true);
  }

  setAllVisibleSelected(selected: boolean): void {
    const next = new Set(this.selectedIdSet);
    if (selected) {
      this.visibleAssessmentIds.forEach(id => next.add(id));
    } else {
      this.visibleAssessmentIds.forEach(id => next.delete(id));
    }
    this.emitSelectedIds(next);
  }

  onContentReady(): void {
    this.refreshVisibleAssessmentIds();
  }

  onRowPrepared(event: { rowType?: string; data?: Assessment; rowElement?: unknown }): void {
    if (event.rowType !== 'data') {
      return;
    }
    const rowClassList = this.getRowClassList(event.rowElement);
    if (!rowClassList) {
      return;
    }
    if (this.isSelected(event.data?.id)) {
      rowClassList.add(SELECTED_ROW_CLASS);
    } else {
      rowClassList.remove(SELECTED_ROW_CLASS);
    }
  }

  get saveDisabled(): boolean {
    return this.saving;
  }

  get saveButtonText(): string {
    return this.saving ? 'Đang đồng bộ…' : 'Đồng bộ GGSheet';
  }

  get selectAllVisibleValue(): boolean | null {
    if (this.visibleAssessmentIds.length === 0) {
      return false;
    }
    const selectedCount = this.visibleAssessmentIds.filter(id => this.selectedIdSet.has(id)).length;
    if (selectedCount === 0) {
      return false;
    }
    return selectedCount === this.visibleAssessmentIds.length ? true : null;
  }

  get selectAllVisibleChecked(): boolean {
    return this.selectAllVisibleValue === true;
  }

  get selectAllVisibleIndeterminate(): boolean {
    return this.selectAllVisibleValue === null;
  }

  get selectAllVisibleText(): string {
    if (this.visibleAssessmentIds.length === 0) {
      return 'Chọn tất cả';
    }
    const selectedCount = this.visibleAssessmentIds.filter(id => this.selectedIdSet.has(id)).length;
    return `Chọn tất cả (${selectedCount}/${this.visibleAssessmentIds.length})`;
  }

  async syncAssessmentsFromGGSheet(): Promise<void> {
    if (this.saving) {
      return;
    }
    this.saving = true;
    this.loadError = '';
    try {
      const resultConfirm = await custom({
        title: 'Xác nhận',
        messageHtml: '<i>Yêu cầu đồng bộ dữ liệu từ Google Sheets?</i>',
        buttons: [
          {
            text: 'Không',
            onClick: () => false,
            focusStateEnabled: true,
            elementAttr: { class: 'dx-button-focused' }
          },
          {
            text: 'Có',
            onClick: () => true,
            elementAttr: { class: 'dx-button-danger' }
          }
        ]
      }).show();
      if (resultConfirm) {
        const result = await firstValueFrom(this.googleSheet.syncFromGoogleSheets({}));
        this.retryLoad();
        notify(`Đã đồng bộ dữ liệu từ GGSheet. Thêm mới [${result.insertedRows}] dòng`, 'success', 2500);
      }
    } catch (error) {
      const apiError = ApiError.from(error);
      this.loadError = this.withTrace(apiError);
      this.notifyError(apiError);
    } finally {
      this.saving = false;
    }
  }

  private loadLv2DataSource(): void {
    this.groupLv2DataSource = asLegacyWidgetDataSource(new CustomStore({
      key: 'id',
      byKey: key => firstValueFrom(this.groups.get(String(key))),
      load: options => {
        const pageSize = Math.min(options.take ?? 100, 100);
        return firstValueFrom(this.groups.list({
          level: 2,
          parentName: this.groupLv1Name ?? undefined,
          search: typeof options.searchValue === 'string' ? options.searchValue.trim() || undefined : undefined,
          page: Math.floor((options.skip ?? 0) / pageSize) + 1,
          pageSize,
          sortBy: 'name',
          sortOrder: 'asc'
        })).then(result => {
          this.groupLv2Placeholder = `Nhóm 2 (${result.pagination.totalItems})`;
          return { data: result.items, totalCount: result.pagination.totalItems };
        }).catch(error => {
          this.groupLv2Placeholder = 'Nhóm 2: Lỗi tải dữ liệu';
          return this.rejectLoad(error);
        });
      }
    }));
  }

  private loadLv3DataSource(): void {
    this.groupLv3DataSource = asLegacyWidgetDataSource(new CustomStore({
      key: 'id',
      byKey: key => firstValueFrom(this.groups.get(String(key))),
      load: options => {
        const pageSize = Math.min(options.take ?? 100, 100);
        return firstValueFrom(this.groups.list({
          level: 3,
          page: Math.floor((options.skip ?? 0) / pageSize) + 1,
          parentName: this.groupLv2Name ?? undefined,
          parentParentName: this.groupLv1Name ?? undefined,
          search: typeof options.searchValue === 'string' ? options.searchValue.trim() || undefined : undefined,
          pageSize,
          sortBy: 'name',
          sortOrder: 'asc'
        })).then(result => {
          this.groupLv3Placeholder = `Nhóm 3 (${result.pagination.totalItems})`;
          return { data: result.items, totalCount: result.pagination.totalItems };
        }).catch(error => {
          this.groupLv3Placeholder = 'Nhóm 3: Lỗi tải dữ liệu';
          return this.rejectLoad(error);
        });
      }
    }));
  }

  private readSort(sortValue: unknown): { field: string; order: 'asc' | 'desc' } {
    const sort = Array.isArray(sortValue) ? sortValue[0] : sortValue;
    const config = sort && typeof sort === 'object' ? sort as { selector?: unknown; desc?: boolean } : undefined;
    const requested = typeof config?.selector === 'string' ? config.selector.toLowerCase() : 'rowindex';
    return {
      field: ASSESSMENT_PICKER_SORT_FIELDS.has(requested) ? requested : 'rowindex',
      order: config?.desc ? 'desc' : 'asc'
    };
  }

  private normalizeSelectedIds(keys: unknown[]): string[] {
    return keys
      .map(value => this.normalizeSelectedId(value))
      .filter((value): value is string => !!value);
  }

  private normalizeSelectedId(value: unknown): string | null {
    if (value === null || value === undefined) {
      return null;
    }
    const id = String(value).trim();
    return id || null;
  }

  private refreshSelectedSet(): void {
    this.selectedIdSet = new Set(this.normalizeSelectedIds(this.selectedIds));
    this.grid?.instance.repaint();
  }

  private emitSelectedIds(selectedIdSet: Set<string>): void {
    const selectedIds = Array.from(selectedIdSet);
    this.selectedIds = selectedIds;
    this.selectedIdSet = selectedIdSet;
    this.selectedIdsChange.emit(selectedIds);
    this.grid?.instance.repaint();
  }

  private refreshVisibleAssessmentIds(): void {
    const gridInstance: any = this.grid?.instance;
    const rows: Array<{ data?: Assessment }> = gridInstance?.getVisibleRows?.() ?? [];
    this.visibleAssessmentIds = rows
      .filter(row => row?.data)
      .map(row => this.normalizeSelectedId(row.data?.id))
      .filter((value): value is string => !!value);
  }

  private getRowClassList(rowElement: unknown): DOMTokenList | null {
    if (rowElement instanceof HTMLElement) {
      return rowElement.classList;
    }
    const possibleElement = Array.isArray(rowElement)
      ? rowElement[0]
      : (rowElement as { get?: (index: number) => unknown } | undefined)?.get?.(0);
    return possibleElement instanceof HTMLElement ? possibleElement.classList : null;
  }

  private isColumnChooserOpen(): boolean {
    const gridInstance: any = this.grid?.instance;
    const chooser = gridInstance?.getController?.('columnChooser');
    return !!chooser?.component?._views?.columnChooserView?._popupContainer?._options?.visible;
  }

  private rejectLoad(error: unknown): Promise<never> {
    const apiError = ApiError.from(error);
    this.loadError = this.withTrace(apiError);
    this.notifyError(apiError);
    return Promise.reject(apiError);
  }

  private notifyError(error: ApiError): void {
    notify(this.withTrace(error), 'error', 3500);
  }

  private withTrace(error: ApiError): string {
    return error.traceId ? `${error.message} Mã tra cứu: ${error.traceId}` : error.message;
  }
}
