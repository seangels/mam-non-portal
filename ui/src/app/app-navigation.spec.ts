import { buildNavigation } from './app-navigation';

describe('buildNavigation', () => {
  it('shows teacher management to both manager roles and admin accounts only to SuperAdmin', () => {
    expect(buildNavigation('Admin').map(item => item.path)).toEqual(['/home', '/attendance', '/teachers', '/students', '/student-groups']);
    expect(buildNavigation('SuperAdmin').map(item => item.path)).toEqual(['/home', '/attendance', '/users', '/teachers', '/students', '/student-groups']);
  });

  it('keeps teachers out of management navigation', () => {
    expect(buildNavigation('Teacher').map(item => item.path)).toEqual(['/home', '/attendance']);
  });
});
