import { CommonModule } from '@angular/common';
import { Component, OnDestroy, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import CustomStore from 'devextreme/data/custom_store';
import { confirm, custom } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';
import { DxDataGridComponent } from 'devextreme-angular/ui/data-grid';
import { ApiError } from '../../core/models/api-error';
import { AssessmentGroup, Assessment } from '../../core/models/api.models';
import { asLegacyWidgetDataSource, LegacyWidgetDataSource } from '../../core/models/devextreme-legacy.types';
import { USER_STATUS_LABELS } from '../../core/i18n/ui-labels';
import { AssessmentGroupsService } from '../../core/services/assessment-groups.service';
import { AssessmentsService } from '../../core/services/assessments.service';
import { GoogleSheetsService } from 'src/app/core/services/google-sheets.service';

const ASSESSMENT_SORT_FIELDS = new Set([
  'code', 'name', 'rowindex'
]);

@Component({
  selector: 'app-assessments',
  templateUrl: './assessments.component.html',
  styleUrls: ['./assessments.component.scss']
})
export class AssessmentsComponent implements OnDestroy {
  @ViewChild(DxDataGridComponent) grid?: DxDataGridComponent;
  search = '';
  saving = false;
  groupLv1Name: string | null = null;
  groupLv2Name: string | null = null;
  groupLv3Name: string | null = null;
  groupLv1Placeholder: string = 'Nhóm tuổi';
  groupLv2Placeholder: string = 'Nhóm 2';
  groupLv3Placeholder: string = 'Nhóm 3';
  filtersExpanded = true;
  loadError = '';
  private searchTimer?: number;
  groupLv2DataSource: LegacyWidgetDataSource | [] = [];
  groupLv3DataSource: LegacyWidgetDataSource | [] = [];

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
      })
        .catch(error => {
          this.groupLv1Placeholder = 'Nhóm tuổi: Lỗi tải dữ liệu';
          return this.rejectLoad(error)
        });
    }
  }));
  private loadLv2DataSouce(): void {
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
        })
          .catch(error => {
            this.groupLv2Placeholder = 'Nhóm 2: Lỗi tải dữ liệu';
            return this.rejectLoad(error)
          });
      }
    }));
  }
  private loadLv3DataSouce(): void {
    console.log({ lv1: this.groupLv1Name, lv2: this.groupLv2Name })
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
        }))
          .then(result => {
            this.groupLv3Placeholder = `Nhóm 3 (${result.pagination.totalItems})`;
            return { data: result.items, totalCount: result.pagination.totalItems };
          })
          .catch(error => {
            this.groupLv3Placeholder = 'Nhóm 3: Lỗi tải dữ liệu';
            return this.rejectLoad(error)
          })
          ;
      }
    }));
  }

  readonly dataSource = asLegacyWidgetDataSource(new CustomStore({
    key: 'id',
    load: options => {
      const pageSize = Math.min(options.take ?? 5000, 5000);
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
    private readonly googleSheet: GoogleSheetsService,
    public readonly router: Router
  ) { }
  ngOnInit(): void {
    this.loadLv2DataSouce();
    this.loadLv3DataSouce();

  }
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
    this.groupLv1Name = null;
    this.groupLv2Name = null;
    this.groupLv3Name = null;
    this.applyFilters();
  }

  private isColumnChooserOpen(): boolean {
    if (this.grid && this.grid?.instance) {
      const gridInstance: any = this.grid?.instance;
      // 1. Lấy controller quản lý Column Chooser của v19.2
      const columnChooserController = gridInstance.getController('columnChooser');
      const isShow = columnChooserController?.component?._views?.columnChooserView?._popupContainer?._options?.visible
      return isShow;
    }
    return false; // Mặc định là đang đóng nếu chưa load xong lưới
  }
  showDialogColumnChooser():void {
    this.grid?.instance.option('columnChooser.mode', 'select');
    const currentState = this.isColumnChooserOpen();
    console.log(currentState);
    if(!currentState) this.grid?.instance.showColumnChooser();
    else this.grid?.instance.hideColumnChooser();
  }

  onGroupLv1Changed(): void {
    this.groupLv2Name = null;
    this.groupLv3Name = null;
    this.loadLv2DataSouce();
    this.loadLv3DataSouce();
  }

  onGroupLv2Changed(): void {
    this.groupLv3Name = null;
    this.loadLv3DataSouce();
  }

  onGroupLv3Changed(): void {
  }


  private readSort(sortValue: unknown): { field: string; order: 'asc' | 'desc' } {
    const sort = Array.isArray(sortValue) ? sortValue[0] : sortValue;
    const config = sort && typeof sort === 'object' ? sort as { selector?: unknown; desc?: boolean } : undefined;
    const requested = typeof config?.selector === 'string' ? config.selector : 'rowindex';
    return {
      field: ASSESSMENT_SORT_FIELDS.has(requested) ? requested : 'rowindex',
      order: config?.desc ? 'desc' : 'asc'
    };
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

  get saveDisabled(): boolean {
    if (this.saving) return true;
    return false;
  }

  get saveButtonText(): string {
    return this.saving ? 'Đang đồng bộ…' : 'Đồng bộ GGSheet';
  }
  async syncAssessmentsFromGGSheet(): Promise<void> {
    this.saving = true;
    this.loadError = '';
    try {
      const resultConfirm = await custom({
        title: "Xác nhận",
        messageHtml: "<i>Yêu cầu đồng bộ dữ liệu từ Google Sheets?</i>",
        buttons: [
          {
            text: "Không",
            onClick: () => false,
            focusStateEnabled: true,
            elementAttr: { class: "dx-button-focused" }
          },
          {
            text: "Có",
            onClick: () => true,
            elementAttr: { class: "dx-button-danger" }
          }
        ]
      }).show();
      if (resultConfirm) {
        const result = await firstValueFrom(this.googleSheet.syncFromGoogleSheets({}));
        this.retryLoad()
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
}
