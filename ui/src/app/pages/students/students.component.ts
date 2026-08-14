import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, NgModule, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import CustomStore from 'devextreme/data/custom_store';
import { confirm } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxCheckBoxModule } from 'devextreme-angular/ui/check-box';
import { DxDataGridComponent, DxDataGridModule } from 'devextreme-angular/ui/data-grid';
import { DxDateBoxModule } from 'devextreme-angular/ui/date-box';
import { DxFormModule } from 'devextreme-angular/ui/form';
import { DxPopupModule } from 'devextreme-angular/ui/popup';
import { DxSelectBoxComponent, DxSelectBoxModule } from 'devextreme-angular/ui/select-box';
import { DxTextBoxModule } from 'devextreme-angular/ui/text-box';
import {
  GENDER_LABELS,
  STUDENT_STATUS_LABELS,
  STUDY_MODE_LABELS,
  STUDY_WEEKDAY_LABELS,
  STUDY_WEEKDAY_SHORT_LABELS
} from '../../core/i18n/ui-labels';
import { ApiError } from '../../core/models/api-error';
import {
  asLegacyWidgetDataSource,
  PopupHidingEvent
} from '../../core/models/devextreme-legacy.types';
import {
  CreateStudentRequest,
  Gender,
  Student,
  StudentGroup,
  StudentStatus,
  StudyMode,
  StudyWeekday,
  UpdateStudentRequest
} from '../../core/models/api.models';
import { StudentGroupsService } from '../../core/services/student-groups.service';
import { StudentsService } from '../../core/services/students.service';
import { fromDateOnly, toDateOnly } from '../../core/utils/date-only';

interface StudentEditor {
  id?: string;
  studentCode: string;
  fullName: string;
  nickName: string;
  dateOfBirth: Date | null;
  gender: Gender | null;
  status: StudentStatus;
  guardianName: string;
  guardianPhone: string;
  note: string;
  studyMode: StudyMode;
  studyWeekdays: StudyWeekday[];
  groupId: string | null;
  groupCode: string | null;
  groupName: string | null;
  responsibleTeacherName: string | null;
  version: number;
}

interface AssignmentGroup extends StudentGroup {
  disabled: boolean;
}

const CANONICAL_WEEKDAYS: StudyWeekday[] = [
  'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'
];

@Component({
  selector: 'app-students',
  templateUrl: './students.component.html',
  styleUrls: ['./students.component.scss']
})
export class StudentsComponent {
  @ViewChild(DxDataGridComponent) grid?: DxDataGridComponent;
  @ViewChild('weekdayFieldset') weekdayFieldset?: ElementRef<HTMLElement>;
  @ViewChild('assignmentGroupPicker') assignmentGroupPicker?: DxSelectBoxComponent;

  readonly genders = [
    { value: 'Male', text: GENDER_LABELS.Male },
    { value: 'Female', text: GENDER_LABELS.Female },
    { value: 'Other', text: GENDER_LABELS.Other }
  ];
  readonly statuses = [
    { value: 'Active', text: STUDENT_STATUS_LABELS.Active },
    { value: 'Inactive', text: STUDENT_STATUS_LABELS.Inactive }
  ];
  readonly studyModes = [
    { value: 'FullDay', text: STUDY_MODE_LABELS.FullDay },
    { value: 'OneToOne', text: STUDY_MODE_LABELS.OneToOne }
  ];
  readonly weekdays = CANONICAL_WEEKDAYS.map(value => ({
    value,
    text: STUDY_WEEKDAY_LABELS[value],
    shortText: STUDY_WEEKDAY_SHORT_LABELS[value]
  }));
  readonly rowButtons = [
    {
      hint: 'Phân nhóm hoặc chuyển nhóm',
      icon: 'group',
      visible: (event: any) => event.row?.data?.status === 'Active' || !!event.row?.data?.groupId,
      onClick: (event: any) => this.openGroupAssignment(event.row.data as Student)
    },
    { hint: 'Chỉnh sửa', icon: 'edit', onClick: (event: any) => this.openEdit(event.row.data as Student) },
    { hint: 'Xóa', icon: 'trash', onClick: (event: any) => this.remove(event.row.data as Student) }
  ];
  readonly today = new Date();
  readonly groupDisplay = (group: StudentGroup | null): string => group
    ? `${group.code} · ${group.responsibleTeacherName} · ${group.studentCount}/100`
    : '';

