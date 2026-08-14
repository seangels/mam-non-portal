import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, NgModule, OnInit, QueryList, ViewChild, ViewChildren } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import CustomStore from 'devextreme/data/custom_store';
import { confirm } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxDateBoxModule } from 'devextreme-angular/ui/date-box';
import { DxLoadIndicatorModule } from 'devextreme-angular/ui/load-indicator';
import { DxPopupModule } from 'devextreme-angular/ui/popup';
import { DxSelectBoxModule } from 'devextreme-angular/ui/select-box';
import { DxTagBoxModule } from 'devextreme-angular/ui/tag-box';
import { DxListComponent, DxListModule } from 'devextreme-angular/ui/list';
import { DxTextBoxModule } from 'devextreme-angular/ui/text-box';
import { ApiError } from '../../core/models/api-error';
import {
  AttendanceContext,
  AttendanceContextGroup,
  AttendanceEntry,
  AttendanceStatus,
  DailyAttendance,
  RecoveryGroupCandidate,
  RecoveryStudentCandidate,
  RecoveryTeacherCandidate,
  SaveAttendanceRecord
} from '../../core/models/api.models';
import {
  ATTENDANCE_STATUS_LABELS,
  SHEET_STATE_LABELS,
  SNAPSHOT_SOURCE_LABELS,
  readOnlyReasonLabel
} from '../../core/i18n/ui-labels';
import { PendingChangesAware } from '../../core/guards/pending-changes.guard';
import { AttendanceService } from '../../core/services/attendance.service';
import { businessToday, formatDateOnly, fromDateOnly, toDateOnly } from '../../core/utils/date-only';
import { includesVietnamese } from '../../core/utils/vietnamese-search';
import { AuthService } from '../../shared/services';

interface AttendanceDraft extends AttendanceEntry {
  invalidMessage?: string;
}

interface AttendanceStatusOption {
  value: AttendanceStatus;
  text: string;
  compactText: string;
  accessibleText: string;
  className: string;
}

@Component({
  selector: 'app-attendance',
  templateUrl: './attendance.component.html',
  styleUrls: ['./attendance.component.scss', './attendance-card.component.scss']
})
export class AttendanceComponent implements OnInit, PendingChangesAware {
  @ViewChildren('studentCard') studentCards?: QueryList<ElementRef<HTMLElement>>;
  @ViewChild('recoveryStudentList') recoveryStudentList!: DxListComponent;
  readonly statusOptions: AttendanceStatusOption[] = [
    { value: 'Present', text: ATTENDANCE_STATUS_LABELS.Present, compactText: 'Có mặt', accessibleText: 'Có mặt', className: 'status-present' },
    { value: 'AbsentFullDay', text: ATTENDANCE_STATUS_LABELS.AbsentFullDay, compactText: 'Nghỉ', accessibleText: 'Nghỉ cả ngày', className: 'status-absent' },
    { value: 'AbsentHalfDay', text: ATTENDANCE_STATUS_LABELS.AbsentHalfDay, compactText: 'Nghỉ 1/2', accessibleText: 'Nghỉ một nửa ngày', className: 'status-half-day' },
    { value: 'OneToOneHour', text: ATTENDANCE_STATUS_LABELS.OneToOneHour, compactText: '1-1', accessibleText: 'Học một kèm một, 60 phút', className: 'status-one-to-one' },
    { value: 'Unmarked', text: ATTENDANCE_STATUS_LABELS.Unmarked, compactText: 'Chưa điểm danh', accessibleText: 'Chưa điểm danh', className: 'status-unmarked' }
  ];
  readonly sheetStateLabels = SHEET_STATE_LABELS;
  readonly snapshotSourceLabels = SNAPSHOT_SOURCE_LABELS;
  readonly contextGroupText = (group: AttendanceContextGroup | null): string => group
    ? `${group.name} · ${group.studentCount} học sinh có lịch`
    : '';
  readonly recoveryGroupText = (item: RecoveryGroupCandidate | null): string => item
    ? `${item.code} · ${item.name}${item.isDeleted ? ' · Đã xóa' : item.status === 'Inactive' ? ' · Ngừng hoạt động' : ''}`
    : '';
  readonly recoveryTeacherText = (item: RecoveryTeacherCandidate | null): string => item
    ? `${item.fullName}${item.isDeleted ? ' · Đã xóa' : !item.isCurrentTeacherRole ? ' · Không còn vai trò giáo viên' : item.status !== 'Active' ? ' · Không hoạt động' : ''}`
    : '';
  readonly recoveryStudentText = (item: RecoveryStudentCandidate | null): string => item
    ? `${item.studentCode} · ${item.fullName}${item.nickName ? ` - [${item.nickName}]` : ''}${item.isDeleted ? ' · Đã xóa' : item.status === 'Inactive' ? ' · Ngừng học' : ''} ${item.currentGroupId ? ` - Nhóm hiện tại: ${item.groupCode} · ${item.responsibleTeacherName}` : ''}`
    : '';

