import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ChangeUserPasswordRequest,
  CreateUserRequest,
  PagedResponse,
  UpdateUserRequest,
  User,
  UserListQuery
} from '../models/api.models';
import { ApiClient } from './api-client.service';

@Injectable({ providedIn: 'root' })
export class UsersService {
  constructor(private readonly api: ApiClient) {}

  list(query: UserListQuery): Observable<PagedResponse<User>> {
    return this.api.get<PagedResponse<User>>('users', query);
  }

  get(id: string): Observable<User> {
    return this.api.get<User>(`users/${id}`);
  }

  create(request: CreateUserRequest): Observable<User> {
    return this.api.post<User>('users', request);
  }

  update(id: string, request: UpdateUserRequest): Observable<User> {
    return this.api.put<User>(`users/${id}`, request);
  }

  changePassword(id: string, request: ChangeUserPasswordRequest): Observable<void> {
    return this.api.put<void>(`users/${id}/password`, request);
  }

  delete(id: string): Observable<void> {
    return this.api.delete(`users/${id}`);
  }
}
