import { Component } from '@angular/core';
import { CurrentUser } from '../../core/models/api.models';
import { AuthService } from '../../shared/services';

@Component({
  templateUrl: 'home.component.html',
  styleUrls: ['./home.component.scss']
})
export class HomeComponent {
  get user(): CurrentUser | null {
    return this.auth.user;
  }

  get canManage(): boolean {
    return this.auth.hasRole('SuperAdmin', 'Admin');
  }

  get roleLabel(): string {
    const labels = { SuperAdmin: 'Quản trị cấp cao', Admin: 'Quản trị viên', Teacher: 'Giáo viên' };
    return this.user ? labels[this.user.role] : '';
  }

  constructor(private readonly auth: AuthService) {}
}
