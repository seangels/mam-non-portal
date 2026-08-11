import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AttendanceContext,
  CandidateListQuery,
  CreateAttendanceSheetRequest,
  DailyAttendance,
  HistoricalRecoveryRequest,
  PagedResponse,
  RecoveryGroupCandidate,
  RecoveryStudentCandidate,
  RecoveryTeacherCandidate,
  UpdateAttendanceSheetRequest
} from '../models/api.models';
import { ApiClient } from './api-client.service';

@Injectable({ providedIn: 'root' })
export class AttendanceService {
  constructor(private readonly api: ApiClient) {}

  context(date: string): Observable<AttendanceContext> {
    return this.api.get<AttendanceContext>('attendance/context', { date });
  }

  daily(date: string, groupId?: string): Observable<DailyAttendance> {
    return this.api.get<DailyAttendance>('attendance/daily', { date, groupId });
  }

  create(request: CreateAttendanceSheetRequest): Observable<DailyAttendance> {
    return this.api.post<DailyAttendance>('attendance/sheets', request);
  }

  update(sheetId: string, request: UpdateAttendanceSheetRequest): Observable<DailyAttendance> {
    return this.api.put<DailyAttendance>(`attendance/sheets/${sheetId}`, request);
  }

  recover(request: HistoricalRecoveryRequest): Observable<DailyAttendance> {
    return this.api.post<DailyAttendance>('attendance/sheets/historical-recovery', request);
  }

  recoveryGroups(query: CandidateListQuery): Observable<PagedResponse<RecoveryGroupCandidate>> {
    return this.api.get<PagedResponse<RecoveryGroupCandidate>>('attendance/historical-recovery/group-candidates', query);
  }

  recoveryStudents(query: CandidateListQuery): Observable<PagedResponse<RecoveryStudentCandidate>> {
    return this.api.get<PagedResponse<RecoveryStudentCandidate>>('attendance/historical-recovery/student-candidates', query);
  }

  recoveryTeachers(query: CandidateListQuery): Observable<PagedResponse<RecoveryTeacherCandidate>> {
    return this.api.get<PagedResponse<RecoveryTeacherCandidate>>('attendance/historical-recovery/teacher-candidates', query);
  }
}
