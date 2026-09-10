import { of } from 'rxjs';
import { ApiClient } from './api-client.service';
import { AssessmentResultsService } from './assessment-results.service';

describe('AssessmentResultsService', () => {
  it('reads the live assessment result snapshot for a student', () => {
    const api = jasmine.createSpyObj<ApiClient>('ApiClient', ['get']);
    api.get.and.returnValue(of({ student: {}, items: [] }));
    const service = new AssessmentResultsService(api);

    service.get('student-1').subscribe();

    expect(api.get).toHaveBeenCalledWith('students/student-1/assessment-results');
  });

  it('patches only the result items supplied by the caller', () => {
    const api = jasmine.createSpyObj<ApiClient>('ApiClient', ['patch']);
    api.patch.and.returnValue(of({ student: {}, items: [] }));
    const service = new AssessmentResultsService(api);
    const request = {
      items: [{ assessmentId: 'assessment-1', expectedVersion: 'v1', grade: 'A' as const, note: null }]
    };

    service.update('student-1', request).subscribe();

    expect(api.patch).toHaveBeenCalledWith('students/student-1/assessment-results', request);
  });
});
