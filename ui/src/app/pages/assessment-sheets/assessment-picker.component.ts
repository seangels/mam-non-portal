import { Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, SimpleChanges, ViewChild } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import notify from 'devextreme/ui/notify';
import { DxDataGridComponent } from 'devextreme-angular/ui/data-grid';
import { ApiError } from '../../core/models/api-error';
import { Assessment, AssessmentGroup, AssessmentListQuery } from '../../core/models/api.models';
import { AssessmentGrade, ASSESSMENT_GRADE_OPTIONS } from '../../core/models/api.models.assessment-sheets';
import { AssessmentsService } from '../../core/services/assessments.service';
import { includesVietnamese } from '../../core/utils/vietnamese-search';

const SELECTED_ROW_CLASS = 'assessment-picker-selected-row';
const ASSESSMENT_CACHE_PAGE_SIZE = 100;
type AssessmentPickerViewMode = 'all' | 'selected';
type AssessmentPickerMode = 'select' | 'add';
type LatestGradeFilterValue = AssessmentGrade | 'none';

@Component({
  selector: 'app-assessment-picker',
  templateUrl: './assessment-picker.component.html',
  styleUrls: ['./assessment-picker.component.scss']
})
export class AssessmentPickerComponent implements OnChanges, OnInit, OnDestroy {
  @ViewChild(DxDataGridComponent) grid?: DxDataGridComponent;
  @Input() mode: AssessmentPickerMode = 'select';
  @Input() selectedIds: string[] = [];
  @Input() studentId: string | null = null;
  @Input() existingCodes: string[] = [];
  @Input() addDisabled = false;
  @Output() selectedIdsChange = new EventEmitter<string[]>();
  @Output() assessmentAdd = new EventEmitter<Assessment>();

  search = '';
  groupLv1Name: string | null = null;
  groupLv2Name: string | null = null;
  groupLv3Name: string | null = null;
  latestGradeFilters: LatestGradeFilterValue[] = [];
  viewMode: AssessmentPickerViewMode = 'all';
  groupLv1Placeholder = 'Nhóm tuổi';
  groupLv2Placeholder = 'Nhóm 2';
  groupLv3Placeholder = 'Nhóm 3';
  filtersExpanded = true;
  loadError = '';
  loading = false;
  allAssessments: Assessment[] = [];
  filteredAssessments: Assessment[] = [];
  groupLv1DataSource: AssessmentGroup[] = [];
  groupLv2DataSource: AssessmentGroup[] = [];
  groupLv3DataSource: AssessmentGroup[] = [];
  visibleAssessmentIds: string[] = [];
  private searchTimer?: number;
  private selectedIdSet = new Set<string>();
  private existingCodeSet = new Set<string>();
  private selectedViewSnapshotIdSet = new Set<string>();
  private initialized = false;
  private loadedStudentId: string | null = null;
  readonly gridRemoteOperations = false;
  readonly gridPageSizes = [20, 50, 100];
  readonly searchInputAttr = { 'aria-label': 'Tìm mục đánh giá theo mã, tên' };
  readonly groupLv1InputAttr = { 'aria-label': 'Lọc theo nhóm tuổi' };
  readonly groupLv2InputAttr = { 'aria-label': 'Lọc theo nhóm 2' };
  readonly groupLv3InputAttr = { 'aria-label': 'Lọc theo nhóm 3' };
  readonly latestGradeInputAttr = { 'aria-label': 'Lọc theo kết quả gần nhất' };
  readonly latestGradeOptions: Array<{ value: LatestGradeFilterValue; text: string }> = [
    { value: 'none', text: 'Chưa có' },
    ...ASSESSMENT_GRADE_OPTIONS
  ];
  readonly viewModeInputAttr = { 'aria-label': 'Chế độ xem mục đánh giá' };
  readonly viewModeOptions: Array<{ value: AssessmentPickerViewMode; text: string }> = [
    { value: 'all', text: 'Xem tất cả' },
    { value: 'selected', text: 'Chỉ những mục đã chọn' }
  ];

  readonly groupDisplay = (group: AssessmentGroup | null): string => group ? `${group.name}` : '';

