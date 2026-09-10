import { Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, SimpleChanges, ViewChild } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import notify from 'devextreme/ui/notify';
import query from 'devextreme/data/query';
import { DxDataGridComponent } from 'devextreme-angular/ui/data-grid';
import { ApiError } from '../../core/models/api-error';
import { Assessment, AssessmentGroup, AssessmentListQuery } from '../../core/models/api.models';
import {
  AssessmentGrade,
  ASSESSMENT_GRADE_OPTIONS,
  assessmentGroupLv2Order,
  compareAssessmentByFixedGroupOrder
} from '../../core/models/api.models.assessment-sheets';
import { AssessmentsService } from '../../core/services/assessments.service';
import { patchGridBestFit } from '../../core/errors/dx-grid-bestfit-guard';
import { includesVietnamese } from '../../core/utils/vietnamese-search';

const SELECTED_ROW_CLASS = 'assessment-picker-selected-row';
const ASSESSMENT_CACHE_PAGE_SIZE = 100;
const LATEST_GRADE_NONE_LABEL = 'Chưa có';
type AssessmentPickerViewMode = 'all' | 'selected';
type AssessmentPickerMode = 'select' | 'add';
type LatestGradeFilterValue = AssessmentGrade | 'none';
type PlanMembershipFilterValue = 'existing' | 'missing';

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
  @Input() cachedAssessments: Assessment[] = [];
  @Input() addDisabled = false;
  @Output() selectedIdsChange = new EventEmitter<string[]>();
  @Output() assessmentAdd = new EventEmitter<Assessment>();
  @Output() assessmentsAdd = new EventEmitter<Assessment[]>();
  @Output() assessmentsRemove = new EventEmitter<Assessment[]>();
  @Output() assessmentRemove = new EventEmitter<Assessment>();

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
  private bulkAddToolbarButton?: { option: (options: Record<string, unknown>) => void };
  private bulkRemoveToolbarButton?: { option: (options: Record<string, unknown>) => void };
  private selectAllToolbarCheckBox?: { option: (options: Record<string, unknown>) => void };
  private membershipToolbarTagBox?: { option: (options: Record<string, unknown>) => void };
  readonly gridRemoteOperations = false;
  readonly gridDefaultPageSize = 50;
  readonly gridPageSizes = [20, 50, 100, 200, 1000, 2000];
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

  // Vòng lọc 2 (header/row filter của lưới). Cột "Kết quả gần nhất" hiển thị theo nhãn (null = "Chưa có")
  // để header filter gom nhóm được; mặc định chọn sẵn tất cả trừ "Đạt +".
  readonly latestGradeColumnCalculateCellValue = (row: Assessment): string =>
    row?.latestGrade
      ? (ASSESSMENT_GRADE_OPTIONS.find(option => option.value === row.latestGrade)?.text ?? row.latestGrade)
      : LATEST_GRADE_NONE_LABEL;
  readonly latestGradeColumnDefaultFilter: string[] = [
    LATEST_GRADE_NONE_LABEL,
    ...ASSESSMENT_GRADE_OPTIONS.filter(option => option.value !== 'A').map(option => option.text)
  ];
  readonly planMembershipFilterOptions: Array<{ value: PlanMembershipFilterValue; text: string }> = [
    { value: 'existing', text: 'Trong KH' },
    { value: 'missing', text: 'Chưa có trong KH' }
  ];
  membershipFilterValues: PlanMembershipFilterValue[] = ['existing', 'missing'];
  // Header filter cột "Nhóm 2" giữ đúng thứ tự cố định của nhóm Lv2 thay vì abc.
  readonly groupLv2HeaderFilter = {
    dataSource: (data: { dataSource: { postProcess?: (items: Array<{ value?: string }>) => Array<{ value?: string }> } }): void => {
      data.dataSource.postProcess = (items) =>
        [...items].sort((left, right) => assessmentGroupLv2Order(left.value) - assessmentGroupLv2Order(right.value));
    }
  };

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
    if (changes['cachedAssessments'] && this.cachedAssessments.length > 0) {
      this.useCachedAssessments(this.cachedAssessments);
    }
    if (changes['mode']) {
      this.syncSelectionToolbar();
    }
    if (changes['addDisabled']) {
      this.syncSelectionToolbar();
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
    if (this.cachedAssessments.length > 0) {
      this.useCachedAssessments(this.cachedAssessments);
    } else {
      void this.loadAssessmentsFromServer();
    }
  }

  ngOnDestroy(): void {
    if (this.searchTimer !== undefined) {
      window.clearTimeout(this.searchTimer);
    }
    this.bulkAddToolbarButton = undefined;
    this.bulkRemoveToolbarButton = undefined;
    this.selectAllToolbarCheckBox = undefined;
    this.membershipToolbarTagBox = undefined;
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

  get selectedAddCount(): number {
    return this.isAddMode ? this.getSelectedAssessments().filter(item => !this.isExistingAssessment(item)).length : 0;
  }

  get selectedRemoveCount(): number {
    return this.isAddMode ? this.getSelectedAssessments().filter(item => this.isExistingAssessment(item)).length : 0;
  }

  get bulkAddText(): string {
    return `Thêm các mục đã chọn (${this.selectedAddCount})`;
  }

  get bulkAddDisabled(): boolean {
    return this.addDisabled || this.selectedAddCount === 0;
  }

  get bulkRemoveText(): string {
    return `Bỏ các mục đã chọn (${this.selectedRemoveCount})`;
  }

  get bulkRemoveDisabled(): boolean {
    return this.addDisabled || this.selectedRemoveCount === 0;
  }

  get selectAllFilteredValue(): boolean | null {
    const state = this.filteredSelectionState();
    if (state.selectedCount === 0) {
      return false;
    }
    return state.selectedCount === state.totalCount ? true : null;
  }

  get selectAllFilteredText(): string {
    const state = this.filteredSelectionState();
    return `Chọn tất cả (${state.selectedCount}/${state.totalCount})`;
  }

  get selectAllFilteredHint(): string {
    const state = this.filteredSelectionState();
    return state.totalCount > 0 && state.selectedCount === state.totalCount
      ? `Bỏ chọn ${state.totalCount} mục đang khớp bộ lọc`
      : `Chọn tất cả ${state.totalCount} mục đang khớp bộ lọc`;
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
    // Thứ tự mặc định của lưới: nhóm Lv2 cố định → rowIndex (giữ như hiện tại) → mã (G3).
    this.filteredAssessments = source
      .filter(assessment => this.matchesCurrentFilters(assessment))
      .sort(compareAssessmentByFixedGroupOrder);
    const grid = this.grid?.instance as unknown as { pageIndex?: (value: number) => void; repaint?: () => void } | undefined;
    grid?.pageIndex?.(0);
    grid?.repaint?.();
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
    this.membershipFilterValues = ['existing', 'missing'];
    this.syncMembershipToolbar();
    this.resetGridFilters();
    this.refreshGroupOptions();
    this.applyFilters();
  }

  // Đưa filter row + header filter của lưới (vòng 2) về mặc định: xóa hết, riêng cột
  // "Kết quả gần nhất" đặt lại mặc định "tất cả trừ Đạt +" (gồm "Chưa có").
  resetGridFilters(): void {
    const grid = this.grid?.instance;
    if (!grid) {
      return;
    }
    grid.clearFilter('row');
    grid.clearFilter('header');
    grid.columnOption('latestGrade', 'filterValues', [...this.latestGradeColumnDefaultFilter]);
  }

  // Toolbar của lưới picker (DevExtreme 19.2 chưa có option `toolbar` khai báo được, dùng event này):
  // giữ nút "Chọn cột" mặc định, thêm nút bulk-add (ở add-mode) và "Đặt lại lọc lưới" ở bên trái.
  onToolbarPreparing(event: { toolbarOptions?: { items?: any[] } }): void {
    const items = event.toolbarOptions?.items;
    if (!items) {
      return;
    }
    const customItems: any[] = [];
    if (this.isAddMode) {
      customItems.push({
        location: 'before',
        widget: 'dxCheckBox',
        options: {
          value: this.selectAllFilteredValue,
          text: this.selectAllFilteredText,
          hint: this.selectAllFilteredHint,
          disabled: this.addDisabled || this.filteredSelectionState().totalCount === 0,
          elementAttr: { 'aria-label': this.selectAllFilteredHint },
          onInitialized: (e: { component?: unknown }) => {
            this.selectAllToolbarCheckBox = e.component as { option: (options: Record<string, unknown>) => void };
            this.syncSelectAllToolbar();
          },
          onValueChanged: (e: { event?: unknown }) => this.onSelectAllFilteredChanged(e)
        }
      }, {
        location: 'before',
        widget: 'dxTagBox',
        options: {
          dataSource: this.planMembershipFilterOptions,
          valueExpr: 'value',
          displayExpr: 'text',
          value: [...this.membershipFilterValues],
          width: 260,
          showSelectionControls: true,
          placeholder: 'Lọc trạng thái kế hoạch',
          inputAttr: { 'aria-label': 'Lọc mục đánh giá theo trạng thái trong kế hoạch' },
          onInitialized: (e: { component?: unknown }) => {
            this.membershipToolbarTagBox = e.component as { option: (options: Record<string, unknown>) => void };
            this.syncMembershipToolbar();
          },
          onValueChanged: (e: { value?: PlanMembershipFilterValue[]; previousValue?: PlanMembershipFilterValue[]; event?: unknown }) =>
            this.onMembershipFilterChanged(e)
        }
      }, {
        location: 'before',
        widget: 'dxButton',
        options: {
          icon: 'add',
          text: this.bulkAddText,
          type: 'default',
          hint: 'Thêm tất cả mục đang chọn vào bảng đánh giá trong một lần lưu',
          disabled: this.bulkAddDisabled,
          onInitialized: (e: { component?: unknown }) => {
            this.bulkAddToolbarButton = e.component as { option: (options: Record<string, unknown>) => void };
            this.syncBulkAddToolbar();
          },
          onClick: () => this.onAddSelectedAssessmentsClick()
        }
      }, {
        location: 'before',
        widget: 'dxButton',
        options: {
          icon: 'trash',
          text: this.bulkRemoveText,
          type: 'danger',
          stylingMode: 'outlined',
          hint: 'Bỏ tất cả mục đã chọn đang có trong kế hoạch bằng một lần lưu',
          disabled: this.bulkRemoveDisabled,
          onInitialized: (e: { component?: unknown }) => {
            this.bulkRemoveToolbarButton = e.component as { option: (options: Record<string, unknown>) => void };
            this.syncBulkRemoveToolbar();
          },
          onClick: () => this.onRemoveSelectedAssessmentsClick()
        }
      });
    }
    customItems.push({
      location: 'before',
      widget: 'dxButton',
      options: {
        icon: 'clearformat',
        text: 'Đặt lại lọc lưới',
        hint: 'Đưa filter row + header filter về mặc định (Kết quả: trừ Đạt +)',
        stylingMode: 'outlined',
        onClick: () => this.resetGridFilters()
      }
    });
    items.unshift(...customItems);
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
    if (assessment && this.isAddMode && this.isExistingAssessment(assessment)) {
      return `Chọn mục ${assessment.code} · ${assessment.name} để bỏ khỏi kế hoạch`;
    }
    return assessment ? `Chọn mục ${assessment.code} · ${assessment.name}` : 'Chọn mục đánh giá';
  }

  addButtonHint(assessment: Assessment | null | undefined): string {
    return assessment ? `Thêm mục ${assessment.code} · ${assessment.name}` : 'Thêm mục đánh giá';
  }

  removeButtonHint(assessment: Assessment | null | undefined): string {
    return assessment ? `Xóa mục ${assessment.code} · ${assessment.name}` : 'Xóa mục đánh giá';
  }

  isExistingAssessment(assessment: Assessment | null | undefined): boolean {
    const code = this.normalizeCode(assessment?.code);
    return code ? this.existingCodeSet.has(code) : false;
  }

  isAddSelectionDisabled(assessment: Assessment | null | undefined): boolean {
    return this.isAddMode && this.addDisabled;
  }

  latestGradeText(value: string | null | undefined): string {
    if (!value) {
      return '-';
    }
    return ASSESSMENT_GRADE_OPTIONS.find(item => item.value === value)?.text ?? value;
  }

  onSelectCheckboxChanged(assessmentOrId: Assessment | unknown, event: { value?: boolean; event?: unknown }): void {
    if (!event.event) {
      return;
    }
    const assessment = typeof assessmentOrId === 'object' && assessmentOrId !== null
      ? assessmentOrId as Assessment
      : this.allAssessments.find(item => item.id === this.normalizeSelectedId(assessmentOrId));
    if (this.isAddSelectionDisabled(assessment)) {
      return;
    }
    const id = assessment?.id ?? assessmentOrId;
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

  onAddSelectedAssessmentsClick(): void {
    if (this.bulkAddDisabled) {
      return;
    }
    const selectedAssessments = this.getSelectedAssessments().filter(item => !this.isExistingAssessment(item));
    if (selectedAssessments.length > 0) {
      this.assessmentsAdd.emit(selectedAssessments);
    }
  }

  onRemoveSelectedAssessmentsClick(): void {
    if (this.bulkRemoveDisabled) {
      return;
    }
    const selectedAssessments = this.getSelectedAssessments().filter(item => this.isExistingAssessment(item));
    if (selectedAssessments.length > 0) {
      this.assessmentsRemove.emit(selectedAssessments);
    }
  }

  clearBulkSelection(): void {
    this.emitSelectedIds(new Set<string>());
  }

  onSelectAllFilteredChanged(event: { event?: unknown }): void {
    if (!event.event || this.addDisabled) {
      return;
    }
    this.setAllFilteredSelected(this.selectAllFilteredValue !== true);
  }

  onMembershipFilterChanged(event: {
    value?: PlanMembershipFilterValue[];
    previousValue?: PlanMembershipFilterValue[];
    event?: unknown;
  }): void {
    if (!event.event) {
      return;
    }
    const next = this.normalizeMembershipFilterValues(event.value ?? []);
    this.membershipFilterValues = next;
    this.applyFilters();
    this.syncMembershipToolbar();
  }

  setAllFilteredSelected(selected: boolean): void {
    const next = new Set(this.selectedIdSet);
    this.getFilteredSelectableAssessments().forEach(assessment => {
      const id = this.normalizeSelectedId(assessment.id);
      if (!id) {
        return;
      }
      if (selected) {
        next.add(id);
      } else {
        next.delete(id);
      }
    });
    this.emitSelectedIds(next);
  }

  onRemoveAssessmentClick(assessment: Assessment | null | undefined): void {
    if (!assessment || this.addDisabled || !this.isExistingAssessment(assessment)) {
      return;
    }
    this.assessmentRemove.emit(assessment);
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
    const assessmentById = new Map(this.allAssessments.map(assessment => [assessment.id, assessment]));
    if (selected) {
      this.visibleAssessmentIds.forEach(id => {
        if (!this.isAddSelectionDisabled(assessmentById.get(id))) {
          next.add(id);
        }
      });
    } else {
      this.visibleAssessmentIds.forEach(id => next.delete(id));
    }
    this.emitSelectedIds(next);
  }

  onContentReady(): void {
    this.refreshVisibleAssessmentIds();
    this.syncSelectAllToolbar();
    // DevExtreme 19.2: `_synchronizeColumns`/`_toggleBestFitMode` (trên ResizingController) có thể chạy
    // sau khi grid picker bị hủy (đóng picker / rời form) → null-css. Vá trên đúng controller `resizing`.
    if (!this.gridBestFitGuarded && patchGridBestFit(this.grid?.instance, '[AssessmentPicker]')) {
      this.gridBestFitGuarded = true;
    }
  }

  private gridBestFitGuarded = false;

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
      this.syncSelectionToolbar();
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
    const names = Array.from(new Set(values.filter(value => value && value.trim())));
    // Nhóm Lv2 theo thứ tự cố định (G3); Lv1/Lv3 giữ abc.
    names.sort(level === 2
      ? (left, right) => assessmentGroupLv2Order(left) - assessmentGroupLv2Order(right) || left.localeCompare(right, 'vi')
      : (left, right) => left.localeCompare(right, 'vi'));
    return names.map(name => ({ id: `${level}:${name}`, name, level }));
  }

  private matchesCurrentFilters(assessment: Assessment): boolean {
    return includesVietnamese([assessment.code, assessment.name], this.search)
      && (!this.groupLv1Name || assessment.groupLv1Name === this.groupLv1Name)
      && (!this.groupLv2Name || assessment.groupLv2Name === this.groupLv2Name)
      && (!this.groupLv3Name || assessment.groupLv3Name === this.groupLv3Name)
      && this.matchesLatestGradeFilter(assessment.latestGrade)
      && this.matchesMembershipFilter(assessment);
  }

  private matchesMembershipFilter(assessment: Assessment): boolean {
    return this.membershipFilterValues.length === this.planMembershipFilterOptions.length
      || (this.membershipFilterValues.includes('existing') && this.isExistingAssessment(assessment))
      || (this.membershipFilterValues.includes('missing') && !this.isExistingAssessment(assessment));
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
    this.syncSelectionToolbar();
  }

  private refreshExistingCodeSet(): void {
    this.existingCodeSet = new Set(
      this.existingCodes
        .map(code => this.normalizeCode(code))
        .filter((code): code is string => !!code)
    );
    this.applyFilters();
    this.syncSelectionToolbar();
  }

  private emitSelectedIds(selectedIdSet: Set<string>): void {
    const selectedIds = Array.from(selectedIdSet);
    this.selectedIds = selectedIds;
    this.selectedIdSet = selectedIdSet;
    this.selectedIdsChange.emit(selectedIds);
    this.grid?.instance.repaint();
    this.syncSelectionToolbar();
  }

  onGridOptionChanged(event: { name?: string; fullName?: string }): void {
    const optionName = event.fullName ?? event.name ?? '';
    if (event.name === 'filterValue' || /filterValue|filterValues|selectedFilterOperation|searchPanel\.text/.test(optionName)) {
      this.syncSelectAllToolbar();
    }
  }

  private syncBulkAddToolbar(): void {
    this.bulkAddToolbarButton?.option({
      text: this.bulkAddText,
      disabled: this.bulkAddDisabled
    });
  }

  private syncBulkRemoveToolbar(): void {
    this.bulkRemoveToolbarButton?.option({
      text: this.bulkRemoveText,
      disabled: this.bulkRemoveDisabled
    });
  }

  private syncSelectAllToolbar(): void {
    const state = this.filteredSelectionState();
    const value = state.selectedCount === 0
      ? false
      : state.selectedCount === state.totalCount ? true : null;
    const hint = state.totalCount > 0 && state.selectedCount === state.totalCount
      ? `Bỏ chọn ${state.totalCount} mục đang khớp bộ lọc`
      : `Chọn tất cả ${state.totalCount} mục đang khớp bộ lọc`;
    this.selectAllToolbarCheckBox?.option({
      value,
      text: `Chọn tất cả (${state.selectedCount}/${state.totalCount})`,
      hint,
      disabled: this.addDisabled || state.totalCount === 0,
      elementAttr: { 'aria-label': hint }
    });
  }

  private syncSelectionToolbar(): void {
    this.syncBulkAddToolbar();
    this.syncBulkRemoveToolbar();
    this.syncSelectAllToolbar();
    this.syncMembershipToolbar();
  }

  private syncMembershipToolbar(): void {
    this.membershipToolbarTagBox?.option({ value: [...this.membershipFilterValues] });
  }

  private normalizeMembershipFilterValues(values: PlanMembershipFilterValue[]): PlanMembershipFilterValue[] {
    const unique = Array.from(new Set(values.filter(value => value === 'existing' || value === 'missing')));
    return unique.length === 1 ? unique : ['existing', 'missing'];
  }

  private filteredSelectionState(): { selectedCount: number; totalCount: number } {
    const assessments = this.getFilteredSelectableAssessments();
    return {
      selectedCount: assessments.filter(assessment => this.selectedIdSet.has(assessment.id)).length,
      totalCount: assessments.length
    };
  }

  private getFilteredSelectableAssessments(): Assessment[] {
    const filter = (this.grid?.instance as unknown as { getCombinedFilter?: () => unknown } | undefined)
      ?.getCombinedFilter?.();
    const matching = filter
      ? query(this.filteredAssessments).filter(filter as any).toArray() as Assessment[]
      : this.filteredAssessments;
    return matching.filter(assessment => !this.isAddSelectionDisabled(assessment));
  }

  private useCachedAssessments(assessments: Assessment[]): void {
    this.allAssessments = [...assessments];
    this.loadedStudentId = this.normalizeOptionalId(this.studentId);
    this.refreshGroupOptions();
    this.applyFilters();
    this.syncSelectionToolbar();
    this.loadError = '';
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
