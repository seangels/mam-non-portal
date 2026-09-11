import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { CurrentUser } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class AuthStateService {
  private readonly userSubject = new BehaviorSubject<CurrentUser | null>(null);
  private accessTokenValue: string | null = localStorage.getItem('admin-portal.access-token');
  private refreshTokenValue: string | null = localStorage.getItem('admin-portal.refresh-token');

  constructor() {
    window.addEventListener('storage', event => {
      if (event.key === 'admin-portal.access-token') this.accessTokenValue = event.newValue;
      if (event.key === 'admin-portal.refresh-token') this.refreshTokenValue = event.newValue;
      if (event.key === 'admin-portal.logout') {
        this.accessTokenValue = null;
        this.refreshTokenValue = null;
        this.userSubject.next(null);
      }
    });
  }

  readonly user$ = this.userSubject.asObservable();

  get user(): CurrentUser | null {
    return this.userSubject.value;
  }

  get accessToken(): string | null {
    return this.accessTokenValue;
  }

  get refreshToken(): string | null {
    return this.refreshTokenValue;
  }

  setSession(accessToken: string, user?: CurrentUser | null, refreshToken?: string): void {
    this.accessTokenValue = accessToken;
    localStorage.setItem('admin-portal.access-token', accessToken);
    if (refreshToken) {
      this.refreshTokenValue = refreshToken;
      localStorage.setItem('admin-portal.refresh-token', refreshToken);
    }
    if (user !== undefined) {
      this.userSubject.next(user);
    }
  }

  setUser(user: CurrentUser): void {
    this.userSubject.next(user);
  }

  clear(): void {
    this.accessTokenValue = null;
    this.refreshTokenValue = null;
    localStorage.removeItem('admin-portal.access-token');
    localStorage.removeItem('admin-portal.refresh-token');
    localStorage.setItem('admin-portal.logout', String(Date.now()));
    this.userSubject.next(null);
  }
}
