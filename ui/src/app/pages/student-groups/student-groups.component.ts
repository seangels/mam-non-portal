import { CommonModule } from '@angular/common';
import { Component, NgModule, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import CustomStore from 'devextreme/data/custom_store';
import { confirm } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxCheckBoxModule } from 'devextreme-angular/ui/check-box';
import { DxDataGridComponent, DxDataGridModule } from 'devextreme-angular/ui/data-grid';
import { DxFormModule } from 'devextreme-angular/ui/form';
import { DxLoadIndicatorModule } from 'devextreme-angular/ui/load-indicator';
import { DxPopupModule } from 'devextreme-angular/ui/popup';
import { DxSelectBoxModule } from 'devextreme-angular/ui/select-box';
import { DxTabsModule } from 'devextreme-angular/ui/tabs';
import { DxTextBoxModule } from 'devextreme-angular/ui/text-box';
import { ApiError } from '../../core/models/api-error';
import {
  Student,
  StudentGroup,
  StudentGroupStatus,
  Teacher,
  UserStatus
} from '../../core/models/api.models';
import { STUDENT_STATUS_LABELS, USER_STATUS_LABELS } from '../../core/i18n/ui-labels';
import { StudentGroupsService } from '../../core/services/student-groups.service';
import { StudentsService } from '../../core/services/students.service';
import { TeachersService } from '../../core/services/teachers.service';
import { includesVietnamese } from '../../core/utils/vietnamese-search';

interface GroupEditor {
  id?: string;
  code: string;
  name: string;
  status: StudentGroupStatus;
}

@Component({
  selector: 'app-student-groups',
  templateUrl: './student-groups.component.html',
  styleUrls: ['./student-groups.component.scss']
})
export class StudentGroupsComponent implements OnInit {
  @ViewChild('groupsGrid') groupsGrid?: DxDataGridComponent;
  @ViewChild('teachersGrid') teachersGrid?: DxDataGridComponent;

  readonly tabs = [{ text: 'Nhóm học sinh' }, { text: 'Chính sách giáo viên' }];
  readonly studentStatusLabels = STUDENT_STATUS_LABELS;
  readonly groupStatuses = [
    { value: 'Active', text: 'Đang hoạt động' },
    { value: 'Inactive', text: 'Ngừng hoạt động' }
  ];
  readonly teacherStatuses = [
    { value: 'Active', text: USER_STATUS_LABELS.Active },
    { value: 'Inactive', text: USER_STATUS_LABELS.Inactive },
    { value: 'Locked', text: USER_STATUS_LABELS.Locked }
  ];
  readonly groupButtons = [
    { hint: 'Quản lý học sinh', icon: 'group', onClick: (event: any) => this.openRoster(event.row.data as StudentGroup) },
    { hint: 'Phân công giáo viên', icon: 'user', onClick: (event: any) => this.openTeacherAssignment(event.row.data as StudentGroup) },
    { hint: 'Chỉnh sửa nhóm', icon: 'edit', onClick: (event: any) => this.openGroupEdit(event.row.data as StudentGroup) },
    { hint: 'Xóa nhóm', icon: 'trash', onClick: (event: any) => this.removeGroup(event.row.data as StudentGroup) }
  ];
  readonly teacherButtons = [
    { hint: 'Cấu hình thời hạn sửa', icon: 'clock', onClick: (event: any) => this.openPolicy(event.row.data as Teacher) }
  ];

  selectedTab = 0;
  groupSearch = '';
  groupStatus: StudentGroupStatus | null = null;
  groupsWithoutTeacher: boolean | null | undefined = false;
  teacherSearch = '';
  teacherStatus: UserStatus | null = null;
  teachersWithoutGroup: boolean | null | undefined = false;
  saving = false;

  groupEditorVisible = false;
  isEditingGroup = false;
  groupEditor: GroupEditor = this.emptyGroup();

  assignmentVisible = false;
  selectedGroup: StudentGroup | null = null;
  selectedTeacherId: string | null = null;

  rosterVisible = false;
  rosterLoading = false;
  rosterSearch = '';
  roster: Student[] = [];
  selectedStudentId: string | null = null;
  rosterConflict = '';

  policyVisible = false;
  selectedTeacher: Teacher | null = null;
  policyEditor = { attendanceEditWindowDays: 7, expectedVersion: 1 };
  policyConflict = '';