  search = '';
  genderFilter: Gender | null = null;
  statusFilter: StudentStatus | null = null;
  groupIdFilter: string | null = null;
  unassignedFilter = false;
  studyModeFilter: StudyMode | null = null;
  studyWeekdayFilter: StudyWeekday | null = null;
  dateOfBirthFrom: Date | string | number = '';
  dateOfBirthTo: Date | string | number = '';

  editorVisible = false;
  saving = false;
  isEditing = false;
  editor: StudentEditor = this.emptyEditor();
  editorError = '';
  editorConflict = '';
  scheduleModeError = '';
  scheduleWeekdaysError = '';
  private editorBaseline = '';
  private editorDiscardConfirmationPending = false;
  private allowEditorCloseOnce = false;

  groupAssignmentVisible = false;
  assignmentSaving = false;
  assignmentStudent: Student | null = null;
  assignmentGroupId: string | null = null;
  assignmentError = '';
  assignmentConflict = '';
  private readonly groupCache = new Map<string, StudentGroup>();

  readonly dataSource = asLegacyWidgetDataSource(new CustomStore({
    key: 'id',
    load: loadOptions => {
      const pageSize = Math.min(loadOptions.take ?? 20, 100);
      const sort = this.readStudentSort(loadOptions.sort);
      return firstValueFrom(this.students.list({
        page: Math.floor((loadOptions.skip ?? 0) / pageSize) + 1,
        pageSize,
        search: this.search.trim() || undefined,
        gender: this.genderFilter ?? undefined,
        status: this.statusFilter ?? undefined,
        groupId: this.groupIdFilter ?? undefined,
        unassigned: this.unassignedFilter ? true : undefined,
        studyMode: this.studyModeFilter ?? undefined,
        studyWeekday: this.studyWeekdayFilter ?? undefined,
        dateOfBirthFrom: toDateOnly(this.dateOfBirthFrom),
        dateOfBirthTo: toDateOnly(this.dateOfBirthTo),
        sortBy: sort.field,
        sortOrder: sort.order
      })).then(response => ({ data: response.items, totalCount: response.pagination.totalItems }))
        .catch(error => this.rejectLoad(error));
    }
  }));

  readonly activeGroupDataSource = asLegacyWidgetDataSource(new CustomStore({
    key: 'id',
    byKey: key => {
      const cached = this.groupCache.get(String(key));
      return cached ? Promise.resolve(cached) : firstValueFrom(this.groups.get(String(key))).then(group => {
        this.groupCache.set(group.id, group);
        return group;
      });
    },
    load: options => {
      const pageSize = Math.min(options.take ?? 20, 100);
      return firstValueFrom(this.groups.list({
        page: Math.floor((options.skip ?? 0) / pageSize) + 1,
        pageSize,
        search: typeof options.searchValue === 'string' ? options.searchValue.trim() || undefined : undefined,
        status: 'Active',
        sortBy: 'code',
        sortOrder: 'asc'
      })).then(result => {
        result.items.forEach(group => this.groupCache.set(group.id, group));
        return { data: result.items, totalCount: result.pagination.totalItems };
      }).catch(error => this.rejectLoad(error));
    }
  }));

