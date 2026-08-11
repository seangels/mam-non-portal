import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { Student, StudentGroup, Teacher } from '../../core/models/api.models';
import { StudentGroupsService } from '../../core/services/student-groups.service';
import { StudentsService } from '../../core/services/students.service';
import { TeachersService } from '../../core/services/teachers.service';
import { StudentGroupsComponent } from './student-groups.component';

describe('StudentGroupsComponent teacher boundaries', () => {
  let groups: jasmine.SpyObj<StudentGroupsService>;
  let students: jasmine.SpyObj<StudentsService>;
  let teachers: jasmine.SpyObj<TeachersService>;
  let component: StudentGroupsComponent;

  beforeEach(() => {
    groups = jasmine.createSpyObj<StudentGroupsService>('StudentGroupsService', ['list', 'get', 'assignTeacher']);
    students = jasmine.createSpyObj<StudentsService>('StudentsService', ['list', 'get', 'assignGroup']);
    teachers = jasmine.createSpyObj<TeachersService>('TeachersService', ['list', 'get', 'updateAttendancePolicy']);
    const route = {
      snapshot: { queryParamMap: { get: () => null } }
    } as unknown as ActivatedRoute;
    component = new StudentGroupsComponent(groups, teachers, students, route);
  });

  it('sends the selected Teacher version when updating attendance policy', async () => {
    const teacher = teacherRow();
    teachers.updateAttendancePolicy.and.returnValue(of({ ...teacher, note: null, responsibleGroups: [] }));
    component.openPolicy(teacher);
    component.policyEditor.attendanceEditWindowDays = 5;

    await component.savePolicy(new Event('submit'));

    expect(teachers.updateAttendancePolicy).toHaveBeenCalledWith('teacher-1', {
      attendanceEditWindowDays: 5,
      expectedVersion: 4
    });
  });

  it('keeps Teacher assignment on StudentGroupsService', async () => {
    groups.assignTeacher.and.returnValue(of({} as StudentGroup));
    component.selectedGroup = {
      id: 'group-1', code: 'MAM-1', name: 'Mầm 1', status: 'Active', studentCount: 10,
      snapshotVersion: 2, createdAt: '', updatedAt: ''
    };
    component.selectedTeacherId = 'teacher-1';

    await component.saveTeacherAssignment();

    expect(groups.assignTeacher).toHaveBeenCalledWith('group-1', { teacherId: 'teacher-1' });
  });

  it('uses the canonical versioned Student service when adding a roster member', async () => {
    const student = studentRow();
    students.get.and.returnValue(of(student));
    students.assignGroup.and.returnValue(of({ ...student, groupId: 'group-1', groupCode: 'MAM-1', groupName: 'Mầm 1', version: 5 }));
    students.list.and.returnValue(of({ items: [], pagination: { page: 1, pageSize: 100, totalItems: 0, totalPages: 0 } }));
    component.selectedGroup = {
      id: 'group-1', code: 'MAM-1', name: 'Mầm 1', status: 'Active', studentCount: 1,
      snapshotVersion: 2, createdAt: '', updatedAt: ''
    };
    component.selectedStudentId = student.id;

    await component.assignStudent();

    expect(students.assignGroup).toHaveBeenCalledWith('student-1', {
      groupId: 'group-1',
      expectedVersion: 4
    });
  });
});

function teacherRow(): Teacher {
  return {
    id: 'teacher-1', userId: 'user-1', teacherCode: 'GV-01', fullName: 'Nguyễn An',
    email: 'an@example.com', phoneNumber: null, status: 'Active', attendanceEditWindowDays: 7,
    responsibleGroupCount: 1, createdAt: '', updatedAt: '', version: 4
  };
}

function studentRow(): Student {
  return {
    id: 'student-1', studentCode: 'HS-01', fullName: 'Nguyễn An', nickName: 'Bé An',
    dateOfBirth: '2021-05-10', gender: null, status: 'Active', guardianName: null,
    guardianPhone: null, note: null, groupId: null, groupCode: null, groupName: null,
    studySchedule: { mode: 'FullDay', weekdays: ['Monday', 'Tuesday'] },
    createdAt: '', updatedAt: '', version: 4
  };
}
