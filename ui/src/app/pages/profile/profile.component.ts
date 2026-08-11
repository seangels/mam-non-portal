import { Component } from '@angular/core';
import { AuthService } from '../../shared/services';

@Component({
  templateUrl: 'profile.component.html',
  styleUrls: ['./profile.component.scss']
})
export class ProfileComponent {
  readonly user$ = this.auth.user$;

  constructor(private readonly auth: AuthService) {}
}
