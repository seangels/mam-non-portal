import { CommonModule } from '@angular/common';
import { Component, ErrorHandler } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { DxButtonComponent } from 'devextreme-angular/ui/button';
import { of } from 'rxjs';
import { UsersService } from '../../core/services/users.service';
import { LoginFormComponent, LoginFormModule } from '../../shared/components/login-form/login-form.component';
import { AuthService } from '../../shared/services/auth.service';
import { UsersComponent, UsersModule } from './users.component';

@Component({
  template: `
    <ng-container *ngIf="authenticated; else unauthenticated">
      <router-outlet></router-outlet>
    </ng-container>
    <ng-template #unauthenticated>
      <router-outlet></router-outlet>
    </ng-template>
  `
})
class UsersLifecycleHostComponent {
  authenticated = true;
}

describe('UsersComponent administrator boundary', () => {
  it('always queries only Admin accounts', async () => {
    const users = jasmine.createSpyObj<UsersService>('UsersService', ['list']);
    users.list.and.returnValue(of({
      items: [],
      pagination: { page: 1, pageSize: 20, totalItems: 0, totalPages: 0 }
    }));
    const component = new UsersComponent(users);

    await (component.dataSource as any).load({ skip: 0, take: 20 });

    expect(users.list.calls.mostRecent().args[0].role).toBe('Admin');
  });
});

