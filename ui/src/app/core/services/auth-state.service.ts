import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { CurrentUser } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class AuthStateService {
  private readonly userSubject = new BehaviorSubject<CurrentUser | null>(null);
  private accessTokenValue: string | null = null;
  private csrfTokenValue: string | null = null;

  readonly user$ = this.userSubject.asObservable();

  get user(): CurrentUser | null {
    return this.userSubject.value;
  }

  get accessToken(): string | null {
    return this.accessTokenValue;
  }

  get csrfToken(): string | null {
    return this.csrfTokenValue;
  }

  setSession(accessToken: string, user?: CurrentUser | null, csrfToken?: string): void {
    this.accessTokenValue = accessToken;
    if (csrfToken) {
      this.csrfTokenValue = csrfToken;
    }
    if (user !== undefined) {
      this.userSubject.next(user);
    }
  }

  setUser(user: CurrentUser): void {
    this.userSubject.next(user);
  }

  setCsrfToken(token: string): void {
    this.csrfTokenValue = token;
  }

  clear(): void {
    this.accessTokenValue = null;
    this.csrfTokenValue = null;
    this.userSubject.next(null);
  }
}