  readonly groupDataSource = new CustomStore({
    key: 'id',
    load: options => {
      const pageSize = Math.min(options.take ?? 20, 100);
      const sort = this.readSort(options.sort, 'code');
      return firstValueFrom(this.groups.list({
        page: Math.floor((options.skip ?? 0) / pageSize) + 1,
        pageSize,
        search: this.groupSearch.trim() || undefined,
        status: this.groupStatus ?? undefined,
        unassigned: this.groupsWithoutTeacher ? true : undefined,
        sortBy: sort.field,
        sortOrder: sort.order
      })).then(result => ({ data: result.items, totalCount: result.pagination.totalItems }))
        .catch(error => this.rejectLoad(error));
    }
  });

  readonly teacherDataSource = new CustomStore({
    key: 'id',
    load: options => {
      const pageSize = Math.min(options.take ?? 20, 100);
      const sort = this.readSort(options.sort, 'fullName');
      return firstValueFrom(this.teachers.list({
        page: Math.floor((options.skip ?? 0) / pageSize) + 1,
        pageSize,
        search: this.teacherSearch.trim() || undefined,
        status: this.teacherStatus ?? undefined,
        unassigned: this.teachersWithoutGroup ? true : undefined,
        sortBy: sort.field,
        sortOrder: sort.order
      })).then(result => ({ data: result.items, totalCount: result.pagination.totalItems }))
        .catch(error => this.rejectLoad(error));
    }
  });

  readonly teacherPicker = new CustomStore({
    key: 'id',
    byKey: key => firstValueFrom(this.teachers.get(String(key))),
    load: options => firstValueFrom(this.teachers.list({
      page: Math.floor((options.skip ?? 0) / Math.min(options.take ?? 20, 100)) + 1,
      pageSize: Math.min(options.take ?? 20, 100),
      search: typeof options.searchValue === 'string' ? options.searchValue : undefined,
      status: 'Active',
      sortBy: 'fullName',
      sortOrder: 'asc'
    })).then(result => ({ data: result.items, totalCount: result.pagination.totalItems }))
  });

  readonly studentPicker = new CustomStore({
    key: 'id',
    byKey: key => firstValueFrom(this.students.get(String(key))),
    load: options => firstValueFrom(this.students.list({
      page: Math.floor((options.skip ?? 0) / Math.min(options.take ?? 20, 100)) + 1,
      pageSize: Math.min(options.take ?? 20, 100),
      search: typeof options.searchValue === 'string' ? options.searchValue : undefined,
      status: 'Active',
      sortBy: 'fullName',
      sortOrder: 'asc'
    })).then(result => ({ data: result.items, totalCount: result.pagination.totalItems }))
  });

  get groupEditorTitle(): string {
    return this.isEditingGroup ? 'Cập nhật nhóm' : 'Thêm nhóm';
  }

  get filteredRoster(): Student[] {
    return this.roster.filter(student => includesVietnamese(
      [student.studentCode, student.fullName, student.nickName],
      this.rosterSearch
    ));
  }

  get groupIsFull(): boolean {
    return (this.selectedGroup?.studentCount ?? 0) >= 100;
  }

  readonly teacherDisplay = (teacher: Teacher | null) => teacher?.fullName ?? '';
  readonly studentDisplay = (student: Student | null) => student
    ? `${student.fullName} (${student.studentCode})${student.groupName ? ` · ${student.groupName}` : ' · Chưa phân nhóm'}`
    : '';

  constructor(
    private readonly groups: StudentGroupsService,
    private readonly teachers: TeachersService,
    private readonly students: StudentsService,
    private readonly route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    if (this.route.snapshot.queryParamMap.get('tab') === 'policy') {
      this.selectedTab = 1;
      this.teacherSearch = this.route.snapshot.queryParamMap.get('search') ?? '';
    }
  }

  applyGroupFilters(): void {
    this.groupsGrid?.instance.pageIndex(0);
    void this.groupsGrid?.instance.refresh();
  }

  applyTeacherFilters(): void {
    this.teachersGrid?.instance.pageIndex(0);
    void this.teachersGrid?.instance.refresh();
  }

  openGroupCreate(): void {
    this.isEditingGroup = false;
    this.groupEditor = this.emptyGroup();
    this.groupEditorVisible = true;
  }

