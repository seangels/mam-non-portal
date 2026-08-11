import { Component } from '@angular/core';
import { AuthService } from '../../shared/services';
import { ROLE_LABELS, USER_STATUS_LABELS } from '../../core/i18n/ui-labels';
import { UserRole, UserStatus } from '../../core/models/api.models';

@Component({
  templateUrl: 'profile.component.html',
  styleUrls: ['./profile.component.scss']
})
export class ProfileComponent {
  readonly user$ = this.auth.user$;
  readonly roleLabels = ROLE_LABELS;
  readonly statusLabels = USER_STATUS_LABELS;

  roleLabel(role: UserRole): string {
    return ROLE_LABELS[role];
  }

  statusLabel(status: UserStatus): string {
    return USER_STATUS_LABELS[status];
  }

  constructor(private readonly auth: AuthService) {}
}