  date = businessToday();
  maxDate = businessToday();
  context: AttendanceContext | null = null;
  selectedGroupId: string | null = null;
  daily: DailyAttendance | null = null;
  drafts: AttendanceDraft[] = [];
  contextLoading = false;
  dailyLoading = false;
  saving = false;
  filtersExpanded = true;
  search = '';
  statusFilter: AttendanceStatus | null = null;
  errorMessage = '';
  errorTitle = '';
  traceId = '';
  conflictMessage = '';
  recoveryAvailable = false;

  recoveryVisible = false;
  recoverySaving = false;
  recoveryGroupId: string | null = null;
  recoveryTeacherId: string | null = null;
  recoveryStudents: RecoveryStudentCandidate[] = [];
  recoveryStudentIds: string[] = [];
  recoveryDrafts: AttendanceDraft[] = [];
  recoveryReason = '';
  recoveryAcknowledged = false;
  recoveryError = '';

  private baseline = new Map<string, string>();
  private noteBaselines = new Map<string, string | null>();
  private contextRequest = 0;
  private dailyRequest = 0;
  private groupCandidates = new Map<string, RecoveryGroupCandidate>();
  private studentCandidates = new Map<string, RecoveryStudentCandidate>();
  private teacherCandidates = new Map<string, RecoveryTeacherCandidate>();

  readonly recoveryGroupDataSource = new CustomStore({
    key: 'id',
    load: options => firstValueFrom(this.attendance.recoveryGroups({
      page: this.page(options), pageSize: this.pageSize(options), search: this.searchOf(options)
    })).then(result => {
      result.items.forEach(item => this.groupCandidates.set(item.id, item));
      return { data: result.items, totalCount: result.pagination.totalItems };
    }).catch(error => this.rejectCandidate(error)),
    byKey: key => Promise.resolve(this.groupCandidates.get(String(key)) ?? null)
  });

  readonly recoveryTeacherDataSource = new CustomStore({
    key: 'id',
    load: options => firstValueFrom(this.attendance.recoveryTeachers({
      page: this.page(options), pageSize: this.pageSize(options), search: this.searchOf(options)
    })).then(result => {
      result.items.forEach(item => this.teacherCandidates.set(item.id, item));
      return { data: result.items, totalCount: result.pagination.totalItems };
    }).catch(error => this.rejectCandidate(error)),
    byKey: key => Promise.resolve(this.teacherCandidates.get(String(key)) ?? null)
  });

  readonly recoveryStudentDataSource = new CustomStore({
    key: 'id',
    load: options => firstValueFrom(this.attendance.recoveryStudents({
      page: this.page(options), pageSize: this.pageSize(options), search: this.searchOf(options)
    })).then(result => {
      result.items.forEach(item => this.studentCandidates.set(item.id, item));
      return { data: result.items, totalCount: result.pagination.totalItems };
    }).catch(error => this.rejectCandidate(error)),
    byKey: key => Promise.resolve(this.studentCandidates.get(String(key)) ?? null)
  });