  readonly assignmentGroupDataSource = asLegacyWidgetDataSource(new CustomStore({
    key: 'id',
    byKey: key => {
      const id = String(key);
      const cached = this.groupCache.get(id);
      const resolve = cached ? Promise.resolve(cached) : firstValueFrom(this.groups.get(id));
      return resolve.then(group => {
        this.groupCache.set(group.id, group);
        return this.assignmentGroup(group);
      });
    },
    load: options => {
      const pageSize = Math.min(options.take ?? 20, 100);
      return firstValueFrom(this.groups.list({
        page: Math.floor((options.skip ?? 0) / pageSize) + 1,
        pageSize,
        search: typeof options.searchValue === 'string' ? options.searchValue.trim() || undefined : undefined,
        status: 'Active',
        sortBy: 'code',
        sortOrder: 'asc'
      })).then(result => {
        result.items.forEach(group => this.groupCache.set(group.id, group));
        return {
          data: result.items.map(group => this.assignmentGroup(group)),
          totalCount: result.pagination.totalItems
        };
      }).catch(error => this.rejectLoad(error));
    }
  }));

  get editorTitle(): string {
    return this.isEditing ? 'Cập nhật học sinh' : 'Thêm học sinh';
  }

  get editorDirty(): boolean {
    return this.editorVisible && this.editorBaseline !== this.serializeEditor(this.editor);
  }

  get assignmentTitle(): string {
    return this.assignmentStudent?.groupId ? 'Chuyển hoặc gỡ nhóm' : 'Phân nhóm học sinh';
  }

  get assignmentChanged(): boolean {
    return !!this.assignmentStudent && this.assignmentGroupId !== (this.assignmentStudent.groupId ?? null);
  }

  constructor(
    private readonly students: StudentsService,
    private readonly groups: StudentGroupsService
  ) {}

  applyFilters(): void {
    if (this.dateOfBirthFrom && this.dateOfBirthTo && new Date(this.dateOfBirthFrom) > new Date(this.dateOfBirthTo)) {
      notify('Khoảng ngày sinh không hợp lệ.', 'error', 2200);
      return;
    }
    this.grid?.instance.pageIndex(0);
    void this.grid?.instance.refresh();
  }

  resetFilters(): void {
    this.search = '';
    this.genderFilter = null;
    this.statusFilter = null;
    this.groupIdFilter = null;
    this.unassignedFilter = false;
    this.studyModeFilter = null;
    this.studyWeekdayFilter = null;
    this.dateOfBirthFrom = '';
    this.dateOfBirthTo = '';
    this.applyFilters();
  }

  onGroupFilterChanged(groupId: string | null | undefined): void {
    this.groupIdFilter = groupId ?? null;
    if (this.groupIdFilter) this.unassignedFilter = false;
  }

  onUnassignedChanged(value: boolean | null | undefined): void {
    this.unassignedFilter = !!value;
    if (this.unassignedFilter) this.groupIdFilter = null;
  }

  openCreate(): void {
    this.isEditing = false;
    this.editor = this.emptyEditor();
    this.resetEditorMessages();
    this.editorBaseline = this.serializeEditor(this.editor);
    this.editorVisible = true;
  }

  openEdit(student: Student): void {
    this.isEditing = true;
    this.setEditor(student);
    this.resetEditorMessages();
    this.editorVisible = true;
  }

  onEditorHiding(event: PopupHidingEvent): void {
    if (this.allowEditorCloseOnce) {
      this.allowEditorCloseOnce = false;
      return;
    }
    if (this.saving) {
      event.cancel = true;
      return;
    }
    if (!this.editorDirty) {
      return;
    }

    event.cancel = true;
    if (this.editorDiscardConfirmationPending) {
      return;
    }

    this.editorDiscardConfirmationPending = true;
    void Promise.resolve(this.confirmEditorDiscard())
      .then(discard => {
        if (discard) {
          this.allowEditorCloseOnce = true;
          this.editorVisible = false;
        }
      })
      .finally(() => {
        this.editorDiscardConfirmationPending = false;
      });
  }

  closeEditor(): void {
    this.editorVisible = false;
  }

  isWeekdaySelected(weekday: StudyWeekday): boolean {
    return this.editor.studyWeekdays.includes(weekday);
  }

  toggleWeekday(weekday: StudyWeekday, selected: boolean): void {
    const next = selected
      ? [...this.editor.studyWeekdays, weekday]
      : this.editor.studyWeekdays.filter(item => item !== weekday);
    this.editor.studyWeekdays = this.canonicalWeekdays(next);
    this.scheduleWeekdaysError = '';
  }

