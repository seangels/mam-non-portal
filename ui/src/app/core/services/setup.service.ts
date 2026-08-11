import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivate, Router, UrlTree } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError } from '../models/api-error';
import {
  CreateSuperAdminRequest,
  SetupStatusResponse,
  SetupSuperAdminResponse
} from '../models/api.models';
import { ApiClient } from './api-client.service';

export type SetupState = 'unknown' | 'required' | 'complete' | 'error';

@Injectable({ providedIn: 'root' })
export class SetupService {
  private stateValue: SetupState = 'unknown';
  private errorValue: ApiError | null = null;

  get state(): SetupState {
    return this.stateValue;
  }

  get error(): ApiError | null {
    return this.errorValue;
  }

  constructor(private readonly api: ApiClient) {}

  async loadStatus(): Promise<SetupState> {
    try {
      const response = await firstValueFrom(this.api.get<SetupStatusResponse>('setup/status'));
      this.stateValue = response.requiresInitialization ? 'required' : 'complete';
      this.errorValue = null;
    } catch (error) {
      this.stateValue = 'error';
      this.errorValue = ApiError.from(error);
    }

    return this.stateValue;
  }

  async createSuperAdmin(request: CreateSuperAdminRequest): Promise<SetupSuperAdminResponse> {
    const created = await firstValueFrom(
      this.api.post<SetupSuperAdminResponse>('setup/super-admin', request)
    );
    this.stateValue = 'complete';
    this.errorValue = null;
    return created;
  }
}

@Injectable({ providedIn: 'root' })
export class SetupRequiredGuard implements CanActivate {
  constructor(private readonly setup: SetupService, private readonly router: Router) {}

  canActivate(): boolean | UrlTree {
    return this.setup.state === 'complete'
      ? this.router.createUrlTree(['/login-form'])
      : true;
  }
}

@Injectable({ providedIn: 'root' })
export class SetupCompletedGuard implements CanActivate {
  constructor(private readonly setup: SetupService, private readonly router: Router) {}

  canActivate(_route: ActivatedRouteSnapshot): boolean | UrlTree {
    return this.setup.state === 'complete'
      ? true
      : this.router.createUrlTree(['/setup']);
  }
}
