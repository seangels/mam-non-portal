import { of, throwError } from 'rxjs';
import { ApiError } from '../../core/models/api-error';
import { Student } from '../../core/models/api.models';
import { StudentGroupsService } from '../../core/services/student-groups.service';
import { StudentsService } from '../../core/services/students.service';
import { StudentsComponent } from './students.component';

describe('StudentsComponent schedule and remote list', () => {
  let students: jasmine.SpyObj<StudentsService>;
  let groups: jasmine.SpyObj<StudentGroupsService>;
  let component: StudentsComponent;

  beforeEach(() => {
    students = jasmine.createSpyObj<StudentsService>('StudentsService', [
      'list', 'get', 'create', 'update', 'assignGroup', 'delete'
    ]);
    groups = jasmine.createSpyObj<StudentGroupsService>('StudentGroupsService', ['list', 'get']);
    students.list.and.returnValue(of({
      items: [studentRow()],
      pagination: { page: 2, pageSize: 20, totalItems: 37, totalPages: 2 }
    }));
    component = new StudentsComponent(students, groups);
  });

  it('maps all filters and nested schedule sorting to the server contract', async () => {
    component.search = ' Nguyễn ';
    component.groupIdFilter = 'group-1';
    component.studyModeFilter = 'OneToOne';
    component.studyWeekdayFilter = 'Saturday';

    const result = await (component.dataSource as any).load({
      skip: 20,
      take: 20,
      sort: [{ selector: 'studySchedule.mode', desc: true }]
    });

    expect(students.list).toHaveBeenCalledWith(jasmine.objectContaining({
      page: 2,
      pageSize: 20,
      search: 'Nguyễn',
      groupId: 'group-1',
      unassigned: undefined,
      studyMode: 'OneToOne',
      studyWeekday: 'Saturday',
      sortBy: 'studyMode',
      sortOrder: 'desc'
    }));
    expect(result.data.length).toBe(1);
    expect(result.totalCount).toBe(37);
  });

  it('never combines a group filter with unassigned=true', () => {
    component.groupIdFilter = 'group-1';
    component.onUnassignedChanged(true);
    expect(component.groupIdFilter).toBeNull();

    component.unassignedFilter = true;
    component.onGroupFilterChanged('group-2');
    expect(component.unassignedFilter).toBeFalse();
  });

  it('sends canonical weekdays and expectedVersion without group response fields', async () => {
    const current = studentRow();
    students.update.and.returnValue(of({ ...current, version: 5 }));
    component.openEdit(current);
    component.editor.studyWeekdays = ['Saturday', 'Monday', 'Saturday', 'Wednesday'];

    await component.save(new Event('submit'));

    const request = students.update.calls.mostRecent().args[1];
    expect(request.expectedVersion).toBe(4);
    expect(request.studySchedule).toEqual({
      mode: 'FullDay',
      weekdays: ['Monday', 'Wednesday', 'Saturday']
    });
    expect(request).not.toEqual(jasmine.objectContaining({ groupId: jasmine.anything() }));
    expect(request).not.toEqual(jasmine.objectContaining({ version: jasmine.anything() }));
  });

  it('requires at least one weekday before calling the API', async () => {
    component.openEdit(studentRow());
    component.editor.studyWeekdays = [];

    await component.save(new Event('submit'));

    expect(students.update).not.toHaveBeenCalled();
    expect(component.scheduleWeekdaysError).toContain('ít nhất một ngày');
  });

  it('renders Vietnamese schedule and group summaries', () => {
    const student = studentRow();
    expect(component.groupText(student)).toBe('MAM-1 · Mầm 1');
    expect(component.scheduleText(student)).toBe('Học cả ngày · T2, T4, T7');
  });

  it('loads assignment groups remotely and marks current or full groups disabled', async () => {
    const current = groupRow('group-1', 20);
    const full = groupRow('group-2', 100);
    const available = groupRow('group-3', 12);
    groups.list.and.returnValue(of({
      items: [current, full, available],
      pagination: { page: 1, pageSize: 20, totalItems: 3, totalPages: 1 }
    }));
    component.openGroupAssignment(studentRow());

    const result = await (component.assignmentGroupDataSource as any).load({
      skip: 0, take: 20, searchValue: 'mầm'
    });

    expect(groups.list).toHaveBeenCalledWith(jasmine.objectContaining({
      page: 1, pageSize: 20, search: 'mầm', status: 'Active'
    }));
    expect(result.totalCount).toBe(3);
    expect(result.data.map((group: any) => group.disabled)).toEqual([true, true, false]);
  });

  it('keeps the editor draft on a nested schedule validation error', async () => {
    students.update.and.returnValue(throwError(() => new ApiError(
      'Một hoặc nhiều thông tin chưa hợp lệ.',
      400,
      { 'studySchedule.weekdays': ['Invalid'] },
      undefined,
      'ValidationFailed'
    )));
    component.openEdit(studentRow());
    component.editor.fullName = 'Tên đang sửa';

    await component.save(new Event('submit'));

    expect(component.editorVisible).toBeTrue();
    expect(component.editor.fullName).toBe('Tên đang sửa');
    expect(component.scheduleWeekdaysError).toContain('Chủ nhật');
    expect(component.editorDirty).toBeTrue();
  });

  it('cancels hiding synchronously and keeps a dirty editor open when discard is declined', async () => {
    component.openEdit(studentRow());
    component.editor.fullName = 'Tên đang sửa';
    const confirmDiscard = replaceDiscardConfirmation(component, false);
    const event: { cancel?: boolean } = {};

    component.onEditorHiding(event);

    expect(event.cancel).toBeTrue();
    await settlePromises();
    expect(component.editorVisible).toBeTrue();
    expect(confirmDiscard).toHaveBeenCalledTimes(1);
  });

  it('closes after discard is confirmed and bypasses exactly one repeated hiding event', async () => {
    component.openEdit(studentRow());
    component.editor.fullName = 'Tên đang sửa';
    const confirmDiscard = replaceDiscardConfirmation(component, true);
    const firstEvent: { cancel?: boolean } = {};

    component.onEditorHiding(firstEvent);

    expect(firstEvent.cancel).toBeTrue();
    await settlePromises();
    expect(component.editorVisible).toBeFalse();

    component.editorVisible = true;
    const repeatedEvent: { cancel?: boolean } = {};
    component.onEditorHiding(repeatedEvent);

    expect(repeatedEvent.cancel).not.toBeTrue();
    expect(confirmDiscard).toHaveBeenCalledTimes(1);
  });
});

