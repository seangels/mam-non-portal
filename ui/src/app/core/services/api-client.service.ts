import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiError } from '../models/api-error';

type QueryValue = string | number | boolean | null | undefined;

@Injectable({ providedIn: 'root' })
export class ApiClient {
  readonly baseUrl = environment.apiBaseUrl.replace(/\/$/, '');

  constructor(private readonly http: HttpClient) {}

  get<T>(path: string, query?: object): Observable<T> {
    return this.http.get<T>(this.url(path), {
      params: this.toParams(query),
      withCredentials: true
    }).pipe(catchError(error => throwError(() => ApiError.from(error))));
  }

  post<T>(path: string, body?: unknown): Observable<T> {
    return this.http.post<T>(this.url(path), body ?? {}, { withCredentials: true })
      .pipe(catchError(error => throwError(() => ApiError.from(error))));
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<T>(this.url(path), body, { withCredentials: true })
      .pipe(catchError(error => throwError(() => ApiError.from(error))));
  }

  patch<T>(path: string, body: unknown): Observable<T> {
    return this.http.patch<T>(this.url(path), body, { withCredentials: true })
      .pipe(catchError(error => throwError(() => ApiError.from(error))));
  }

  delete(path: string, query?: object): Observable<void> {
    return this.http.delete<void>(this.url(path), {
      params: this.toParams(query),
      withCredentials: true
    })
      .pipe(catchError(error => throwError(() => ApiError.from(error))));
  }

  private url(path: string): string {
    return `${this.baseUrl}/${path.replace(/^\//, '')}`;
  }

  private toParams(query?: object): HttpParams {
    let params = new HttpParams();
    Object.entries((query ?? {}) as Record<string, QueryValue>).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    });
    return params;
  }
}
