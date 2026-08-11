import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivate, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { Observable, catchError, firstValueFrom, map, of, switchMap, tap, throwError } from 'rxjs';
import { ApiError } from '../../core/models/api-error';
import { AuthResponse, CsrfResponse, CurrentUser, LoginRequest, UserRole } from '../../core/models/api.models';
import { ApiClient } from '../../core/services/api-client.service';
import { AuthStateService } from '../../core/services/auth-state.service';

export type IUser = CurrentUser;

export interface AuthResult {
  isOk: boolean;
  data?: CurrentUser | null;
  message?: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly user$ = this.state.user$;

  get user(): CurrentUser | null {
    return this.state.user;
  }

  get loggedIn(): boolean {
    return !!this.state.user && !!this.state.accessToken;
  }

  constructor(
    private readonly api: ApiClient,
    private readonly state: AuthStateService,
    private readonly router: Router
  ) {}

  async logIn(email: string, password: string): Promise<AuthResult> {
    try {
      const auth = await firstValueFrom(this.api.post<AuthResponse>('auth/login', { email, password } as LoginRequest));
      this.state.setSession(auth.accessToken, auth.user, auth.csrfToken);
      const user = auth.user ?? await firstValueFrom(this.loadCurrentUser());
      return { isOk: true, data: user };
    } catch (error) {
      this.state.clear();
      return { isOk: false, message: ApiError.from(error).message };
    }
  }

  async getUser(): Promise<AuthResult> {
    if (this.state.user) {
      return { isOk: true, data: this.state.user };
    }

    try {
      const user = await firstValueFrom(this.loadCurrentUser());
      return { isOk: true, data: user };
    } catch (error) {
      return { isOk: false, data: null, message: ApiError.from(error).message };
    }
  }

  restoreSession(): Promise<void> {
    return firstValueFrom(
      this.api.get<CsrfResponse>('auth/csrf').pipe(
        tap(response => this.state.setCsrfToken(response.csrfToken)),
        switchMap(() => this.refreshAccessToken()),
        switchMap(() => this.loadCurrentUser()),
        map(() => undefined),
        catchError(() => {
          this.state.clear();
          return of(undefined);
        })
      )
    );
  }

  refreshAccessToken(): Observable<string> {
    return this.api.post<AuthResponse>('auth/refresh').pipe(
      tap(response => this.state.setSession(response.accessToken, response.user, response.csrfToken)),
      map(response => response.accessToken)
    );
  }

  async logOut(): Promise<void> {
    try {
      await firstValueFrom(this.api.post<void>('auth/logout'));
    } catch (error) {
      const apiError = ApiError.from(error);
      if (apiError.status !== 401) {
        throw apiError;
      }
    } finally {
      this.state.clear();
      await this.router.navigate(['/login-form']);
    }
  }

  hasRole(...roles: UserRole[]): boolean {
    return !!this.user && roles.includes(this.user.role);
  }

  expireSession(): void {
    this.state.clear();
    void this.router.navigate(['/login-form']);
  }

  private loadCurrentUser(): Observable<CurrentUser> {
    return this.api.get<CurrentUser>('auth/me').pipe(tap(user => this.state.setUser(user)));
  }
}

@Injectable({ providedIn: 'root' })
export class AuthGuardService implements CanActivate {
  constructor(private readonly router: Router, private readonly auth: AuthService) {}

  canActivate(_route: ActivatedRouteSnapshot, state: RouterStateSnapshot): boolean | UrlTree {
    return this.auth.loggedIn
      ? true
      : this.router.createUrlTree(['/login-form'], { queryParams: { returnUrl: state.url } });
  }
}

@Injectable({ providedIn: 'root' })
export class PublicOnlyGuard implements CanActivate {
  constructor(private readonly router: Router, private readonly auth: AuthService) {}

  canActivate(): boolean | UrlTree {
    return this.auth.loggedIn ? this.router.createUrlTree(['/home']) : true;
  }
}

@Injectable({ providedIn: 'root' })
export class RoleGuard implements CanActivate {
  constructor(private readonly router: Router, private readonly auth: AuthService) {}

  canActivate(route: ActivatedRouteSnapshot): boolean | UrlTree {
    const roles = (route.data['roles'] ?? []) as UserRole[];
    return this.auth.loggedIn && (roles.length === 0 || this.auth.hasRole(...roles))
      ? true
      : this.router.createUrlTree(['/home']);
  }
}
