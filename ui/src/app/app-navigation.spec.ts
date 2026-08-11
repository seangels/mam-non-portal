import { buildNavigation } from './app-navigation';

describe('buildNavigation', () => {
  it('shows management pages to administrators', () => {
    expect(buildNavigation('Admin').map(item => item.path)).toEqual(['/home', '/attendance', '/users', '/students', '/student-groups']);
    expect(buildNavigation('SuperAdmin').map(item => item.path)).toEqual(['/home', '/attendance', '/users', '/students', '/student-groups']);
  });

  it('keeps teachers out of management navigation', () => {
    expect(buildNavigation('Teacher').map(item => item.path)).toEqual(['/home', '/attendance']);
  });
});
