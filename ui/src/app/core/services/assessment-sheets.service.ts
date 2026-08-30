import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  PagedResponse,
} from '../models/api.models';
import { ApiClient } from './api-client.service';
import {
  AssessmentSheet,
  AssessmentSheetDetail,
  AssessmentSheetImportExcelResult,
  AssessmentSheetImportExcelPreviewResult,
  AssessmentSheetListQuery,
  CreateAssessmentSheetRequest,
  ReplaceAssessmentSheetRecordsRequest,
  SubmitResultsPreview,
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

  replaceRecords(id: string, request: ReplaceAssessmentSheetRecordsRequest): Observable<AssessmentSheetDetail> {
    return this.api.put<AssessmentSheetDetail>(`assessment-sheets/${id}/records`, request);
  }

  updateStatus(id: string, request: UpdateAssessmentSheetStatusRequest): Observable<AssessmentSheetDetail> {
    return this.api.put<AssessmentSheetDetail>(`assessment-sheets/${id}/status`, request);
  }

  submitResults(id: string): Observable<AssessmentSheetDetail> {
    return this.api.post<AssessmentSheetDetail>(`assessment-sheets/${id}/submit-results`, {});
  }

  previewSubmitResults(id: string): Observable<SubmitResultsPreview> {
    return this.api.post<SubmitResultsPreview>(`assessment-sheets/${id}/submit-results/preview`, {});
  }

  importExcel(file: File): Observable<AssessmentSheetImportExcelResult> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.api.post<AssessmentSheetImportExcelResult>('assessment-sheets/import-excel', formData);
  }

  previewImportExcel(file: File): Observable<AssessmentSheetImportExcelPreviewResult> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.api.post<AssessmentSheetImportExcelPreviewResult>('assessment-sheets/import-excel/preview', formData);
  }

  uploadPlanPdf(id: string, file: Blob, fileName: string): Observable<AssessmentSheetDetail> {
    const formData = new FormData();
    formData.append('file', file, fileName);
    return this.api.post<AssessmentSheetDetail>(`assessment-sheets/${id}/upload-plan-pdf`, formData);
  }

  uploadResultPdf(id: string, file: Blob, fileName: string): Observable<AssessmentSheetDetail> {
    const formData = new FormData();
    formData.append('file', file, fileName);
    return this.api.post<AssessmentSheetDetail>(`assessment-sheets/${id}/upload-result-pdf`, formData);
  }

  delete(id: string): Observable<void> {
    return this.api.delete(`assessment-sheets/${id}`);
  }
}