  get isAdministrator(): boolean {
    return this.auth.hasRole('SuperAdmin', 'Admin');
  }

  get isTeacher(): boolean {
    return this.auth.hasRole('Teacher');
  }

  get showGroupSelector(): boolean {
    return this.isAdministrator || (this.context?.groups.length ?? 0) > 1;
  }

  get selectedGroup(): AttendanceContextGroup | null {
    return this.context?.groups.find(group => group.id === this.selectedGroupId) ?? null;
  }

  get filteredDrafts(): AttendanceDraft[] {
    return this.drafts.filter(item =>
      (!this.statusFilter || item.status === this.statusFilter) &&
      includesVietnamese([item.studentCode, item.fullName, item.nickName], this.search)
    );
  }

  get dirtyCount(): number {
    return this.drafts.filter(item => this.isDirty(item)).length;
  }

  get summary(): { total: number; present: number; absent: number; oneToOne: number; unmarked: number } {
    return {
      total: this.drafts.length,
      present: this.drafts.filter(item => item.status === 'Present').length,
      absent: this.drafts.filter(item => item.status === 'AbsentFullDay' || item.status === 'AbsentHalfDay').length,
      oneToOne: this.drafts.filter(item => item.status === 'OneToOneHour').length,
      unmarked: this.drafts.filter(item => item.status === 'Unmarked').length
    };
  }

  get canModify(): boolean {
    if (!this.daily) return false;
    return this.daily.sheetState === 'Missing' ? this.daily.canCreate : this.daily.canEdit;
  }

  get saveDisabled(): boolean {
    if (!this.daily || !this.canModify || this.saving || this.drafts.length === 0) return true;
    return this.daily.sheetState === 'Saved' && this.dirtyCount === 0;
  }

  get saveButtonText(): string {
    if (this.saving) return 'Đang lưu…';
    return this.daily?.sheetState === 'Missing' ? 'Lưu phiếu' : `Lưu thay đổi (${this.dirtyCount})`;
  }

  get readOnlyText(): string {
    return readOnlyReasonLabel(this.daily?.readOnlyReason ?? this.context?.readOnlyReason ?? null);
  }

  get noScheduledStudents(): boolean {
    return this.daily?.sheetState === 'Missing' && this.daily.readOnlyReason === 'NoScheduledStudents';
  }

  get recoveryReady(): boolean {
    return !!this.recoveryGroupId && !!this.recoveryTeacherId && this.recoveryDrafts.length > 0 &&
      !!this.recoveryReason.trim() && this.recoveryReason.trim().length <= 500 && this.recoveryAcknowledged;
  }

  get canStartHistoricalRecovery(): boolean {
    return this.isAdministrator && this.date < this.maxDate;
  }

  constructor(
    private readonly attendance: AttendanceService,
    private readonly auth: AuthService
  ) { }

  ngOnInit(): void {
    console.log('attendance.component đã được khởi tạo/quay lại!');
    void this.loadContext();
  }

  async changeDate(event: { value?: Date | string | null }): Promise<void> {
    const next = toDateOnly(event.value ?? this.date);
    if (!next || next === this.date) return;
    if (!(await this.confirmDiscard())) return;
    this.date = next;
    await this.loadContext();
  }

  async changeGroup(event: { value?: string | null }): Promise<void> {
    const next = event.value ?? null;
    if (next === this.selectedGroupId) return;
    if (!(await this.confirmDiscard())) return;
    this.selectedGroupId = next;
    this.clearDaily();
    if (next) await this.loadDaily();
  }

  async reload(): Promise<void> {
    if (!(await this.confirmDiscard())) return;
    await this.loadContext(true);
  }

