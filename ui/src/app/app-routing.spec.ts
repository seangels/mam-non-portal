import { APP_ROUTES } from './app-routing.module';
import { PendingChangesGuard } from './core/guards/pending-changes.guard';

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

  it('allows Teacher to open assessment sheet list and create/edit forms', () => {
    const assessmentSheetRoutes = APP_ROUTES.filter(route => route.path?.startsWith('assessment-sheets'));
    expect(assessmentSheetRoutes.map(route => route.path)).toEqual([
      'assessment-sheets',
      'assessment-sheets/new',
      'assessment-sheets/:id/edit',
      'assessment-sheets/:id/plan-pdf-preview',
      'assessment-sheets/:id/result-pdf-preview'
    ]);
    assessmentSheetRoutes.forEach(route => expect(route.data?.['roles']).toEqual(['SuperAdmin', 'Admin', 'Teacher']));
  });

  it('protects direct assessment-result updates with manager roles and pending-change guard', () => {
    const route = APP_ROUTES.find(item => item.path === 'assessment-results');

    expect(route?.data?.['roles']).toEqual(['SuperAdmin', 'Admin']);
    expect(route?.canActivate?.length).toBe(3);
    expect(route?.canDeactivate).toEqual([PendingChangesGuard]);
  });
});
