import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  PagedResponse,
  AssessmentGroup,
  AssessmentGroupDetail,
  AssessmentGroupListQuery,
} from '../models/api.models';
import { ApiClient } from './api-client.service';

@Injectable({ providedIn: 'root' })
export class AssessmentGroupsService {
  constructor(private readonly api: ApiClient) {}

  list(query: AssessmentGroupListQuery): Observable<PagedResponse<AssessmentGroup>> {
    return this.api.get<PagedResponse<AssessmentGroup>>('assessment-groups', query);
  }

  get(id: string): Observable<AssessmentGroupDetail> {
    return this.api.get<AssessmentGroupDetail>(`assessment-groups/${id}`);
  }
}
