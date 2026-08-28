import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  PagedResponse,
  Assessment,
  AssessmentDetail,
  AssessmentListQuery,
} from '../models/api.models';
import {
  UpdateAssessmentGroupRequest,
  UpdateAssessmentGroupResult
} from '../models/api.models.assessment-sheets';
import { ApiClient } from './api-client.service';

@Injectable({ providedIn: 'root' })
export class AssessmentsService {
  constructor(private readonly api: ApiClient) {}

  list(query: AssessmentListQuery): Observable<PagedResponse<Assessment>> {
    return this.api.get<PagedResponse<Assessment>>('assessments', query);
  }

  get(id: string): Observable<AssessmentDetail> {
    return this.api.get<AssessmentDetail>(`assessments/${id}`);
  }

  updateGroup(request: UpdateAssessmentGroupRequest): Observable<UpdateAssessmentGroupResult> {
    return this.api.patch<UpdateAssessmentGroupResult>('assessments/group', request);
  }
}
