import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  PagedResponse,
} from '../models/api.models';
import { ApiClient } from './api-client.service';
import {
  AssessmentSheet,
  AssessmentSheetDetail,
  AssessmentSheetListQuery,
  CreateAssessmentSheetRequest,
  UpdateAssessmentSheetRequest,
  UpdateAssessmentSheetStatusRequest
} from '../models/api.models.assessment-sheets';

@Injectable({ providedIn: 'root' })
export class AssessmentSheetsService {
  constructor(private readonly api: ApiClient) { }

  list(query: AssessmentSheetListQuery): Observable<PagedResponse<AssessmentSheet>> {
    return this.api.get<PagedResponse<AssessmentSheet>>('assessment-sheets', query);
  }

  get(id: string): Observable<AssessmentSheetDetail> {
    return this.api.get<AssessmentSheetDetail>(`assessment-sheets/${id}`);
  }

  create(request: CreateAssessmentSheetRequest): Observable<AssessmentSheetDetail> {
    return this.api.post<AssessmentSheetDetail>('assessment-sheets', request);
  }

  update(id: string, request: UpdateAssessmentSheetRequest): Observable<AssessmentSheetDetail> {
    return this.api.put<AssessmentSheetDetail>(`assessment-sheets/${id}`, request);
  }

  updateStatus(id: string, request: UpdateAssessmentSheetStatusRequest): Observable<AssessmentSheetDetail> {
    return this.api.put<AssessmentSheetDetail>(`assessment-sheets/${id}/status`, request);
  }

  delete(id: string): Observable<void> {
    return this.api.delete(`assessment-sheets/${id}`);
  }
}