  constructor(
    private readonly assessments: AssessmentsService
  ) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['selectedIds']) {
      this.refreshSelectedSet();
    }
    if (changes['existingCodes']) {
      this.refreshExistingCodeSet();
    }
    if (changes['studentId'] && this.initialized) {
      const nextStudentId = this.normalizeOptionalId(this.studentId);
      if (nextStudentId !== this.loadedStudentId) {
        void this.loadAssessmentsFromServer();
      }
    }
  }

  ngOnInit(): void {
    this.refreshSelectedSet();
    this.refreshExistingCodeSet();
    this.initialized = true;
    void this.loadAssessmentsFromServer();
  }

  ngOnDestroy(): void {
    if (this.searchTimer !== undefined) {
      window.clearTimeout(this.searchTimer);
    }
  }

  focus(): void {
    (this.grid?.instance as unknown as { focus?: () => void } | undefined)?.focus?.();
  }

  getSelectedAssessments(): Assessment[] {
    const assessmentById = new Map(this.allAssessments.map(assessment => [assessment.id, assessment]));
    return this.selectedIds
      .map(id => assessmentById.get(id))
      .filter((assessment): assessment is Assessment => !!assessment);
  }

  getCachedAssessments(): Assessment[] {
    return this.allAssessments;
  }

  get isSelectMode(): boolean {
    return this.mode !== 'add';
  }

  get isAddMode(): boolean {
    return this.mode === 'add';
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
    const source = this.viewMode === 'selected'
      ? this.allAssessments.filter(assessment => this.isInSelectedViewSnapshot(assessment.id))
      : this.allAssessments;
    this.filteredAssessments = source.filter(assessment => this.matchesCurrentFilters(assessment));
    this.grid?.instance.pageIndex(0);
    this.grid?.instance.repaint();
  }

  retryLoad(): void {
    void this.loadAssessmentsFromServer();
  }

  resetFilters(): void {
    this.search = '';
    this.groupLv1Name = null;
    this.groupLv2Name = null;
    this.groupLv3Name = null;
    this.latestGradeFilters = [];
    this.refreshGroupOptions();
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
    this.refreshGroupOptions();
    this.applyFilters();
  }

  onGroupLv2Changed(): void {
    this.groupLv3Name = null;
    this.refreshGroupOptions();
    this.applyFilters();
  }

  onGroupLv3Changed(): void {
    this.applyFilters();
  }

  onLatestGradeFiltersChanged(): void {
    this.applyFilters();
  }

  onViewModeChanged(): void {
    if (this.viewMode === 'selected') {
      this.selectedViewSnapshotIdSet = new Set(this.selectedIdSet);
    } else {
      this.selectedViewSnapshotIdSet.clear();
    }
    this.applyFilters();
  }

  isSelected(id: unknown): boolean {
    const normalizedId = this.normalizeSelectedId(id);
    return normalizedId ? this.selectedIdSet.has(normalizedId) : false;
  }

  selectCheckboxHint(assessment: Assessment | null | undefined): string {
    return assessment ? `Chọn mục ${assessment.code} · ${assessment.name}` : 'Chọn mục đánh giá';
  }

  addButtonHint(assessment: Assessment | null | undefined): string {
    return assessment ? `Thêm mục ${assessment.code} · ${assessment.name}` : 'Thêm mục đánh giá';
  }

  isExistingAssessment(assessment: Assessment | null | undefined): boolean {
    const code = this.normalizeCode(assessment?.code);
    return code ? this.existingCodeSet.has(code) : false;
  }

  latestGradeText(value: string | null | undefined): string {
    if (!value) {
      return '-';
    }
    return ASSESSMENT_GRADE_OPTIONS.find(item => item.value === value)?.text ?? value;
  }

  onSelectCheckboxChanged(id: unknown, event: { value?: boolean; event?: unknown }): void {
    if (!this.isSelectMode) {
      return;
    }
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

  onAddAssessmentClick(assessment: Assessment | null | undefined): void {
    if (!assessment || this.addDisabled || this.isExistingAssessment(assessment)) {
      return;
    }
    this.assessmentAdd.emit(assessment);
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

  async loadAssessmentsFromServer(): Promise<void> {
    if (this.loading) {
      return;
    }
    const requestedStudentId = this.normalizeOptionalId(this.studentId);
    this.loading = true;
    this.loadError = '';
    try {
      const loaded: Assessment[] = [];
      let page = 1;
      let totalPages = 1;
      do {
        const query: AssessmentListQuery = {
          page,
          pageSize: ASSESSMENT_CACHE_PAGE_SIZE,
          sortBy: 'rowindex',
          sortOrder: 'asc'
        };
        if (requestedStudentId) {
          query.studentId = requestedStudentId;
        }
        const result = await firstValueFrom(this.assessments.list(query));
        loaded.push(...result.items);
        totalPages = Math.max(1, result.pagination.totalPages || Math.ceil(result.pagination.totalItems / ASSESSMENT_CACHE_PAGE_SIZE));
        page += 1;
      } while (page <= totalPages);

      this.allAssessments = loaded;
      this.loadedStudentId = requestedStudentId;
      this.refreshGroupOptions();
      this.applyFilters();
      this.loadError = '';
    } catch (error) {
      const apiError = ApiError.from(error);
      this.loadError = this.withTrace(apiError);
      this.notifyError(apiError);
    } finally {
      this.loading = false;
      if (this.normalizeOptionalId(this.studentId) !== requestedStudentId) {
        void this.loadAssessmentsFromServer();
      }
    }
  }

  private refreshGroupOptions(): void {
    this.groupLv1DataSource = this.buildGroupOptions(
      1,
      this.allAssessments.map(assessment => assessment.groupLv1Name)
    );
    this.groupLv2DataSource = this.buildGroupOptions(
      2,
      this.allAssessments
        .filter(assessment => !this.groupLv1Name || assessment.groupLv1Name === this.groupLv1Name)
        .map(assessment => assessment.groupLv2Name)
    );
    this.groupLv3DataSource = this.buildGroupOptions(
      3,
      this.allAssessments
        .filter(assessment => !this.groupLv1Name || assessment.groupLv1Name === this.groupLv1Name)
        .filter(assessment => !this.groupLv2Name || assessment.groupLv2Name === this.groupLv2Name)
        .map(assessment => assessment.groupLv3Name)
    );
    this.groupLv1Placeholder = `Nhóm tuổi (${this.groupLv1DataSource.length})`;
    this.groupLv2Placeholder = `Nhóm 2 (${this.groupLv2DataSource.length})`;
    this.groupLv3Placeholder = `Nhóm 3 (${this.groupLv3DataSource.length})`;
  }

  private buildGroupOptions(level: number, values: string[]): AssessmentGroup[] {
    return Array.from(new Set(values.filter(value => value && value.trim())))
      .sort((left, right) => left.localeCompare(right, 'vi'))
      .map(name => ({ id: `${level}:${name}`, name, level }));
  }

  private matchesCurrentFilters(assessment: Assessment): boolean {
    return includesVietnamese([assessment.code, assessment.name], this.search)
      && (!this.groupLv1Name || assessment.groupLv1Name === this.groupLv1Name)
      && (!this.groupLv2Name || assessment.groupLv2Name === this.groupLv2Name)
      && (!this.groupLv3Name || assessment.groupLv3Name === this.groupLv3Name)
      && this.matchesLatestGradeFilter(assessment.latestGrade);
  }

  private matchesLatestGradeFilter(latestGrade: string | null | undefined): boolean {
    return this.latestGradeFilters.length === 0
      || this.latestGradeFilters.some(grade => grade === latestGrade || (grade === 'none' && !latestGrade));
  }

  private isInSelectedViewSnapshot(id: unknown): boolean {
    const normalizedId = this.normalizeSelectedId(id);
    return normalizedId ? this.selectedViewSnapshotIdSet.has(normalizedId) : false;
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

  private normalizeOptionalId(value: string | null | undefined): string | null {
    const id = value?.trim();
    return id || null;
  }

  private normalizeCode(value: string | null | undefined): string | null {
    const code = value?.trim().toLocaleLowerCase('vi');
    return code || null;
  }

  private refreshSelectedSet(): void {
    this.selectedIdSet = new Set(this.normalizeSelectedIds(this.selectedIds));
    this.grid?.instance.repaint();
  }

  private refreshExistingCodeSet(): void {
    this.existingCodeSet = new Set(
      this.existingCodes
        .map(code => this.normalizeCode(code))
        .filter((code): code is string => !!code)
    );
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