  openGroupEdit(group: StudentGroup): void {
    this.isEditingGroup = true;
    this.groupEditor = { id: group.id, code: group.code, name: group.name, status: group.status };
    this.groupEditorVisible = true;
  }

  async saveGroup(event: Event): Promise<void> {
    event.preventDefault();
    this.saving = true;
    try {
      const request = {
        code: this.groupEditor.code.trim(),
        name: this.groupEditor.name.trim(),
        status: this.groupEditor.status
      };
      if (this.isEditingGroup && this.groupEditor.id) {
        await firstValueFrom(this.groups.update(this.groupEditor.id, request));
        notify('Đã cập nhật nhóm.', 'success', 1800);
      } else {
        await firstValueFrom(this.groups.create(request));
        notify('Đã tạo nhóm.', 'success', 1800);
      }
      this.groupEditorVisible = false;
      await this.groupsGrid?.instance.refresh();
    } catch (error) {
      this.showError(error);
    } finally {
      this.saving = false;
    }
  }

  async removeGroup(group: StudentGroup): Promise<void> {
    const accepted = await confirm(`Xóa nhóm “${group.name}” (${group.code})?`, 'Xác nhận xóa nhóm');
    if (!accepted) {
      return;
    }
    try {
      await firstValueFrom(this.groups.delete(group.id));
      notify('Đã xóa nhóm.', 'success', 1800);
      await this.groupsGrid?.instance.refresh();
    } catch (error) {
      this.showError(error);
    }
  }

  openTeacherAssignment(group: StudentGroup): void {
    this.selectedGroup = group;
    this.selectedTeacherId = group.responsibleTeacherId ?? null;
    this.assignmentVisible = true;
  }

  async saveTeacherAssignment(): Promise<void> {
    if (!this.selectedGroup) {
      return;
    }
    this.saving = true;
    try {
      await firstValueFrom(this.groups.assignTeacher(this.selectedGroup.id, { teacherId: this.selectedTeacherId }));
      this.assignmentVisible = false;
      notify(this.selectedTeacherId ? 'Đã phân công giáo viên.' : 'Đã gỡ giáo viên phụ trách.', 'success', 1800);
      await this.groupsGrid?.instance.refresh();
    } catch (error) {
      this.showError(error);
    } finally {
      this.saving = false;
    }
  }

  async openRoster(group: StudentGroup): Promise<void> {
    this.selectedGroup = group;
    this.rosterSearch = '';
    this.selectedStudentId = null;
    this.rosterConflict = '';
    this.rosterVisible = true;
    await this.loadRoster();
  }

  async assignStudent(): Promise<void> {
    if (!this.selectedGroup || !this.selectedStudentId || this.groupIsFull) {
      return;
    }
    const student = await firstValueFrom(this.students.get(this.selectedStudentId));
    if (student.groupId && student.groupId !== this.selectedGroup.id) {
      const accepted = await confirm(
        `Chuyển “${student.fullName}” từ nhóm “${student.groupName ?? 'hiện tại'}” sang “${this.selectedGroup.name}”?`,
        'Xác nhận chuyển nhóm'
      );
      if (!accepted) {
        return;
      }
    }

    this.saving = true;
    this.rosterConflict = '';
    try {
      await firstValueFrom(this.students.assignGroup(student.id, {
        groupId: this.selectedGroup.id,
        expectedVersion: student.version
      }));
      this.selectedStudentId = null;
      notify('Đã phân nhóm học sinh.', 'success', 1800);
      await this.reloadRosterAndGroups();
    } catch (error) {
      this.handleRosterError(error);
    } finally {
      this.saving = false;
    }
  }

  async unassignStudent(student: Student): Promise<void> {
    const accepted = await confirm(`Gỡ “${student.fullName}” khỏi nhóm “${this.selectedGroup?.name}”?`, 'Xác nhận gỡ khỏi nhóm');
    if (!accepted) {
      return;
    }
    this.saving = true;
    this.rosterConflict = '';
    try {
      await firstValueFrom(this.students.assignGroup(student.id, {
        groupId: null,
        expectedVersion: student.version
      }));
      notify('Đã gỡ học sinh khỏi nhóm.', 'success', 1800);
      await this.reloadRosterAndGroups();
    } catch (error) {
      this.handleRosterError(error);
    } finally {
      this.saving = false;
    }
  }

