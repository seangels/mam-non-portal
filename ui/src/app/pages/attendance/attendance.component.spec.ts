import { of, throwError } from 'rxjs';
import { ApiError } from '../../core/models/api-error';
import { DailyAttendance } from '../../core/models/api.models';
import { AttendanceService } from '../../core/services/attendance.service';
import { AuthService } from '../../shared/services';
import { AttendanceComponent } from './attendance.component';

describe('AttendanceComponent editor', () => {
  let attendance: jasmine.SpyObj<AttendanceService>;
  let auth: jasmine.SpyObj<AuthService>;
  let component: AttendanceComponent;

  beforeEach(() => {
    attendance = jasmine.createSpyObj<AttendanceService>('AttendanceService', [
      'context', 'daily', 'create', 'update', 'recover',
      'recoveryGroups', 'recoveryStudents', 'recoveryTeachers'
    ]);
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['hasRole']);
    auth.hasRole.and.callFake((...roles) => roles.includes('Admin'));
    component = new AttendanceComponent(attendance, auth);
  });

  it('keeps notes but clears conditional fields when Present is selected', () => {
    applyDaily(component, daily('Saved'));
    const item = component.drafts[0];
    item.status = 'AbsentHalfDay';
    item.halfDayPart = 'Morning';
    item.isExcused = true;
    item.notes = 'Theo dõi thêm';

    component.onStatusChange(item, 'Present');

    expect(item.halfDayPart).toBeNull();
    expect(item.isExcused).toBeNull();
    expect(item.durationMinutes).toBeNull();
    expect(item.notes).toBe('Theo dõi thêm');
  });

  it('allows first save with zero dirty cards and posts the full roster', async () => {
    const missing = daily('Missing');
    missing.items[1].status = 'OneToOneHour';
    missing.items[1].durationMinutes = 60;
    const saved = daily('Saved');
    applyDaily(component, missing);
    attendance.create.and.returnValue(of(saved));

    expect(component.dirtyCount).toBe(0);
    expect(component.saveDisabled).toBeFalse();
    await component.save();

    const request = attendance.create.calls.mostRecent().args[0];
    expect(request.expectedSnapshotVersion).toBe(7);
    expect(request.records.length).toBe(2);
    expect(request.records.map(record => record.status)).toEqual(['Present', 'OneToOneHour']);
    expect(request.records[1].durationMinutes).toBe(60);
    expect(component.daily?.sheetState).toBe('Saved');
    expect(component.dirtyCount).toBe(0);
  });

  it('puts the full saved roster and resets the baseline after a change', async () => {
    const original = daily('Saved');
    applyDaily(component, original);
    component.onStatusChange(component.drafts[0], 'AbsentFullDay');
    component.drafts[0].isExcused = true;
    const response = daily('Saved');
    response.sheetVersion = 4;
    response.items[0] = { ...component.drafts[0], updatedAt: '2026-08-01T10:00:00Z' };
    attendance.update.and.returnValue(of(response));

    expect(component.dirtyCount).toBe(1);
    await component.save();

    const [sheetId, request] = attendance.update.calls.mostRecent().args;
    expect(sheetId).toBe('sheet-1');
    expect(request.expectedVersion).toBe(3);
    expect(request.records.length).toBe(2);
    expect(request.records[0].isExcused).toBeTrue();
    expect(request.records[1].status).toBe('Present');
    expect(component.dirtyCount).toBe(0);
  });

  it('keeps hidden card changes and whole-roster summary while filtering locally', () => {
    applyDaily(component, daily('Saved'));
    component.onStatusChange(component.drafts[0], 'OneToOneHour');
    component.search = 'Bình';

    expect(component.filteredDrafts.map(item => item.studentId)).toEqual(['student-2']);
    expect(component.dirtyCount).toBe(1);
    expect(component.summary.total).toBe(2);
    expect(component.summary.oneToOne).toBe(1);
  });

  it('persists Unmarked, clears conditional fields and exposes its filter and summary', async () => {
    const original = daily('Saved');
    applyDaily(component, original);
    const item = component.drafts[0];
    item.halfDayPart = 'Morning';
    item.isExcused = true;
    item.durationMinutes = 60;
    item.notes = 'Cần xác nhận sau';
    component.onStatusChange(item, 'Unmarked');
    component.statusFilter = 'Unmarked';
    attendance.update.and.returnValue(of({
      ...original,
      items: [{ ...item }, original.items[1]],
      summary: { rosterTotal: 2, present: 1, absent: 0, oneToOne: 0, unmarked: 1 }
    }));

    expect(item.halfDayPart).toBeNull();
    expect(item.isExcused).toBeNull();
    expect(item.durationMinutes).toBeNull();
    expect(item.notes).toBe('Cần xác nhận sau');
    expect(component.filteredDrafts.map(value => value.studentId)).toEqual(['student-1']);
    expect(component.summary.unmarked).toBe(1);

    await component.save();

    const record = attendance.update.calls.mostRecent().args[1].records[0];
    expect(record.status).toBe('Unmarked');
    expect(record.halfDayPart).toBeNull();
    expect(record.isExcused).toBeNull();
    expect(record.durationMinutes).toBeNull();
  });

  it('always sends a new half-day absence without a morning or afternoon part', async () => {
    const original = daily('Saved');
    applyDaily(component, original);
    const item = component.drafts[0];
    item.halfDayPart = 'Morning';
    component.onStatusChange(item, 'AbsentHalfDay');
    item.isExcused = true;
    attendance.update.and.returnValue(of({ ...original, items: [{ ...item }, original.items[1]] }));

    await component.save();

    const record = attendance.update.calls.mostRecent().args[1].records[0];
    expect(record.status).toBe('AbsentHalfDay');
    expect(record.halfDayPart).toBeNull();
    expect(record.isExcused).toBeTrue();
    expect(record.durationMinutes).toBeNull();
  });

  it('round-trips an untouched legacy note over 200 characters during a full-roster update', async () => {
    const original = daily('Saved');
    const legacyNote = `  ${'Ghi chú cũ '.repeat(22)}  `;
    original.items[0].notes = legacyNote;
    applyDaily(component, original);
    component.onStatusChange(component.drafts[1], 'Unmarked');
    attendance.update.and.returnValue(of({ ...original, items: component.drafts.map(item => ({ ...item })) }));

    expect(component.isLegacyNote(component.drafts[0])).toBeTrue();
    await component.save();

    expect(attendance.update).toHaveBeenCalled();
    expect(attendance.update.calls.mostRecent().args[1].records[0].notes).toBe(legacyNote);
  });

  it('blocks an edited note over the 200-character UI limit', async () => {
    applyDaily(component, daily('Saved'));
    component.drafts[0].notes = 'x'.repeat(201);

    await component.save();

    expect(attendance.update).not.toHaveBeenCalled();
    expect(component.drafts[0].invalidMessage).toContain('200 ký tự');
  });

  it('uses only nickname and student code for the main card accessible identity', () => {
    applyDaily(component, daily('Saved'));

    const identity = component.cardIdentity(component.drafts[0]);

    expect(identity).toBe('Bé An · HS-01');
    expect(identity).not.toContain('Nguyễn An');
  });

  it('allows an administrator to start recovery for a past date without a current group', () => {
    component.date = '2026-08-01';
    component.maxDate = '2026-08-11';
    component.selectedGroupId = null;

    expect(component.canStartHistoricalRecovery).toBeTrue();
    component.openRecovery();

    expect(component.recoveryVisible).toBeTrue();
    expect(component.recoveryGroupId).toBeNull();
  });

  it('treats an empty scheduled roster as a read-only state without a save action', () => {
    const value = daily('Missing');
    value.items = [];
    value.summary = { rosterTotal: 0, present: 0, absent: 0, oneToOne: 0, unmarked: 0 };
    value.canCreate = false;
    value.readOnlyReason = 'NoScheduledStudents';

    applyDaily(component, value);

    expect(component.noScheduledStudents).toBeTrue();
    expect(component.canModify).toBeFalse();
    expect(component.saveDisabled).toBeTrue();
    expect(component.readOnlyText).toContain('Không có học sinh có lịch học');
  });

  it('does not send a mutation for a read-only saved sheet', async () => {
    const value = daily('Saved');
    value.canEdit = false;
    value.readOnlyReason = 'EditWindowExpired';
    applyDaily(component, value);
    component.onStatusChange(component.drafts[0], 'Unmarked');

    await component.save();

    expect(component.canModify).toBeFalse();
    expect(attendance.update).not.toHaveBeenCalled();
  });

  it('keeps the full draft and surfaces reload guidance after a version conflict', async () => {
    applyDaily(component, daily('Saved'));
    component.onStatusChange(component.drafts[0], 'Unmarked');
    attendance.update.and.returnValue(throwError(() => new ApiError(
      'Phiếu điểm danh đã được cập nhật.', 409, {}, undefined, 'SheetVersionConflict', 4
    )));

    await component.save();

    expect(component.drafts[0].status).toBe('Unmarked');
    expect(component.dirtyCount).toBe(1);
    expect(component.conflictMessage).toContain('Hãy tải lại dữ liệu');
    expect(attendance.daily).not.toHaveBeenCalled();
  });

  it('keeps historical recovery manual and defaults a selected candidate to Present', () => {
    (component as any).studentCandidates.set('student-old', {
      id: 'student-old', studentCode: 'HS-CU', fullName: 'Học sinh cũ', nickName: '',
      status: 'Inactive', isDeleted: true, currentGroupId: null
    });

    component.onRecoveryStudentsChanged({ value: ['student-old'] });

    expect(component.recoveryDrafts.length).toBe(1);
    expect(component.recoveryDrafts[0].status).toBe('Present');
    expect(component.recoveryDrafts[0].durationMinutes).toBeNull();
  });

  it('sends historical recovery half-day records with permission and no day part', async () => {
    (component as any).studentCandidates.set('student-old', {
      id: 'student-old', studentCode: 'HS-CU', fullName: 'Học sinh cũ', nickName: 'Bé Cũ',
      status: 'Inactive', isDeleted: true, currentGroupId: null
    });
    component.onRecoveryStudentsChanged({ value: ['student-old'] });
    component.onRecoveryStatusChange(component.recoveryDrafts[0], 'AbsentHalfDay');
    component.recoveryDrafts[0].halfDayPart = 'Afternoon';
    component.recoveryDrafts[0].isExcused = false;
    component.recoveryGroupId = 'group-old';
    component.recoveryTeacherId = 'teacher-old';
    component.recoveryReason = 'Bổ sung phiếu bị thiếu';
    component.recoveryAcknowledged = true;
    attendance.recover.and.returnValue(of(daily('Saved')));

    await component.saveRecovery();

    const record = attendance.recover.calls.mostRecent().args[0].records[0];
    expect(record.status).toBe('AbsentHalfDay');
    expect(record.halfDayPart).toBeNull();
    expect(record.isExcused).toBeFalse();
    expect(record.durationMinutes).toBeNull();
  });

  it('blocks a recovery note over the 200-character UI limit', async () => {
    (component as any).studentCandidates.set('student-old', {
      id: 'student-old', studentCode: 'HS-CU', fullName: 'Học sinh cũ', nickName: 'Bé Cũ',
      status: 'Inactive', isDeleted: true, currentGroupId: null
    });
    component.onRecoveryStudentsChanged({ value: ['student-old'] });
    component.recoveryDrafts[0].notes = 'x'.repeat(201);
    component.recoveryGroupId = 'group-old';
    component.recoveryTeacherId = 'teacher-old';
    component.recoveryReason = 'Bổ sung phiếu bị thiếu';
    component.recoveryAcknowledged = true;

    await component.saveRecovery();

    expect(attendance.recover).not.toHaveBeenCalled();
    expect(component.recoveryDrafts[0].invalidMessage).toContain('200 ký tự');
  });
});