  toggleFilters(): void {
    this.filtersExpanded = !this.filtersExpanded;
  }

  clearFilters(): void {
    this.search = '';
    this.statusFilter = null;
    this.scrollCardsToTop();
  }

  onSearchChanged(value: string | null | undefined): void {
    this.search = value ?? '';
    this.scrollCardsToTop();
  }

  onStatusFilterChanged(value: AttendanceStatus | null | undefined): void {
    this.statusFilter = value ?? null;
    this.scrollCardsToTop();
  }

  onStatusChange(item: AttendanceDraft, value: AttendanceStatus): void {
    item.status = value;
    item.invalidMessage = undefined;
    if (value === 'Present' || value === 'Unmarked') {
      item.halfDayPart = null;
      item.isExcused = null;
      item.durationMinutes = null;
    } else if (value === 'AbsentHalfDay') {
      item.halfDayPart = null;
      item.isExcused ??= false;
      item.durationMinutes = null;
    } else if (value === 'AbsentFullDay') {
      item.halfDayPart = null;
      item.isExcused ??= false;
      item.durationMinutes = null;
    } else {
      item.halfDayPart = null;
      item.isExcused = null;
      item.durationMinutes = 60;
    }
  }

  onRecoveryStatusChange(item: AttendanceDraft, value: AttendanceStatus): void {
    this.onStatusChange(item, value);
  }

  clearValidation(item: AttendanceDraft): void {
    item.invalidMessage = undefined;
  }

  cardIdentity(item: AttendanceDraft): string {
    return `${item.nickName.trim() || 'Chưa có tên gọi'} · ${item.studentCode}`;
  }

  statusClass(status: AttendanceStatus): string {
    return this.statusOptions.find(option => option.value === status)?.className ?? 'status-unmarked';
  }

  noteCounter(item: AttendanceDraft): string {
    const length = item.notes?.length ?? 0;
    return length > 200 && !this.notesChanged(item)
      ? `Dữ liệu cũ · ${length} ký tự`
      : `${length}/200`;
  }

  isLegacyNote(item: AttendanceDraft): boolean {
    return (item.notes?.length ?? 0) > 200 && !this.notesChanged(item);
  }

  async save(): Promise<void> {
    if (!this.daily || this.saveDisabled || !this.validate(this.drafts)) return;
    this.saving = true;
    this.errorMessage = '';
    this.conflictMessage = '';
    try {
      const records = this.drafts.map(item => this.toRecord(item));
      let result: DailyAttendance;
      if (this.daily.sheetState === 'Missing') {
        if (this.daily.currentSnapshotVersion == null || !this.selectedGroupId) {
          this.errorTitle = 'Không thể lưu điểm danh.';
          this.errorMessage = 'Không thể xác định phiên bản danh sách học sinh. Hãy tải lại dữ liệu.';
          return;
        }
        result = await firstValueFrom(this.attendance.create({
          groupId: this.selectedGroupId,
          date: this.date,
          expectedSnapshotVersion: this.daily.currentSnapshotVersion,
          records
        }));
      } else {
        if (!this.daily.sheetId || this.daily.sheetVersion == null) {
          this.errorTitle = 'Không thể lưu điểm danh.';
          this.errorMessage = 'Không thể xác định phiên bản phiếu điểm danh. Hãy tải lại dữ liệu.';
          return;
        }
        result = await firstValueFrom(this.attendance.update(this.daily.sheetId, {
          expectedVersion: this.daily.sheetVersion,
          records
        }));
      }
      this.applyDaily(result);
      notify('Đã lưu điểm danh.', 'success', 2500);
    } catch (error) {
      await this.handleWriteError(error);
    } finally {
      this.saving = false;
    }
  }