  openPolicy(teacher: Teacher): void {
    this.selectedTeacher = teacher;
    this.policyConflict = '';
    this.policyEditor = {
      attendanceEditWindowDays: teacher.attendanceEditWindowDays,
      expectedVersion: teacher.version
    };
    this.policyVisible = true;
  }

  async savePolicy(event: Event): Promise<void> {
    event.preventDefault();
    if (!this.selectedTeacher) {
      return;
    }
    this.saving = true;
    try {
      this.selectedTeacher = await firstValueFrom(this.teachers.updateAttendancePolicy(this.selectedTeacher.id, this.policyEditor));
      this.policyVisible = false;
      notify('Đã cập nhật thời hạn sửa điểm danh.', 'success', 1800);
      await this.teachersGrid?.instance.refresh();
    } catch (error) {
      const apiError = ApiError.from(error);
      this.policyConflict = apiError.code === 'TeacherVersionConflict' ? apiError.message : '';
      this.notifyError(apiError);
    } finally {
      this.saving = false;
    }
  }

  async reloadPolicyTeacher(): Promise<void> {
    if (!this.selectedTeacher) {
      return;
    }
    try {
      const teacher = await firstValueFrom(this.teachers.get(this.selectedTeacher.id));
      this.selectedTeacher = teacher;
      this.policyEditor = {
        attendanceEditWindowDays: teacher.attendanceEditWindowDays,
        expectedVersion: teacher.version
      };
      this.policyConflict = '';
    } catch (error) {
      this.showError(error);
    }
  }

  groupStatusText(status: StudentGroupStatus): string {
    return this.groupStatuses.find(item => item.value === status)?.text ?? 'Không xác định';
  }

  teacherStatusText(status: UserStatus): string {
    return USER_STATUS_LABELS[status];
  }

  async reloadRosterConflict(): Promise<void> {
    this.rosterConflict = '';
    await this.reloadRosterAndGroups();
  }

  private async loadRoster(): Promise<void> {
    if (!this.selectedGroup) {
      return;
    }
    this.rosterLoading = true;
    try {
      const result = await firstValueFrom(this.students.list({
        page: 1,
        pageSize: 100,
        groupId: this.selectedGroup.id,
        sortBy: 'fullName',
        sortOrder: 'asc'
      }));
      this.roster = result.items;
      this.selectedGroup = { ...this.selectedGroup, studentCount: result.pagination.totalItems };
    } catch (error) {
      this.roster = [];
      this.showError(error);
    } finally {
      this.rosterLoading = false;
    }
  }

  private async reloadRosterAndGroups(): Promise<void> {
    await this.loadRoster();
    await this.groupsGrid?.instance.refresh();
  }

  private emptyGroup(): GroupEditor {
    return { code: '', name: '', status: 'Active' };
  }

  private readSort(sortValue: unknown, defaultField: string): { field: string; order: 'asc' | 'desc' } {
    const sort = Array.isArray(sortValue) ? sortValue[0] : sortValue;
    const config = sort && typeof sort === 'object' ? sort as { selector?: unknown; desc?: boolean } : undefined;
    return {
      field: typeof config?.selector === 'string' ? config.selector : defaultField,
      order: config?.desc ? 'desc' : 'asc'
    };
  }

  private rejectLoad(error: unknown): Promise<never> {
    const apiError = ApiError.from(error);
    this.notifyError(apiError);
    return Promise.reject(apiError);
  }

  private showError(error: unknown): void {
    this.notifyError(ApiError.from(error));
  }

  private handleRosterError(error: unknown): void {
    const apiError = ApiError.from(error);
    if (apiError.code === 'StudentVersionConflict') {
      this.rosterConflict = apiError.message;
    }
    this.notifyError(apiError);
  }

  private notifyError(error: ApiError): void {
    const message = error.traceId ? `${error.message} Mã tra cứu: ${error.traceId}` : error.message;
    notify(message, 'error', 3500);
  }
}

@NgModule({
  declarations: [StudentGroupsComponent],
  imports: [
    CommonModule,
    FormsModule,
    DxButtonModule,
    DxCheckBoxModule,
    DxDataGridModule,
    DxFormModule,
    DxLoadIndicatorModule,
    DxPopupModule,
    DxSelectBoxModule,
    DxTabsModule,
    DxTextBoxModule
  ],
  exports: [StudentGroupsComponent]
})
export class StudentGroupsModule {}
