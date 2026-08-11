import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResponse, Teacher, TeacherListQuery, UpdateAttendancePolicyRequest } from '../models/api.models';
import { ApiClient } from './api-client.service';

@Injectable({ providedIn: 'root' })
export class TeachersService {
  constructor(private readonly api: ApiClient) {}

  list(query: TeacherListQuery): Observable<PagedResponse<Teacher>> {
    return this.api.get<PagedResponse<Teacher>>('teachers', query);
  }

  get(id: string): Observable<Teacher> {
    return this.api.get<Teacher>(`teachers/${id}`);
  }

  updateAttendancePolicy(id: string, request: UpdateAttendancePolicyRequest): Observable<Teacher> {
    return this.api.put<Teacher>(`teachers/${id}/attendance-policy`, request);
  }
}
