import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AssessmentResultsSnapshot,
  UpdateAssessmentResultsRequest
} from '../models/api.models.assessment-results';
import { ApiClient } from './api-client.service';

@Injectable({ providedIn: 'root' })
export class AssessmentResultsService {
  constructor(private readonly api: ApiClient) {}

  get(studentId: string): Observable<AssessmentResultsSnapshot> {
    return this.api.get<AssessmentResultsSnapshot>(`students/${studentId}/assessment-results`);
  }

  update(studentId: string, request: UpdateAssessmentResultsRequest): Observable<AssessmentResultsSnapshot> {
    return this.api.patch<AssessmentResultsSnapshot>(`students/${studentId}/assessment-results`, request);
  }
}
