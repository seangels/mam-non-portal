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

function clickDxButton(fixture: ComponentFixture<UsersComponent>, text: string): void {
  const button = fixture.debugElement.queryAll(By.directive(DxButtonComponent))
    .find(item => (item.componentInstance as DxButtonComponent).text === text);
  if (!button) {
    throw new Error(`DevExtreme button not found: ${text}`);
  }
  (button.nativeElement as HTMLElement).click();
  fixture.detectChanges();
}

async function settleWidgetUpdates(fixture: ComponentFixture<UsersComponent>): Promise<void> {
  await fixture.whenStable();
  for (let turn = 0; turn < 5; turn += 1) {
    await Promise.resolve();
  }
  fixture.detectChanges();
}
