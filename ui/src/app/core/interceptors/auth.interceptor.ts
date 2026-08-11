import { HttpErrorResponse, HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable, Injector } from '@angular/core';
import { Observable, catchError, finalize, shareReplay, switchMap, throwError } from 'rxjs';
import { AuthService } from '../../shared/services/auth.service';
import { AuthStateService } from '../services/auth-state.service';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  private refreshRequest$: Observable<string> | null = null;

  constructor(
    private readonly state: AuthStateService,
    private readonly injector: Injector
  ) {}

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const prepared = this.prepareRequest(request);

    return next.handle(prepared).pipe(
      catchError(error => {
        if (!(error instanceof HttpErrorResponse) || error.status !== 401 || this.isAuthEndpoint(request.url)) {
          return throwError(() => error);
        }

        return this.refreshAccessToken().pipe(
          switchMap(token => next.handle(this.prepareRequest(request, token))),
          catchError(refreshError => {
            this.injector.get(AuthService).expireSession();
            return throwError(() => refreshError);
          })
        );
      })
    );
  }

  private prepareRequest(request: HttpRequest<unknown>, token = this.state.accessToken): HttpRequest<unknown> {
    const headers: Record<string, string> = {};
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const csrfToken = this.state.csrfToken;
    if (csrfToken && request.method !== 'GET' && request.method !== 'HEAD') {
      headers['X-CSRF-TOKEN'] = csrfToken;
    }

    return request.clone({ setHeaders: headers, withCredentials: true });
  }

  private refreshAccessToken(): Observable<string> {
    if (!this.refreshRequest$) {
      this.refreshRequest$ = this.injector.get(AuthService).refreshAccessToken().pipe(
        finalize(() => this.refreshRequest$ = null),
        shareReplay(1)
      );
    }
    return this.refreshRequest$;
  }

  private isAuthEndpoint(url: string): boolean {
    return ['/auth/login', '/auth/refresh', '/auth/logout', '/auth/csrf', '/setup/status', '/setup/super-admin']
      .some(path => url.endsWith(path));
  }
}
