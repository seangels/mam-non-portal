import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AssignStudentGroupRequest,
  CreateStudentRequest,
  PagedResponse,
  Student,
  StudentListQuery,
  UpdateStudentRequest
} from '../models/api.models';
import { ApiClient } from './api-client.service';

@Injectable({ providedIn: 'root' })
export class StudentsService {
  constructor(private readonly api: ApiClient) {}

  list(query: StudentListQuery): Observable<PagedResponse<Student>> {
    return this.api.get<PagedResponse<Student>>('students', query);
  }

  get(id: string): Observable<Student> {
    return this.api.get<Student>(`students/${id}`);
  }

  create(request: CreateStudentRequest): Observable<Student> {
    return this.api.post<Student>('students', request);
  }

  update(id: string, request: UpdateStudentRequest): Observable<Student> {
    return this.api.put<Student>(`students/${id}`, request);
  }

  assignGroup(id: string, request: AssignStudentGroupRequest): Observable<Student> {
    return this.api.put<Student>(`students/${id}/group`, request);
  }

  delete(id: string, expectedVersion: number): Observable<void> {
    return this.api.delete(`students/${id}`, { expectedVersion });
  }
}
