import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AssignResponsibleTeacherRequest,
  AssignStudentGroupRequest,
  PagedResponse,
  SaveStudentGroupRequest,
  StudentGroup,
  StudentGroupListQuery
} from '../models/api.models';
import { ApiClient } from './api-client.service';

@Injectable({ providedIn: 'root' })
export class StudentGroupsService {
  constructor(private readonly api: ApiClient) {}

  list(query: StudentGroupListQuery): Observable<PagedResponse<StudentGroup>> {
    return this.api.get<PagedResponse<StudentGroup>>('student-groups', query);
  }

  get(id: string): Observable<StudentGroup> {
    return this.api.get<StudentGroup>(`student-groups/${id}`);
  }

  create(request: SaveStudentGroupRequest): Observable<StudentGroup> {
    return this.api.post<StudentGroup>('student-groups', request);
  }

  update(id: string, request: SaveStudentGroupRequest): Observable<StudentGroup> {
    return this.api.put<StudentGroup>(`student-groups/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.api.delete(`student-groups/${id}`);
  }

  assignTeacher(groupId: string, request: AssignResponsibleTeacherRequest): Observable<StudentGroup> {
    return this.api.put<StudentGroup>(`student-groups/${groupId}/responsible-teacher`, request);
  }

  assignStudent(studentId: string, request: AssignStudentGroupRequest): Observable<void> {
    return this.api.put<void>(`students/${studentId}/group`, request);
  }
}
