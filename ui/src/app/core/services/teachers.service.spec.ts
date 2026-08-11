import { of } from 'rxjs';
import { ApiClient } from './api-client.service';
import { TeachersService } from './teachers.service';

describe('TeachersService', () => {
  let api: jasmine.SpyObj<ApiClient>;
  let service: TeachersService;

  beforeEach(() => {
    api = jasmine.createSpyObj<ApiClient>('ApiClient', ['get', 'post', 'put', 'delete']);
    api.get.and.returnValue(of({}));
    api.post.and.returnValue(of({}));
    api.put.and.returnValue(of({}));
    api.delete.and.returnValue(of(undefined));
    service = new TeachersService(api);
  });

  it('uses the paged list and detail endpoints', () => {
    const query = {
      page: 2, pageSize: 20, search: 'nguyen', status: 'Active' as const,
      groupId: 'group-1', sortBy: 'fullName', sortOrder: 'asc' as const
    };
    service.list(query).subscribe();
    service.get('teacher-1').subscribe();

    expect(api.get).toHaveBeenCalledWith('teachers', query);
    expect(api.get).toHaveBeenCalledWith('teachers/teacher-1');
  });

  it('uses canonical create and full update payloads', () => {
    const create = {
      teacherCode: 'GV01', fullName: 'Nguyễn An', email: 'an@example.com',
      phoneNumber: null, status: 'Active' as const, password: 'Strong#Pass123', note: null
    };
    service.create(create).subscribe();
    expect(api.post).toHaveBeenCalledWith('teachers', create);

    const update = {
      teacherCode: 'GV02', fullName: 'Nguyễn An', email: 'an@example.com',
      phoneNumber: null, status: 'Inactive' as const, note: null, expectedVersion: 3
    };
    service.update('teacher-1', update).subscribe();
    expect(api.put).toHaveBeenCalledWith('teachers/teacher-1', update);
  });

  it('uses versioned policy/delete and the linked user password endpoint', () => {
    const policy = { attendanceEditWindowDays: 5, expectedVersion: 4 };
    service.updateAttendancePolicy('teacher-1', policy).subscribe();
    expect(api.put).toHaveBeenCalledWith('teachers/teacher-1/attendance-policy', policy);

    const password = { password: 'Strong#Pass123' };
    service.changePassword('user-1', password).subscribe();
    expect(api.put).toHaveBeenCalledWith('users/user-1/password', password);

    service.delete('teacher-1', 5).subscribe();
    expect(api.delete).toHaveBeenCalledWith('teachers/teacher-1', { expectedVersion: 5 });
  });
});