function replaceDiscardConfirmation(component: StudentsComponent, result: boolean): jasmine.Spy {
  const confirmation = jasmine.createSpy('confirmEditorDiscard').and.returnValue(Promise.resolve(result));
  const testable = component as unknown as { confirmEditorDiscard: () => Promise<boolean> };
  testable.confirmEditorDiscard = confirmation;
  return confirmation;
}

async function settlePromises(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
}

function studentRow(): Student {
  return {
    id: 'student-1',
    studentCode: 'HS-01',
    fullName: 'Nguyễn An',
    nickName: 'Bé An',
    dateOfBirth: '2021-05-10',
    gender: null,
    status: 'Active',
    guardianName: null,
    guardianPhone: null,
    note: null,
    groupId: 'group-1',
    groupCode: 'MAM-1',
    groupName: 'Mầm 1',
    responsibleTeacherName: 'Cô Lan',
    studySchedule: { mode: 'FullDay', weekdays: ['Monday', 'Wednesday', 'Saturday'] },
    createdAt: '',
    updatedAt: '',
    version: 4
  };
}

function groupRow(id: string, studentCount: number) {
  return {
    id,
    code: id.toUpperCase(),
    name: `Nhóm ${id}`,
    status: 'Active' as const,
    responsibleTeacherId: null,
    responsibleTeacherName: null,
    studentCount,
    snapshotVersion: 1,
    createdAt: '',
    updatedAt: ''
  };
}
