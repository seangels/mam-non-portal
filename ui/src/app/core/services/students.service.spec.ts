import { of } from 'rxjs';
import { ApiClient } from './api-client.service';
import { StudentsService } from './students.service';

describe('StudentsService', () => {
  let api: jasmine.SpyObj<ApiClient>;
  let service: StudentsService;

  beforeEach(() => {
    api = jasmine.createSpyObj<ApiClient>('ApiClient', ['get', 'post', 'put', 'delete']);
    api.get.and.returnValue(of({}));
    api.post.and.returnValue(of({}));
    api.put.and.returnValue(of({}));
    api.delete.and.returnValue(of(undefined));
    service = new StudentsService(api);
  });

  it('uses schedule filters with server paging', () => {
    const query = {
      page: 2,
      pageSize: 20,
      groupId: 'group-1',
      studyMode: 'FullDay' as const,
      studyWeekday: 'Monday' as const,
      sortBy: 'studyMode',
      sortOrder: 'asc' as const
    };

    service.list(query).subscribe();

    expect(api.get).toHaveBeenCalledWith('students', query);
  });

  it('uses explicit schedule bodies for create and full update', () => {
    const create = {
      studentCode: 'HS-01',
      fullName: 'Nguyễn An',
      nickName: 'Bé An',
      dateOfBirth: '2021-05-10',
      gender: null,
      status: 'Active' as const,
      guardianName: null,
      guardianPhone: null,
      note: null,
      studySchedule: { mode: 'FullDay' as const, weekdays: ['Monday' as const] }
    };

    service.create(create).subscribe();
    expect(api.post).toHaveBeenCalledWith('students', create);

    const update = { ...create, expectedVersion: 3 };
    service.update('student-1', update).subscribe();
    expect(api.put).toHaveBeenCalledWith('students/student-1', update);
  });

  it('uses versioned group and delete commands', () => {
    const assignment = { groupId: 'group-1', expectedVersion: 4 };

    service.assignGroup('student-1', assignment).subscribe();
    expect(api.put).toHaveBeenCalledWith('students/student-1/group', assignment);

    service.delete('student-1', 5).subscribe();
    expect(api.delete).toHaveBeenCalledWith('students/student-1', { expectedVersion: 5 });
  });
});
