import { of } from 'rxjs';
import { ApiClient } from './api-client.service';
import { AttendanceService } from './attendance.service';

describe('AttendanceService', () => {
  let api: jasmine.SpyObj<ApiClient>;
  let service: AttendanceService;

  beforeEach(() => {
    api = jasmine.createSpyObj<ApiClient>('ApiClient', ['get', 'post', 'put']);
    api.get.and.returnValue(of({}));
    api.post.and.returnValue(of({}));
    api.put.and.returnValue(of({}));
    service = new AttendanceService(api);
  });

  it('uses the context and daily read contract', () => {
    service.context('2026-08-01').subscribe();
    expect(api.get).toHaveBeenCalledWith('attendance/context', { date: '2026-08-01' });

    service.daily('2026-08-01', 'group-1').subscribe();
    expect(api.get).toHaveBeenCalledWith('attendance/daily', { date: '2026-08-01', groupId: 'group-1' });
  });

  it('uses full create, update and historical recovery endpoints', () => {
    const record = {
      studentId: 'student-1', status: 'Present' as const, halfDayPart: null,
      isExcused: null, durationMinutes: null, notes: null
    };
    const create = { groupId: 'group-1', date: '2026-08-01', expectedSnapshotVersion: 2, records: [record] };
    service.create(create).subscribe();
    expect(api.post).toHaveBeenCalledWith('attendance/sheets', create);

    const update = { expectedVersion: 3, records: [record] };
    service.update('sheet-1', update).subscribe();
    expect(api.put).toHaveBeenCalledWith('attendance/sheets/sheet-1', update);

    const recovery = {
      ...create,
      responsibleTeacherId: 'teacher-1',
      acknowledgeHistoricalSnapshot: true as const,
      recoveryReason: 'Đối chiếu phiếu giấy'
    };
    service.recover(recovery).subscribe();
    expect(api.post).toHaveBeenCalledWith('attendance/sheets/historical-recovery', recovery);
  });

  it('uses all three paged historical candidate endpoints', () => {
    const query = { page: 1, pageSize: 20, search: 'an' };
    service.recoveryGroups(query).subscribe();
    service.recoveryStudents(query).subscribe();
    service.recoveryTeachers(query).subscribe();

    expect(api.get).toHaveBeenCalledWith('attendance/historical-recovery/group-candidates', query);
    expect(api.get).toHaveBeenCalledWith('attendance/historical-recovery/student-candidates', query);
    expect(api.get).toHaveBeenCalledWith('attendance/historical-recovery/teacher-candidates', query);
  });
});