  async save(event: Event): Promise<void> {
    event.preventDefault();
    this.resetEditorMessages();
    const dateOfBirth = toDateOnly(this.editor.dateOfBirth);
    if (!dateOfBirth) {
      this.editorError = 'Vui lòng nhập ngày sinh.';
      return;
    }
    if (this.editor.studyWeekdays.length === 0) {
      this.scheduleWeekdaysError = 'Vui lòng chọn ít nhất một ngày học.';
      setTimeout(() => this.weekdayFieldset?.nativeElement.focus());
      return;
    }
    if (this.isEditing && this.editor.status === 'Inactive' && this.editor.groupId) {
      this.editorError = 'Cần gỡ học sinh khỏi nhóm trước khi chuyển sang trạng thái Ngừng học.';
      return;
    }

    const request: CreateStudentRequest = {
      studentCode: this.editor.studentCode.trim(),
      fullName: this.editor.fullName.trim(),
      nickName: this.editor.nickName.trim(),
      dateOfBirth,
      gender: this.editor.gender,
      status: this.editor.status,
      guardianName: this.editor.guardianName.trim() || null,
      guardianPhone: this.editor.guardianPhone.trim() || null,
      note: this.editor.note.trim() || null,
      studySchedule: {
        mode: this.editor.studyMode,
        weekdays: this.canonicalWeekdays(this.editor.studyWeekdays)
      }
    };

    this.saving = true;
    try {
      let saved: Student;
      if (this.isEditing && this.editor.id) {
        const updateRequest: UpdateStudentRequest = { ...request, expectedVersion: this.editor.version };
        saved = await firstValueFrom(this.students.update(this.editor.id, updateRequest));
        notify('Đã cập nhật học sinh.', 'success', 1800);
      } else {
        saved = await firstValueFrom(this.students.create(request));
        notify('Đã thêm học sinh.', 'success', 1800);
      }
      this.setEditor(saved);
      this.editorVisible = false;
      await this.grid?.instance.refresh();
    } catch (error) {
      this.handleEditorError(error);
    } finally {
      this.saving = false;
    }
  }

  async reloadEditor(): Promise<void> {
    if (!this.editor.id) return;
    const accepted = await confirm(
      'Tải bản mới nhất sẽ thay thế các thông tin bạn đang nhập.',
      'Tải lại dữ liệu?'
    );
    if (!accepted) return;
    try {
      this.setEditor(await firstValueFrom(this.students.get(this.editor.id)));
      this.resetEditorMessages();
    } catch (error) {
      this.showError(error);
    }
  }

  openGroupAssignment(student: Student): void {
    if (student.status === 'Inactive' && !student.groupId) {
      notify('Chỉ có thể phân nhóm học sinh đang học.', 'warning', 2400);
      return;
    }
    this.assignmentStudent = student;
    this.assignmentGroupId = student.groupId ?? null;
    this.assignmentError = '';
    this.assignmentConflict = '';
    this.assignmentGroupDataSource.clearRawDataCache();
    this.groupAssignmentVisible = true;
    setTimeout(() => void this.assignmentGroupPicker?.instance.getDataSource().reload());
  }

