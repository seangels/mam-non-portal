import { AuthService } from '../../shared/services';
import { HomeComponent } from './home.component';

describe('HomeComponent management boundaries', () => {
  it('lets Admin manage Teachers without exposing administrator accounts', () => {
    const auth = jasmine.createSpyObj<AuthService>('AuthService', ['hasRole'], { user: null });
    auth.hasRole.and.callFake((...roles) => roles.includes('Admin'));
    const component = new HomeComponent(auth);

    expect(component.canManage).toBeTrue();
    expect(component.isSuperAdmin).toBeFalse();
  });

  it('shows both management areas to SuperAdmin', () => {
    const auth = jasmine.createSpyObj<AuthService>('AuthService', ['hasRole'], { user: null });
    auth.hasRole.and.callFake((...roles) => roles.includes('SuperAdmin'));
    const component = new HomeComponent(auth);

    expect(component.canManage).toBeTrue();
    expect(component.isSuperAdmin).toBeTrue();
  });
});