function applyDaily(component: AttendanceComponent, value: DailyAttendance): void {
  (component as any).applyDaily(value);
}

function daily(state: 'Missing' | 'Saved'): DailyAttendance {
  return {
    date: '2026-08-01',
    serverDate: '2026-08-11',
    group: { id: 'group-1', code: 'MAM-01', name: 'Mầm 1' },
    sheetState: state,
    sheetId: state === 'Saved' ? 'sheet-1' : null,
    sheetVersion: state === 'Saved' ? 3 : null,
    snapshotSource: state === 'Saved' ? 'CurrentSnapshot' : null,
    currentSnapshotVersion: 7,
    sourceSnapshotVersion: state === 'Saved' ? 7 : null,
    canCreate: state === 'Missing',
    canEdit: state === 'Saved',
    canRecover: false,
    readOnlyReason: null,
    summary: { rosterTotal: 2, present: 2, absent: 0, oneToOne: 0, unmarked: 0 },
    items: [
      {
        entryId: state === 'Saved' ? 'entry-1' : null,
        studentId: 'student-1', studentCode: 'HS-01', fullName: 'Nguyễn An', nickName: 'Bé An',
        status: 'Present', halfDayPart: null, isExcused: null, durationMinutes: null,
        notes: null, updatedAt: null
      },
      {
        entryId: state === 'Saved' ? 'entry-2' : null,
        studentId: 'student-2', studentCode: 'HS-02', fullName: 'Trần Bình', nickName: 'Bé Bình',
        status: 'Present', halfDayPart: null, isExcused: null, durationMinutes: null,
        notes: null, updatedAt: null
      }
    ]
  };
}
