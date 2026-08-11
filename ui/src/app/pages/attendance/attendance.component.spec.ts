import { of } from 'rxjs';
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
    const saved = daily('Saved');
    applyDaily(component, missing);
    attendance.create.and.returnValue(of(saved));

    expect(component.dirtyCount).toBe(0);
    expect(component.saveDisabled).toBeFalse();
    await component.save();

    const request = attendance.create.calls.mostRecent().args[0];
    expect(request.expectedSnapshotVersion).toBe(7);
    expect(request.records.length).toBe(2);
    expect(request.records.every(record => record.status === 'Present')).toBeTrue();
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

  it('allows an administrator to start recovery for a past date without a current group', () => {
    component.date = '2026-08-01';
    component.maxDate = '2026-08-11';
    component.selectedGroupId = null;

    expect(component.canStartHistoricalRecovery).toBeTrue();
    component.openRecovery();

    expect(component.recoveryVisible).toBeTrue();
    expect(component.recoveryGroupId).toBeNull();
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
    summary: { rosterTotal: 2, present: 2, absent: 0, oneToOne: 0 },
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