describe('UsersComponent DevExtreme filter value integration', () => {
  let fixture: ComponentFixture<UsersComponent>;
  let component: UsersComponent;
  let users: jasmine.SpyObj<UsersService>;

  beforeEach(async () => {
    users = jasmine.createSpyObj<UsersService>('UsersService', [
      'list', 'create', 'update', 'changePassword', 'delete'
    ]);
    users.list.and.returnValue(of({
      items: [],
      pagination: { page: 1, pageSize: 20, totalItems: 0, totalPages: 0 }
    }));

    await TestBed.configureTestingModule({
      imports: [UsersModule],
      providers: [{ provide: UsersService, useValue: users }]
    }).compileComponents();

    fixture = TestBed.createComponent(UsersComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await settleWidgetUpdates(fixture);
    users.list.calls.reset();
  });

  it('sends text entered through the real text box when the filter button is clicked', async () => {
    enterSearchValue(fixture, 'DX19-QA-NO-MATCH');
    expect(component.search).toBe('DX19-QA-NO-MATCH');

    clickDxButton(fixture, 'Lọc');
    await settleWidgetUpdates(fixture);
    await loadUsers(component);

    expect(users.list.calls.mostRecent().args[0].search).toBe('DX19-QA-NO-MATCH');
  });

  it('clears the real text box and remote query when reset is clicked', async () => {
    enterSearchValue(fixture, 'DX19-QA-NO-MATCH');
    clickDxButton(fixture, 'Lọc');
    await settleWidgetUpdates(fixture);

    clickDxButton(fixture, 'Đặt lại');
    await settleWidgetUpdates(fixture);

    const input = fixture.nativeElement.querySelector('.filters .dx-textbox input') as HTMLInputElement;
    expect(input.value).toBe('');
    expect(component.search).toBe('');
    await loadUsers(component);
    expect(users.list.calls.mostRecent().args[0].search).toBeUndefined();
  });

  it('submits text and password typed into the real create-user editors', async () => {
    users.create.and.returnValue(of({
      id: 'admin-2',
      email: 'dx19-admin@example.test',
      fullName: 'DX19 Admin',
      phoneNumber: '0900000000',
      role: 'Admin',
      status: 'Active',
      createdAt: '2026-08-14T00:00:00Z',
      updatedAt: '2026-08-14T00:00:00Z'
    }));

    component.openCreate();
    fixture.detectChanges();
    await settleWidgetUpdates(fixture);

    const fullNameInput = enterPopupFormValue('fullName', 'DX19 Admin');
    fullNameInput.dispatchEvent(new Event('blur', { bubbles: true }));
    const emailInput = enterPopupFormValue('email', 'dx19-admin@example.test');
    emailInput.dispatchEvent(new Event('blur', { bubbles: true }));
    const passwordInput = enterPopupFormValue('password', 'Strong#Pass123');
    passwordInput.dispatchEvent(new Event('blur', { bubbles: true }));
    fixture.detectChanges();

    expect(fullNameInput.value).toBe('DX19 Admin');
    expect(emailInput.value).toBe('dx19-admin@example.test');
    expect(passwordInput.value).toBe('Strong#Pass123');
    expect(component.editor.fullName).toBe('DX19 Admin');
    expect(component.editor.email).toBe('dx19-admin@example.test');
    expect(component.editor.password).toBe('Strong#Pass123');
    clickPopupButton(fixture, 'Tạo quản trị viên');
    await flushMicrotasks(fixture);

    expect(component.editor.password).toBe('Strong#Pass123');
    expect(users.create).toHaveBeenCalledTimes(1);
    expect(users.create.calls.mostRecent().args[0]).toEqual(jasmine.objectContaining({
      fullName: 'DX19 Admin',
      email: 'dx19-admin@example.test',
      password: 'Strong#Pass123'
    }));
  });
});

describe('UsersComponent routed nested-option lifecycle', () => {
  it('renders login after authentication changes during route navigation without a nested-option error', async () => {
    const users = jasmine.createSpyObj<UsersService>('UsersService', [
      'list', 'create', 'update', 'changePassword', 'delete'
    ]);
    users.list.and.returnValue(of({
      items: [],
      pagination: { page: 1, pageSize: 20, totalItems: 0, totalPages: 0 }
    }));
    const auth = jasmine.createSpyObj<AuthService>('AuthService', ['logIn']);
    await TestBed.configureTestingModule({
      imports: [
        CommonModule,
        UsersModule,
        LoginFormModule,
        RouterTestingModule.withRoutes([
          { path: 'users', component: UsersComponent },
          { path: 'login-form', component: LoginFormComponent }
        ])
      ],
      declarations: [UsersLifecycleHostComponent],
      providers: [
        { provide: UsersService, useValue: users },
        { provide: AuthService, useValue: auth }
      ]
    }).compileComponents();

    const hostFixture = TestBed.createComponent(UsersLifecycleHostComponent);
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/users');
    hostFixture.detectChanges();
    await hostFixture.whenStable();
    const usersComponent = hostFixture.debugElement.query(By.directive(UsersComponent))
      .componentInstance as UsersComponent;
    usersComponent.openCreate();
    hostFixture.detectChanges();
    await hostFixture.whenStable();

    usersComponent.openEdit({
      id: 'admin-2',
      email: 'dx19-admin@example.test',
      fullName: 'DX19 Admin',
      phoneNumber: '0900000000',
      role: 'Admin',
      status: 'Active',
      createdAt: '2026-08-14T00:00:00Z',
      updatedAt: '2026-08-14T00:00:00Z'
    });
    hostFixture.detectChanges();
    await hostFixture.whenStable();

    const errorHandler = TestBed.inject(ErrorHandler);
    const handleError = spyOn(errorHandler, 'handleError');
    hostFixture.componentInstance.authenticated = false;
    await router.navigateByUrl('/login-form');

    expect(() => hostFixture.detectChanges()).not.toThrow();
    await hostFixture.whenStable();
    hostFixture.detectChanges();

    expect(handleError).not.toHaveBeenCalled();
    expect(hostFixture.debugElement.query(By.directive(LoginFormComponent))).not.toBeNull();
    expect(hostFixture.nativeElement.querySelectorAll('.login-form .dx-field-item-label').length).toBe(0);

    const submitInput = hostFixture.nativeElement.querySelector(
      '.login-form .dx-button-submit-input'
    ) as HTMLInputElement;
    submitInput.click();
    hostFixture.detectChanges();

    expect(hostFixture.nativeElement.textContent).toContain('Vui lòng nhập email');
    expect(hostFixture.nativeElement.textContent).toContain('Vui lòng nhập mật khẩu');
    expect(auth.logIn).not.toHaveBeenCalled();
  });
});

interface LoadableDataSource {
  load(options: { skip: number; take: number }): Promise<unknown>;
}

async function loadUsers(component: UsersComponent): Promise<void> {
  const dataSource = component.dataSource as unknown as LoadableDataSource;
  await dataSource.load({ skip: 0, take: 20 });
}

function enterSearchValue(fixture: ComponentFixture<UsersComponent>, value: string): void {
  const input = fixture.nativeElement.querySelector('.filters .dx-textbox input') as HTMLInputElement;
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
  fixture.detectChanges();
}

function enterPopupFormValue(name: string, value: string): HTMLInputElement {
  const input = document.querySelector(`.dx-popup-content input[name="${name}"]`) as HTMLInputElement | null;
  if (!input) {
    throw new Error(`DevExtreme form input not found: ${name}`);
  }
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
  return input;
}

function clickDxButton(fixture: ComponentFixture<UsersComponent>, text: string): void {
  const button = fixture.debugElement.queryAll(By.directive(DxButtonComponent))
    .find(item => (item.componentInstance as DxButtonComponent).text === text);
  if (!button) {
    throw new Error(`DevExtreme button not found: ${text}`);
  }
  (button.nativeElement as HTMLElement).click();
  fixture.detectChanges();
}

function clickPopupButton(fixture: ComponentFixture<UsersComponent>, text: string): void {
  const button = Array.from(document.querySelectorAll<HTMLElement>('.dx-popup-content .dx-button'))
    .find(item => item.textContent?.includes(text));
  if (!button) {
    throw new Error(`DevExtreme popup button not found: ${text}`);
  }
  const submitInput = button.querySelector<HTMLInputElement>('.dx-button-submit-input');
  if (!submitInput) {
    throw new Error(`DevExtreme popup submit input not found: ${text}`);
  }
  submitInput.click();
  fixture.detectChanges();
}

async function settleWidgetUpdates(fixture: ComponentFixture<UsersComponent>): Promise<void> {
  await fixture.whenStable();
  await flushMicrotasks(fixture);
}

async function flushMicrotasks(fixture: ComponentFixture<UsersComponent>): Promise<void> {
  for (let turn = 0; turn < 5; turn += 1) {
    await Promise.resolve();
  }
  fixture.detectChanges();
}
