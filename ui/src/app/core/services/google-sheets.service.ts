import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  SyncAssessmentFromGoogleSheetsRequest,
  SyncAssessmentFromGoogleSheetsResponse,
} from '../models/api.models';
import { ApiClient } from './api-client.service';

@Injectable({ providedIn: 'root' })
export class GoogleSheetsService {
  constructor(private readonly api: ApiClient) {}

  syncFromGoogleSheets(request: SyncAssessmentFromGoogleSheetsRequest): Observable<SyncAssessmentFromGoogleSheetsResponse> {
    return this.api.post<SyncAssessmentFromGoogleSheetsResponse>('google-sheets/sync-assessments', request);
  }
}
