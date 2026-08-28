import { of } from 'rxjs';
import { ApiClient } from './api-client.service';
import { AssessmentSheetsService } from './assessment-sheets.service';
import { AssessmentsService } from './assessments.service';

describe('AssessmentSheetsService', () => {
  it('replaces records (including snapshot group names) through the PUT contract', () => {
    const detail = { id: 'sheet-1', records: [] } as any;
    const api = jasmine.createSpyObj<ApiClient>('ApiClient', ['put']);
    api.put.and.returnValue(of(detail));
    const service = new AssessmentSheetsService(api);
    const request = {
      records: [
        {
          assessmentId: 'a-1',
          planGrade: null,
          planNote: null,
          finalGrade: null,
          finalNote: null,
          displayOrder: 1,
          groupLv2Name: 'Nhóm lớn mới',
          groupLv3Name: 'Nhóm nhỏ mới'
        }
      ]
    };

    service.replaceRecords('sheet-1', request).subscribe(result => expect(result).toBe(detail));

    expect(api.put).toHaveBeenCalledWith('assessment-sheets/sheet-1/records', request);
  });
});

describe('AssessmentsService', () => {
  it('updates the catalog assessment group through the PATCH contract', () => {
    const result = { updatedCount: 3 };
    const api = jasmine.createSpyObj<ApiClient>('ApiClient', ['patch']);
    api.patch.and.returnValue(of(result));
    const service = new AssessmentsService(api);
    const request = { level: 3 as const, assessmentCodes: ['c-1', 'c-2'], name: 'Vận động tinh' };

    service.updateGroup(request).subscribe(value => expect(value).toBe(result));

    expect(api.patch).toHaveBeenCalledWith('assessments/group', request);
  });
});