  async saveGroupAssignment(): Promise<void> {
    const student = this.assignmentStudent;
    if (!student || !this.assignmentChanged || this.assignmentSaving) return;
    if (student.status === 'Inactive' && this.assignmentGroupId) {
      this.assignmentError = 'Học sinh ngừng học chỉ có thể được gỡ khỏi nhóm hiện tại.';
      return;
    }

    const nextGroup = this.assignmentGroupId ? this.groupCache.get(this.assignmentGroupId) : null;
    const action = this.assignmentGroupId
      ? student.groupId
        ? `Chuyển “${student.fullName}” từ nhóm “${student.groupName}” sang “${nextGroup?.name ?? 'nhóm đã chọn'}”?`
        : `Phân “${student.fullName}” vào nhóm “${nextGroup?.name ?? 'đã chọn'}”?`
      : `Gỡ “${student.fullName}” khỏi nhóm “${student.groupName}”?`;
    const accepted = await confirm(action, student.groupId ? 'Xác nhận thay đổi nhóm' : 'Xác nhận phân nhóm');
    if (!accepted) return;

    this.assignmentSaving = true;
    this.assignmentError = '';
    this.assignmentConflict = '';
    try {
      this.assignmentStudent = await firstValueFrom(this.students.assignGroup(student.id, {
        groupId: this.assignmentGroupId,
        expectedVersion: student.version
      }));
      this.groupCache.clear();
      this.activeGroupDataSource.clearRawDataCache();
      this.assignmentGroupDataSource.clearRawDataCache();
      notify(this.assignmentGroupId ? 'Đã cập nhật nhóm của học sinh.' : 'Đã gỡ học sinh khỏi nhóm.', 'success', 1800);
      this.groupAssignmentVisible = false;
      await this.grid?.instance.refresh();
    } catch (error) {
      const apiError = ApiError.from(error);
      if (apiError.code === 'StudentVersionConflict') this.assignmentConflict = apiError.message;
      this.assignmentError = apiError.message;
    } finally {
      this.assignmentSaving = false;
    }
  }

  async reloadAssignmentStudent(): Promise<void> {
    if (!this.assignmentStudent) return;
    try {
      this.assignmentStudent = await firstValueFrom(this.students.get(this.assignmentStudent.id));
      this.assignmentGroupDataSource.clearRawDataCache();
      await this.assignmentGroupPicker?.instance.getDataSource().reload();
      this.assignmentConflict = '';
      this.assignmentError = '';
    } catch (error) {
      this.showError(error);
    }
  }

  async remove(student: Student): Promise<void> {
    if (student.groupId) {
      notify('Cần gỡ học sinh khỏi nhóm trước khi xóa.', 'warning', 2600);
      this.openGroupAssignment(student);
      return;
    }
    const accepted = await confirm(`Xóa học sinh “${student.fullName}” (${student.studentCode})?`, 'Xác nhận xóa');
    if (!accepted) return;

    try {
      await firstValueFrom(this.students.delete(student.id, student.version));
      notify('Đã xóa học sinh.', 'success', 1800);
      await this.grid?.instance.refresh();
    } catch (error) {
      const apiError = ApiError.from(error);
      this.showError(apiError);
      if (apiError.code === 'StudentVersionConflict') await this.grid?.instance.refresh();
    }
  }

  statusText(status: StudentStatus): string {
    return STUDENT_STATUS_LABELS[status] ?? 'Không xác định';
  }

  genderText(gender: Gender | null): string {
    return gender ? GENDER_LABELS[gender] : '—';
  }

  groupText(student: Student): string {
    return student.groupId ? `${student.groupCode} · ${student.groupName}` : 'Chưa phân nhóm';
  }

