import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivate, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { Observable, catchError, finalize, firstValueFrom, map, of, shareReplay, switchMap, tap } from 'rxjs';
import { ApiError } from '../../core/models/api-error';
import { AuthResponse, CurrentUser, LoginRequest, UserRole } from '../../core/models/api.models';
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
  private refreshRequest$: Observable<string> | null = null;
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
      this.state.setSession(auth.accessToken, auth.user, auth.refreshToken);
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
      this.refreshAccessToken().pipe(
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
    if (!this.refreshRequest$) {
      this.refreshRequest$ = new Observable<string>(subscriber => {
        const initialAccessToken = this.state.accessToken;
        const owner = `${Date.now()}-${Math.random()}`;
        const lockKey = 'admin-portal.refresh-lock';
        const deadline = Date.now() + 10000;
        let timer: number | undefined;
        let stopped = false;

        const finish = (error?: unknown, token?: string) => {
          if (stopped) return;
          stopped = true;
          if (timer !== undefined) window.clearTimeout(timer);
          if (localStorage.getItem(lockKey)?.includes(owner)) localStorage.removeItem(lockKey);
          if (error) subscriber.error(error);
          else if (token) {
            subscriber.next(token);
            subscriber.complete();
          } else subscriber.error(new Error('Không nhận được access token mới.'));
        };

        const waitForOtherTab = () => {
          const token = localStorage.getItem('admin-portal.access-token');
          if (token && token !== initialAccessToken) {
            this.state.setSession(token);
            finish(undefined, token);
            return;
          }
          if (Date.now() >= deadline) {
            localStorage.removeItem(lockKey);
            tryRefresh();
            return;
          }
          timer = window.setTimeout(waitForOtherTab, 100);
        };

        const tryRefresh = () => {
          const lock = localStorage.getItem(lockKey);
          if (!lock || Number(lock.split('|')[0]) <= Date.now()) {
            localStorage.setItem(lockKey, `${Date.now() + 10000}|${owner}`);
          }
          if (localStorage.getItem(lockKey)?.endsWith(owner)) {
            this.api.post<AuthResponse>('auth/refresh', { refreshToken: this.state.refreshToken }).subscribe({
              next: response => {
                this.state.setSession(response.accessToken, response.user, response.refreshToken);
                finish(undefined, response.accessToken);
              },
              error: error => finish(error)
            });
          } else {
            waitForOtherTab();
          }
        };

        tryRefresh();
        return () => finish(new Error('Refresh bị hủy.'));
      }).pipe(
        shareReplay(1),
        finalize(() => this.refreshRequest$ = null)
      );
    }
    return this.refreshRequest$;
  }

  async logOut(): Promise<void> {
    try {
      await firstValueFrom(this.api.post<void>('auth/logout', { refreshToken: this.state.refreshToken }));
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
