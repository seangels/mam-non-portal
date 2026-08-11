import { buildNavigation } from './app-navigation';

describe('buildNavigation', () => {
  it('shows management pages to administrators', () => {
    expect(buildNavigation('Admin').map(item => item.path)).toEqual(['/home', '/users', '/students']);
    expect(buildNavigation('SuperAdmin').map(item => item.path)).toEqual(['/home', '/users', '/students']);
  });

  it('keeps teachers out of management navigation', () => {
    expect(buildNavigation('Teacher').map(item => item.path)).toEqual(['/home']);
  });
});
