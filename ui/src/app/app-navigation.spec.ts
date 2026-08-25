import { buildNavigation } from './app-navigation';

describe('buildNavigation', () => {
  it('shows teacher management to both manager roles and admin accounts only to SuperAdmin', () => {
    expect(buildNavigation('Admin').map(item => item.path)).toEqual([
      '/home', '/attendance', '/assessments', '/assessment-sheets', '/teachers', '/students', '/student-groups'
    ]);
    expect(buildNavigation('SuperAdmin').map(item => item.path)).toEqual([
      '/home', '/attendance', '/assessments', '/assessment-sheets', '/teachers', '/students', '/student-groups', '/users'
    ]);
  });

  it('keeps teachers out of management navigation while showing assessment features', () => {
    expect(buildNavigation('Teacher').map(item => item.path)).toEqual([
      '/home', '/attendance', '/assessments', '/assessment-sheets'
    ]);
  });
});
