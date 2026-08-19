import { of } from 'rxjs';
import { Router } from '@angular/router';
import { StudentGroupsService } from '../../core/services/student-groups.service';
import { TeachersService } from '../../core/services/teachers.service';
import { TeachersComponent } from './teachers.component';

describe('TeachersComponent remote list', () => {
  let teachers: jasmine.SpyObj<TeachersService>;
  let groups: jasmine.SpyObj<StudentGroupsService>;
  let router: jasmine.SpyObj<Router>;
  let component: TeachersComponent;

  beforeEach(() => {
    teachers = jasmine.createSpyObj<TeachersService>('TeachersService', ['list', 'get', 'delete']);
    groups = jasmine.createSpyObj<StudentGroupsService>('StudentGroupsService', ['list', 'get']);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    teachers.list.and.returnValue(of({
      items: [],
      pagination: { page: 2, pageSize: 20, totalItems: 37, totalPages: 2 }
    }));
    component = new TeachersComponent(teachers, groups, router);
  });

  it('maps DevExtreme paging/filter/sort and trusts the server total', async () => {
    component.search = ' Nguyễn ';
    component.statusFilter = 'Active';
    component.groupId = 'group-1';

    const result = await (component.dataSource as any).load({
      skip: 20,
      take: 20,
      sort: [{ selector: 'teacherCode', desc: true }]
    });

    expect(teachers.list).toHaveBeenCalledWith({
      page: 2,
      pageSize: 20,
      search: 'Nguyễn',
      status: 'Active',
      groupId: 'group-1',
      unassigned: undefined,
      sortBy: 'teacherCode',
      sortOrder: 'desc'
    });
    expect(result.totalCount).toBe(37);
  });

  it('never sends groupId together with unassigned=true', () => {
    component.groupId = 'group-1';
    component.unassigned = true;
    component.onUnassignedChanged();
    expect(component.groupId).toBeNull();

    component.unassigned = true;
    component.groupId = 'group-2';
    component.onGroupChanged();
    expect(component.unassigned).toBeFalse();
  });
});
