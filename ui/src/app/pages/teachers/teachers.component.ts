import { CommonModule } from '@angular/common';
import { Component, OnDestroy, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import CustomStore from 'devextreme/data/custom_store';
import { confirm } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';
import { DxDataGridComponent } from 'devextreme-angular/ui/data-grid';
import { ApiError } from '../../core/models/api-error';
import { StudentGroup, Teacher, UserStatus } from '../../core/models/api.models';
import { asLegacyWidgetDataSource } from '../../core/models/devextreme-legacy.types';
import { USER_STATUS_LABELS } from '../../core/i18n/ui-labels';
import { StudentGroupsService } from '../../core/services/student-groups.service';
import { TeachersService } from '../../core/services/teachers.service';

const TEACHER_SORT_FIELDS = new Set([
  'teacherCode', 'fullName', 'email', 'status', 'attendanceEditWindowDays',
  'responsibleGroupCount', 'createdAt', 'updatedAt'
]);

@Component({
  selector: 'app-teachers',
  templateUrl: './teachers.component.html',
  styleUrls: ['./teachers.component.scss']
})
export class TeachersComponent implements OnDestroy {
  @ViewChild(DxDataGridComponent) grid?: DxDataGridComponent;

  readonly statuses = [
    { value: 'Active', text: USER_STATUS_LABELS.Active },
    { value: 'Inactive', text: USER_STATUS_LABELS.Inactive },
    { value: 'Locked', text: USER_STATUS_LABELS.Locked }
  ];
  readonly rowButtons = [
    { hint: 'Xem chi tiết', icon: 'eyeopen', onClick: (event: any) => this.openDetail(event.row.data as Teacher) },
    { hint: 'Chỉnh sửa', icon: 'edit', onClick: (event: any) => this.openEdit(event.row.data as Teacher) },
    { hint: 'Đổi mật khẩu', icon: 'key', onClick: (event: any) => this.openPassword(event.row.data as Teacher) },
    { hint: 'Xóa giáo viên', icon: 'trash', onClick: (event: any) => this.remove(event.row.data as Teacher) }
  ];

  search = '';
  statusFilter: UserStatus | null = null;
  groupId: string | null = null;
  unassigned = false;
  filtersExpanded = true;
  loadError = '';
  deletingId: string | null = null;
  passwordVisible = false;
  passwordTeacher: Teacher | null = null;
  private searchTimer?: number;

  readonly groupDataSource = asLegacyWidgetDataSource(new CustomStore({
    key: 'id',
    byKey: key => firstValueFrom(this.groups.get(String(key))),
    load: options => {
      const pageSize = Math.min(options.take ?? 20, 100);
      return firstValueFrom(this.groups.list({
        page: Math.floor((options.skip ?? 0) / pageSize) + 1,
        pageSize,
        search: typeof options.searchValue === 'string' ? options.searchValue.trim() || undefined : undefined,
        status: 'Active',
        sortBy: 'name',
        sortOrder: 'asc'
      })).then(result => ({ data: result.items, totalCount: result.pagination.totalItems }));
    }
  }));

  readonly dataSource = asLegacyWidgetDataSource(new CustomStore({
    key: 'id',
    load: options => {
      const pageSize = Math.min(options.take ?? 20, 100);
      const sort = this.readSort(options.sort);
      return firstValueFrom(this.teachers.list({
        page: Math.floor((options.skip ?? 0) / pageSize) + 1,
        pageSize,
        search: this.search.trim() || undefined,
        status: this.statusFilter ?? undefined,
        groupId: this.groupId ?? undefined,
        unassigned: this.unassigned ? true : undefined,
        sortBy: sort.field,
        sortOrder: sort.order
      })).then(result => {
        this.loadError = '';
        return { data: result.items, totalCount: result.pagination.totalItems };
      }).catch(error => this.rejectLoad(error));
    }
  }));

  readonly groupDisplay = (group: StudentGroup | null): string => group ? `${group.code} · ${group.name}` : '';

  constructor(
    private readonly teachers: TeachersService,
    private readonly groups: StudentGroupsService,
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
    this.statusFilter = null;
    this.groupId = null;
    this.unassigned = false;
    this.applyFilters();
  }

  onGroupChanged(): void {
    if (this.groupId) {
      this.unassigned = false;
    }
  }

  onUnassignedChanged(): void {
    if (this.unassigned) {
      this.groupId = null;
    }
  }

  openDetail(teacher: Teacher): void {
    void this.router.navigate(['/teachers', teacher.id]);
  }

  openEdit(teacher: Teacher): void {
    void this.router.navigate(['/teachers', teacher.id, 'edit']);
  }

  openPassword(teacher: Teacher): void {
    this.passwordTeacher = teacher;
    this.passwordVisible = true;
  }

  async remove(teacher: Teacher): Promise<void> {
    if (this.deletingId) {
      return;
    }
    const accepted = await confirm(
      `Xóa giáo viên “${teacher.fullName}” (${teacher.teacherCode})? Tài khoản sẽ bị vô hiệu hóa và các phiên đăng nhập sẽ bị thu hồi.`,
      'Xác nhận xóa giáo viên'
    );
    if (!accepted) {
      return;
    }

    this.deletingId = teacher.id;
    try {
      await firstValueFrom(this.teachers.delete(teacher.id, teacher.version));
      notify('Đã xóa giáo viên và thu hồi các phiên đăng nhập.', 'success', 2400);
      await this.grid?.instance.refresh();
    } catch (error) {
      const apiError = ApiError.from(error);
      this.notifyError(apiError);
      if (apiError.code === 'TeacherVersionConflict') {
        await this.grid?.instance.refresh();
      }
    } finally {
      this.deletingId = null;
    }
  }

  statusText(status: UserStatus): string {
    return USER_STATUS_LABELS[status];
  }

  private readSort(sortValue: unknown): { field: string; order: 'asc' | 'desc' } {
    const sort = Array.isArray(sortValue) ? sortValue[0] : sortValue;
    const config = sort && typeof sort === 'object' ? sort as { selector?: unknown; desc?: boolean } : undefined;
    const requested = typeof config?.selector === 'string' ? config.selector : 'fullName';
    return {
      field: TEACHER_SORT_FIELDS.has(requested) ? requested : 'fullName',
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
}
