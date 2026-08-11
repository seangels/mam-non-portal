import { APP_ROUTES } from './app-routing.module';

describe('APP_ROUTES teacher permissions', () => {
  it('allows both manager roles on every teacher route', () => {
    const teacherRoutes = APP_ROUTES.filter(route => route.path?.startsWith('teachers'));
    expect(teacherRoutes.map(route => route.path)).toEqual([
      'teachers', 'teachers/new', 'teachers/:id/edit', 'teachers/:id'
    ]);
    teacherRoutes.forEach(route => expect(route.data?.['roles']).toEqual(['SuperAdmin', 'Admin']));
  });

  it('keeps administrator accounts restricted to SuperAdmin', () => {
    const usersRoute = APP_ROUTES.find(route => route.path === 'users');
    expect(usersRoute?.data?.['roles']).toEqual(['SuperAdmin']);
  });
});
