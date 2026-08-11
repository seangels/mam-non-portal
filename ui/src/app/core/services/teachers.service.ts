import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ChangeUserPasswordRequest,
  CreateTeacherRequest,
  PagedResponse,
  Teacher,
  TeacherDetail,
  TeacherListQuery,
  UpdateAttendancePolicyRequest,
  UpdateTeacherRequest
} from '../models/api.models';
import { ApiClient } from './api-client.service';

@Injectable({ providedIn: 'root' })
export class TeachersService {
  constructor(private readonly api: ApiClient) {}

  list(query: TeacherListQuery): Observable<PagedResponse<Teacher>> {
    return this.api.get<PagedResponse<Teacher>>('teachers', query);
  }

  get(id: string): Observable<TeacherDetail> {
    return this.api.get<TeacherDetail>(`teachers/${id}`);
  }

  create(request: CreateTeacherRequest): Observable<TeacherDetail> {
    return this.api.post<TeacherDetail>('teachers', request);
  }

  update(id: string, request: UpdateTeacherRequest): Observable<TeacherDetail> {
    return this.api.put<TeacherDetail>(`teachers/${id}`, request);
  }

  updateAttendancePolicy(id: string, request: UpdateAttendancePolicyRequest): Observable<TeacherDetail> {
    return this.api.put<TeacherDetail>(`teachers/${id}/attendance-policy`, request);
  }

  changePassword(userId: string, request: ChangeUserPasswordRequest): Observable<void> {
    return this.api.put<void>(`users/${userId}/password`, request);
  }

  delete(id: string, expectedVersion: number): Observable<void> {
    return this.api.delete(`teachers/${id}`, { expectedVersion });
  }
}
