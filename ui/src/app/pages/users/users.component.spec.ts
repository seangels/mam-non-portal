import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { DxButtonComponent } from 'devextreme-angular/ui/button';
import { of } from 'rxjs';
import { UsersService } from '../../core/services/users.service';
import { UsersComponent, UsersModule } from './users.component';

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

  it('submits the password typed into the real create-user editor', async () => {
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

    enterPopupFormValue('fullName', 'DX19 Admin');
    enterPopupFormValue('email', 'dx19-admin@example.test');
    enterPopupFormValue('phoneNumber', '0900000000');
    const passwordInput = enterPopupFormValue('password', 'Strong#Pass123', false);
    passwordInput.dispatchEvent(new Event('blur', { bubbles: true }));
    fixture.detectChanges();

    expect(passwordInput.value).toBe('Strong#Pass123');
    expect(component.editor.fullName).toBe('DX19 Admin');
    expect(component.editor.email).toBe('dx19-admin@example.test');
    expect(component.editor.phoneNumber).toBe('0900000000');
    expect(component.editor.password).toBe('Strong#Pass123');
    clickPopupButton(fixture, 'Tạo quản trị viên');
    await flushMicrotasks(fixture);

    expect(component.editor.password).toBe('Strong#Pass123');
    expect(users.create).toHaveBeenCalledTimes(1);
    expect(users.create.calls.mostRecent().args[0].password).toBe('Strong#Pass123');
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

function enterPopupFormValue(name: string, value: string, commitChange = true): HTMLInputElement {
  const input = document.querySelector(`.dx-popup-content input[name="${name}"]`) as HTMLInputElement | null;
  if (!input) {
    throw new Error(`DevExtreme form input not found: ${name}`);
  }
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
  if (commitChange) {
    input.dispatchEvent(new Event('change', { bubbles: true }));
  }
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
