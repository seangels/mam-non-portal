import { of } from 'rxjs';
import { UsersService } from '../../core/services/users.service';
import { UsersComponent } from './users.component';

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
