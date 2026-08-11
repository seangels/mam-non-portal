import { Component } from '@angular/core';
import { CurrentUser } from '../../core/models/api.models';
import { AuthService } from '../../shared/services';
import { ROLE_LABELS } from '../../core/i18n/ui-labels';

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

  get isSuperAdmin(): boolean {
    return this.auth.hasRole('SuperAdmin');
  }

  get roleLabel(): string {
    return this.user ? ROLE_LABELS[this.user.role] : '';
  }

  constructor(private readonly auth: AuthService) {}
}