  scheduleText(student: Student): string {
    const days = student.studySchedule.weekdays.map(day => STUDY_WEEKDAY_SHORT_LABELS[day]).join(', ');
    return `${STUDY_MODE_LABELS[student.studySchedule.mode]} · ${days}`;
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.editorDirty) event.preventDefault();
  }

  private setEditor(student: Student): void {
    this.editor = {
      id: student.id,
      studentCode: student.studentCode,
      fullName: student.fullName,
      nickName: student.nickName,
      dateOfBirth: fromDateOnly(student.dateOfBirth),
      gender: student.gender ?? null,
      status: student.status,
      guardianName: student.guardianName ?? '',
      guardianPhone: student.guardianPhone ?? '',
      note: student.note ?? '',
      studyMode: student.studySchedule.mode,
      studyWeekdays: this.canonicalWeekdays(student.studySchedule.weekdays),
      groupId: student.groupId ?? null,
      groupCode: student.groupCode ?? null,
      groupName: student.groupName ?? null,
      responsibleTeacherName: student.responsibleTeacherName ?? null,
      version: student.version
    };
    this.editorBaseline = this.serializeEditor(this.editor);
  }

  private emptyEditor(): StudentEditor {
    return {
      studentCode: '',
      fullName: '',
      nickName: '',
      dateOfBirth: null,
      gender: null,
      status: 'Active',
      guardianName: '',
      guardianPhone: '',
      note: '',
      studyMode: 'FullDay',
      studyWeekdays: [...CANONICAL_WEEKDAYS],
      groupId: null,
      groupCode: null,
      groupName: null,
      responsibleTeacherName: null,
      version: 1
    };
  }

  private canonicalWeekdays(values: StudyWeekday[]): StudyWeekday[] {
    const selected = new Set(values);
    return CANONICAL_WEEKDAYS.filter(day => selected.has(day));
  }

  private assignmentGroup(group: StudentGroup): AssignmentGroup {
    return {
      ...group,
      disabled: group.studentCount >= 100 || group.id === this.assignmentStudent?.groupId
    };
  }

  private readStudentSort(sortValue: unknown): { field: string; order: 'asc' | 'desc' } {
    const sort = Array.isArray(sortValue) ? sortValue[0] : sortValue;
    const config = sort && typeof sort === 'object' ? sort as { selector?: unknown; desc?: boolean } : undefined;
    const selector = config?.selector === 'studySchedule.mode' ? 'studyMode' : config?.selector;
    const allowed = new Set([
      'studentCode', 'fullName', 'nickName', 'dateOfBirth', 'gender', 'status', 'createdAt', 'studyMode'
    ]);
    return {
      field: typeof selector === 'string' && allowed.has(selector) ? selector : 'createdAt',
      order: config?.desc ? 'desc' : 'asc'
    };
  }

  private serializeEditor(editor: StudentEditor): string {
    return JSON.stringify({
      ...editor,
      dateOfBirth: toDateOnly(editor.dateOfBirth),
      studyWeekdays: this.canonicalWeekdays(editor.studyWeekdays)
    });
  }

  private confirmEditorDiscard(): Promise<boolean> {
    return confirm(
      'Thông tin học sinh chưa được lưu sẽ bị mất.',
      'Bỏ thay đổi?'
    );
  }

  private resetEditorMessages(): void {
    this.editorError = '';
    this.editorConflict = '';
    this.scheduleModeError = '';
    this.scheduleWeekdaysError = '';
  }

  private handleEditorError(error: unknown): void {
    const apiError = ApiError.from(error);
    this.editorError = apiError.message;
    this.editorConflict = apiError.code === 'StudentVersionConflict' ? apiError.message : '';
    Object.keys(apiError.fieldErrors).forEach(field => {
      const normalized = field.toLocaleLowerCase('en-US');
      if (normalized.includes('studyschedule.mode')) {
        this.scheduleModeError = 'Vui lòng chọn hình thức học hợp lệ.';
      }
      if (normalized.includes('studyschedule.weekdays')) {
        this.scheduleWeekdaysError = 'Vui lòng chọn từ một đến sáu ngày, không trùng và không có Chủ nhật.';
        setTimeout(() => this.weekdayFieldset?.nativeElement.focus());
      }
    });
  }

  private rejectLoad(error: unknown): Promise<never> {
    const apiError = ApiError.from(error);
    this.showError(apiError);
    return Promise.reject(apiError);
  }

  private showError(error: unknown): void {
    const apiError = ApiError.from(error);
    const message = apiError.traceId ? `${apiError.message} Mã tra cứu: ${apiError.traceId}` : apiError.message;
    notify(message, 'error', 3200);
  }
}

@NgModule({
  declarations: [StudentsComponent],
  imports: [
    CommonModule,
    FormsModule,
    DxButtonModule,
    DxCheckBoxModule,
    DxDataGridModule,
    DxDateBoxModule,
    DxFormModule,
    DxPopupModule,
    DxSelectBoxModule,
    DxTextBoxModule
  ],
  exports: [StudentsComponent]
})
export class StudentsModule {}
