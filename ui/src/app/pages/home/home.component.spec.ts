import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AppRoutingModule } from '../../app-routing.module';
import { CurrentUser } from '../../core/models/api.models';
import { AuthService } from '../../shared/services';
import { ProfileComponent } from '../profile/profile.component';
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

describe('AppRoutingModule directive scopes', () => {
  const superAdmin: CurrentUser = {
    id: 'user-1',
    email: 'admin@example.test',
    fullName: 'Portal Administrator',
    phoneNumber: null,
    role: 'SuperAdmin',
    status: 'Active'
  };

  beforeEach(async () => {
    const auth = jasmine.createSpyObj<AuthService>('AuthService', ['hasRole'], {
      user: superAdmin,
      user$: of(superAdmin),
      loggedIn: true
    });
    auth.hasRole.and.callFake((...roles) => roles.includes('SuperAdmin'));

    await TestBed.configureTestingModule({
      imports: [AppRoutingModule],
      providers: [{ provide: AuthService, useValue: auth }]
    }).compileComponents();
  });

  it('renders every SuperAdmin dashboard card through the production module scope', () => {
    const fixture = TestBed.createComponent(HomeComponent);

    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('a.dashboard-card').length).toBe(5);
  });

  it('renders the authenticated profile through the production module scope', () => {
    const fixture = TestBed.createComponent(ProfileComponent);

    fixture.detectChanges();

    const profile = fixture.nativeElement.querySelector('.profile-card');
    expect(profile).not.toBeNull();
    expect(profile.textContent).toContain('admin@example.test');
  });
});
