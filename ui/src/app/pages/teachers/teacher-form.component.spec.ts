import { buildCreateTeacherRequest, buildUpdateTeacherRequest, TeacherEditor } from './teacher-form.component';

describe('Teacher form request mapping', () => {
  const editor: TeacherEditor = {
    teacherCode: ' gv-01 ',
    fullName: ' Nguyễn An ',
    email: ' an@example.com ',
    phoneNumber: ' ',
    status: 'Active',
    note: ' ',
    password: 'Strong#Pass123',
    confirmPassword: 'Strong#Pass123'
  };

  it('normalizes the user-entered code and clears nullable fields on create', () => {
    expect(buildCreateTeacherRequest(editor)).toEqual({
      teacherCode: 'GV-01',
      fullName: 'Nguyễn An',
      email: 'an@example.com',
      phoneNumber: null,
      status: 'Active',
      password: 'Strong#Pass123',
      note: null
    });
  });

  it('builds a full update with expectedVersion and no password or policy', () => {
    const request = buildUpdateTeacherRequest(editor, 7);
    expect(request.expectedVersion).toBe(7);
    expect(request.teacherCode).toBe('GV-01');
    expect(request.phoneNumber).toBeNull();
    expect(request.note).toBeNull();
    expect('password' in request).toBeFalse();
    expect('attendanceEditWindowDays' in request).toBeFalse();
  });
});