  openRecovery(): void {
    this.recoveryError = '';
    this.recoveryGroupId = this.selectedGroupId;
    this.recoveryTeacherId = null;
    this.recoveryStudentIds = [];
    this.recoveryDrafts = [];
    this.recoveryReason = '';
    this.recoveryAcknowledged = false;
    if (this.selectedGroup) {
      this.groupCandidates.set(this.selectedGroup.id, {
        id: this.selectedGroup.id,
        code: this.selectedGroup.code,
        name: this.selectedGroup.name,
        status: 'Active',
        isDeleted: false
      });
    }
    this.recoveryVisible = true;
  }

  onRecoveryStudentsChanged(event: any): void {
    const requested = this.recoveryStudentList.selectedItemKeys;
    const ids = requested.slice(0, 100);
    if (requested.length > 100) notify('Mỗi phiếu có tối đa 100 học sinh.', 'warning', 2500);
    const previous = new Map(this.recoveryDrafts.map(item => [item.studentId, item]));
    this.recoveryStudentIds = ids;
    this.recoveryDrafts = ids.map(id => previous.get(id) ?? this.recoveryDraft(this.studentCandidates.get(id), id));
  }
  onRecoveryShown(): void {
    console.log('onRecoveryShown()');
    this.recoveryStudentList.instance.unselectAll();
  }
  async saveRecovery(): Promise<void> {
    if (!this.recoveryReady || !this.validate(this.recoveryDrafts, true)) return;
    this.recoverySaving = true;
    this.recoveryError = '';
    try {
      const result = await firstValueFrom(this.attendance.recover({
        groupId: this.recoveryGroupId!,
        date: this.date,
        responsibleTeacherId: this.recoveryTeacherId!,
        records: this.recoveryDrafts.map(item => this.toRecord(item, true)),
        acknowledgeHistoricalSnapshot: true,
        recoveryReason: this.recoveryReason.trim()
      }));
      this.recoveryVisible = false;
      this.selectedGroupId = result.group.id;
      this.applyDaily(result);
      notify('Đã khôi phục và lưu phiếu điểm danh.', 'success', 3000);
    } catch (error) {
      const apiError = ApiError.from(error);
      this.recoveryError = apiError.message;
      this.traceId = apiError.traceId ?? '';
      this.applyFieldErrors(apiError, this.recoveryDrafts, true);
    } finally {
      this.recoverySaving = false;
    }
  }

  canLeave(): boolean | Promise<boolean> {
    return !this.hasPendingChanges() || confirm(
      'Các thay đổi điểm danh chưa được lưu. Bạn có muốn rời trang và bỏ thay đổi?',
      'Bỏ thay đổi?'
    );
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasPendingChanges()) event.preventDefault();
  }

  formatDate(value: string): string {
    return formatDateOnly(value);
  }

  isDirty(item: AttendanceDraft): boolean {
    return this.baseline.get(item.studentId) !== this.serialize(item);
  }

  trackStudent(_index: number, item: AttendanceDraft): string {
    return item.studentId;
  }

  private async loadContext(keepSelection = false): Promise<void> {
    const request = ++this.contextRequest;
    this.contextLoading = true;
    this.errorMessage = '';
    this.traceId = '';
    this.conflictMessage = '';
    this.recoveryAvailable = false;
    if (!keepSelection) this.clearDaily();
    try {
      const context = await firstValueFrom(this.attendance.context(this.date));
      if (request !== this.contextRequest) return;
      this.context = context;
      this.maxDate = context.serverDate;
      const stillAvailable = context.groups.some(group => group.id === this.selectedGroupId);
      if (!keepSelection || !stillAvailable) {
        this.selectedGroupId = this.isTeacher && context.groups.length === 1 ? context.groups[0].id : null;
      }
      if (this.selectedGroupId) await this.loadDaily();
      const recoveryStudentsResult = await firstValueFrom(this.attendance.recoveryStudents({ page: 1, pageSize: 100 }));
      recoveryStudentsResult.items.forEach(item => this.studentCandidates.set(item.id, item));
      this.recoveryStudents = recoveryStudentsResult.items.map(item => {
        return {
          ...item,
          text: this.recoveryStudentText(item)
        }
      });
    } catch (error) {
      if (request === this.contextRequest) this.setError(error, 'Không thể tải phạm vi điểm danh.');
    } finally {
      if (request === this.contextRequest) this.contextLoading = false;
    }
  }

  private async loadDaily(): Promise<void> {
    if (!this.selectedGroupId) return;
    const request = ++this.dailyRequest;
    this.dailyLoading = true;
    this.errorMessage = '';
    this.traceId = '';
    this.recoveryAvailable = false;
    try {
      const daily = await firstValueFrom(this.attendance.daily(this.date, this.selectedGroupId));
      if (request !== this.dailyRequest) return;
      this.applyDaily(daily);
    } catch (error) {
      if (request !== this.dailyRequest) return;
      const apiError = this.setError(error, 'Không thể tải danh sách điểm danh.');
      this.recoveryAvailable = this.isAdministrator && apiError.code === 'HistoricalSnapshotUnavailable';
      this.daily = null;
      this.drafts = [];
      this.baseline.clear();
      this.noteBaselines.clear();
    } finally {
      if (request === this.dailyRequest) this.dailyLoading = false;
    }
  }

  private applyDaily(daily: DailyAttendance): void {
    this.daily = daily;
    this.date = daily.date;
    this.maxDate = daily.serverDate;
    this.selectedGroupId = daily.group.id;
    this.drafts = daily.items.map(item => ({ ...item }));
    this.noteBaselines = new Map(this.drafts.map(item => [item.studentId, item.notes]));
    this.baseline = new Map(this.drafts.map(item => [item.studentId, this.serialize(item)]));
    this.errorMessage = '';
    this.errorTitle = '';
    this.conflictMessage = '';
    this.recoveryAvailable = daily.canRecover;
  }

  private clearDaily(): void {
    ++this.dailyRequest;
    this.daily = null;
    this.drafts = [];
    this.baseline.clear();
    this.noteBaselines.clear();
    this.search = '';
    this.statusFilter = null;
    this.errorMessage = '';
    this.errorTitle = '';
    this.conflictMessage = '';
    this.recoveryAvailable = false;
  }

  private validate(items: AttendanceDraft[], recovery = false): boolean {
    let firstInvalid = -1;
    items.forEach((item, index) => {
      item.invalidMessage = undefined;
      if ((item.status === 'AbsentHalfDay' || item.status === 'AbsentFullDay') && item.isExcused == null) {
        item.invalidMessage = 'Chọn có phép hoặc không phép.';
      } else if ((item.notes?.length ?? 0) > 200 && (recovery || this.notesChanged(item))) {
        item.invalidMessage = 'Ghi chú không được vượt quá 200 ký tự.';
      }
      if (item.invalidMessage && firstInvalid < 0) firstInvalid = index;
    });
    if (firstInvalid < 0) return true;
    if (!recovery) {
      this.search = '';
      this.statusFilter = null;
      setTimeout(() => this.studentCards?.get(firstInvalid)?.nativeElement.focus());
    }
    return false;
  }

  private async handleWriteError(error: unknown): Promise<void> {
    const apiError = this.setError(error, 'Không thể lưu điểm danh.');
    this.applyFieldErrors(apiError, this.drafts);
    if (apiError.status === 403) {
      this.clearDaily();
      await this.loadContext();
      return;
    }
    if (apiError.status === 409) {
      this.conflictMessage = `${apiError.message} Hãy tải lại dữ liệu trước khi tiếp tục.`;
    }
  }

  private setError(error: unknown, title = 'Không thể thực hiện yêu cầu.'): ApiError {
    const apiError = ApiError.from(error);
    this.errorTitle = title;
    this.errorMessage = apiError.message;
    this.traceId = apiError.traceId ?? '';
    return apiError;
  }

  private applyFieldErrors(apiError: ApiError, items: AttendanceDraft[], recovery = false): void {
    let firstInvalid = -1;
    Object.keys(apiError.fieldErrors).forEach(field => {
      const match = /^records\[(\d+)](?:\.|$)/i.exec(field);
      if (!match) return;
      const index = Number(match[1]);
      if (!items[index]) return;
      const normalized = field.toLocaleLowerCase('en-US');
      items[index].invalidMessage = normalized.includes('halfdaypart')
        ? 'Dữ liệu buổi nghỉ không hợp lệ. Hãy chọn lại trạng thái.'
        : normalized.includes('isexcused')
          ? 'Chọn có phép hoặc không phép.'
          : normalized.includes('notes')
            ? 'Ghi chú không hợp lệ.'
            : normalized.includes('status')
              ? 'Chọn trạng thái điểm danh hợp lệ.'
              : 'Thông tin điểm danh của học sinh chưa hợp lệ.';
      if (firstInvalid < 0) firstInvalid = index;
    });
    if (firstInvalid >= 0 && !recovery) {
      this.search = '';
      this.statusFilter = null;
      setTimeout(() => this.studentCards?.get(firstInvalid)?.nativeElement.focus());
    }
  }

  private toRecord(item: AttendanceDraft, recovery = false): SaveAttendanceRecord {
    return {
      studentId: item.studentId,
      status: item.status,
      halfDayPart: null,
      isExcused: item.status === 'AbsentFullDay' || item.status === 'AbsentHalfDay' ? item.isExcused : null,
      durationMinutes: item.status === 'OneToOneHour' ? 60 : null,
      notes: !recovery && !this.notesChanged(item)
        ? this.noteBaselines.get(item.studentId) ?? null
        : item.notes?.trim() || null
    };
  }

  private serialize(item: AttendanceDraft): string {
    return JSON.stringify(this.toRecord(item));
  }

  private notesChanged(item: AttendanceDraft): boolean {
    return (item.notes ?? null) !== (this.noteBaselines.get(item.studentId) ?? null);
  }

  private hasPendingChanges(): boolean {
    return this.dirtyCount > 0 || (this.recoveryVisible && this.recoveryDrafts.length > 0);
  }

  private async confirmDiscard(): Promise<boolean> {
    return !this.hasPendingChanges() || await confirm(
      'Các thay đổi điểm danh chưa được lưu sẽ bị mất.',
      'Bỏ thay đổi?'
    );
  }

  private recoveryDraft(candidate: RecoveryStudentCandidate | undefined, id: string): AttendanceDraft {
    return {
      entryId: null,
      studentId: id,
      studentCode: candidate?.studentCode ?? '',
      fullName: candidate?.fullName ?? 'Học sinh',
      nickName: candidate?.nickName ?? '',
      status: 'Present',
      halfDayPart: null,
      isExcused: null,
      durationMinutes: null,
      notes: null,
      updatedAt: null
    };
  }

  private pageSize(options: { take?: number }): number {
    return Math.min(options.take ?? 20, 100);
  }

  private page(options: { skip?: number; take?: number }): number {
    return Math.floor((options.skip ?? 0) / this.pageSize(options)) + 1;
  }

  private searchOf(options: { searchValue?: unknown }): string | undefined {
    return String(options.searchValue ?? '').trim() || undefined;
  }

  private rejectCandidate(error: unknown): never {
    throw new Error(ApiError.from(error).message);
  }

  private scrollCardsToTop(): void {
    document.querySelector('.attendance-list')?.scrollTo({ top: 0, behavior: 'smooth' });
  }
}

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    DxButtonModule,
    DxDateBoxModule,
    DxLoadIndicatorModule,
    DxPopupModule,
    DxSelectBoxModule,
    DxTagBoxModule,
    DxListModule,
    DxTextBoxModule
  ],
  declarations: [AttendanceComponent],
  exports: [AttendanceComponent]
})
export class AttendanceModule { }
