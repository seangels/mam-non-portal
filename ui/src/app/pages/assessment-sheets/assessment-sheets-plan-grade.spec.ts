import { of, throwError } from 'rxjs';
import { UserRole } from '../../core/models/api.models';
import {
  AssessmentGrade,
  AssessmentSheetDetail,
  AssessmentSheetStatus,
  ReplaceAssessmentSheetRecordsRequest,
  UpdateAssessmentSheetRequest
} from '../../core/models/api.models.assessment-sheets';
import { AssessmentSheetFormComponent } from './assessment-sheets-form.component';

describe('Assessment sheet plan grade editing', () => {
  const makeForm = (status: AssessmentSheetStatus = 'Open', role: UserRole = 'Admin') => {
    let stored: AssessmentSheetDetail = {
      id: 'sheet-1', studentId: 'student-1', studentSnapshot: {}, status,
      createdAt: '', updatedAt: '',
      records: [{
        id: 'record-1', assessmentSheetId: 'sheet-1',
        assessment: { code: 'A01', name: 'Mục đánh giá', groupLv2Name: 'Nhóm lớn', groupLv3Name: 'Nhóm nhỏ' },
        planGrade: 'B', planNote: 'Ghi chú kế hoạch', finalGrade: 'C', finalNote: 'Ghi chú kết quả',
        displayOrder: 1, createdAt: '', updatedAt: ''
      }]
    };
    const snapshot = (): AssessmentSheetDetail => ({
      ...stored,
      records: stored.records.map(record => ({ ...record, assessment: { ...record.assessment } }))
    });
    const events: string[] = [];
    const assessmentSheets = {
      get: jasmine.createSpy('get').and.callFake(() => of(snapshot())),
      update: jasmine.createSpy('update').and.callFake((_id: string, request: UpdateAssessmentSheetRequest) => {
        events.push(`update:${stored.status}`);
        stored = { ...stored, ...request };
        return of(snapshot());
      }),
      replaceRecords: jasmine.createSpy('replaceRecords').and.callFake((_id: string, request: ReplaceAssessmentSheetRecordsRequest) => {
        events.push(`records:${stored.status}`);
        stored = { ...stored, records: [{ ...stored.records[0], ...request.records[0] }] };
        return of(snapshot());
      }),
      updateStatus: jasmine.createSpy('updateStatus').and.callFake((_id: string, request: { status: AssessmentSheetStatus }) => {
        events.push(`status:${request.status}`);
        stored = { ...stored, status: request.status };
        return of(snapshot());
      })
    };
    const assessments = {
      list: jasmine.createSpy('list').and.returnValue(of({
        items: [{ id: 'assessment-1', code: 'A01', name: 'Mục đánh giá', latestGrade: 'D', latestNote: 'Ghi chú hiện tại' }],
        pagination: { page: 1, pageSize: 1000, totalItems: 1, totalPages: 1 }
      }))
    };
    const router = { navigate: jasmine.createSpy('navigate').and.resolveTo(true) };
    const component = new AssessmentSheetFormComponent(
      assessmentSheets as any, assessments as any, { user: { role } } as any,
      {} as any, {} as any,
      { snapshot: { data: { mode: 'edit' }, paramMap: { get: () => 'sheet-1' } } } as any,
      router as any
    );
    component.isCreate = false;
    (component as any).applyAssessmentSheet(snapshot());
    return { component, assessmentSheets, router, events };
  };

  it('allows Teacher, Admin and SuperAdmin to edit only Open and Canceled plan grades', () => {
    const roles: UserRole[] = ['Teacher', 'Admin', 'SuperAdmin'];
    const statuses: AssessmentSheetStatus[] = ['Open', 'Canceled', 'Planed', 'Done'];
    roles.forEach(role => statuses.forEach(status => {
      const { component } = makeForm(status, role);
      const allowed = status === 'Open' || status === 'Canceled';
      expect(component.canEditPlanGrade).withContext(`${role}/${status}`).toBe(allowed);
      expect(component.planGradeControlsDisabled).withContext(`${role}/${status}`).toBe(!allowed);

      component.updateRecordPlanGrade(component.records[0], 'A');

      expect(component.records[0].planGrade).withContext(`${role}/${status}`).toBe(allowed ? 'A' : 'B');
      expect(component.dirty).withContext(`${role}/${status}`).toBe(allowed);
    }));
  });

  it('follows the selected status immediately while keeping Planed result values editable', () => {
    const { component } = makeForm('Planed');
    component.editor.status = 'Open';
    expect(component.canEditPlanGrade).toBeTrue();
    component.updateRecordPlanGrade(component.records[0], 'A');
    expect(component.records[0].planGrade).toBe('A');

    component.editor.status = 'Planed';
    expect(component.canEditPlanGrade).toBeFalse();
    expect(component.recordValueControlsDisabled).toBeFalse();
    component.updateRecordPlanGrade(component.records[0], null);
    expect(component.records[0].planGrade).toBe('A');

    component.editor.status = 'Canceled';
    expect(component.canEditPlanGrade).toBeTrue();
    component.updateRecordPlanGrade(component.records[0], null);
    expect(component.records[0].planGrade).toBeNull();
  });

  it('locks the dropdown and its handler during every loading or mutation state', () => {
    const busyStates: Partial<AssessmentSheetFormComponent>[] = [
      { loading: true }, { saving: true }, { completingPlan: true }, { deleting: true },
      { addingRecord: true }, { removingRecordId: 'record-1' }, { groupCatalogSaving: true },
      { preparingResultsSubmit: true }, { submittingResults: true }
    ];
    busyStates.forEach(busy => {
      const { component } = makeForm();
      Object.assign(component, busy);
      expect(component.canEditPlanGrade).withContext(JSON.stringify(busy)).toBeTrue();
      expect(component.planGradeControlsDisabled).withContext(JSON.stringify(busy)).toBeTrue();

      component.updateRecordPlanGrade(component.records[0], 'A');
      component.updateRecordPlanGrade(component.records[0], null);

      expect(component.records[0].planGrade).withContext(JSON.stringify(busy)).toBe('B');
      expect(component.dirty).withContext(JSON.stringify(busy)).toBeFalse();
    });
  });

  it('tracks an unsaved plan change and clears dirty state when the original grade is restored', () => {
    const { component, assessmentSheets } = makeForm();
    component.updateRecordPlanGrade(component.records[0], 'A');
    expect(component.dirty).toBeTrue();
    const event = { preventDefault: jasmine.createSpy('preventDefault'), returnValue: undefined } as any;
    component.beforeUnload(event);
    expect(event.preventDefault).toHaveBeenCalled();
    expect(event.returnValue).toBe('');
    expect(assessmentSheets.update).not.toHaveBeenCalled();
    expect(assessmentSheets.replaceRecords).not.toHaveBeenCalled();

    component.updateRecordPlanGrade(component.records[0], 'B');
    expect(component.dirty).toBeFalse();
  });

  (['A', null] as (AssessmentGrade | null)[]).forEach(grade => {
    it(`saves plan grade ${grade} and retains it after reload without changing notes, results or latest`, async () => {
      const { component, assessmentSheets } = makeForm();
      await (component as any).load('sheet-1');
      const original = { ...component.records[0] };
      const latest = { ...component.assessmentCache[0] };
      component.updateRecordPlanGrade(component.records[0], grade);
      expect(component.records[0]).toEqual({ ...original, planGrade: grade });
      expect(component.recordRows[0].latestGrade).toBe('D');
      expect(component.assessmentCache[0]).toEqual(latest);
      expect(component.dirty).toBeTrue();

      await component.save();

      expect(component.formError).toBe('');
      expect(assessmentSheets.replaceRecords).toHaveBeenCalledOnceWith('sheet-1', { records: [{
        assessmentId: 'assessment-1', planGrade: grade, planNote: original.planNote,
        finalGrade: original.finalGrade, finalNote: original.finalNote, displayOrder: 1,
        groupLv2Name: 'Nhóm lớn', groupLv3Name: 'Nhóm nhỏ'
      }] });
      expect(component.dirty).toBeFalse();

      await (component as any).load('sheet-1');

      expect(component.records[0].planGrade).toBe(grade);
      expect(component.records[0].planNote).toBe(original.planNote);
      expect(component.records[0].finalGrade).toBe(original.finalGrade);
      expect(component.records[0].finalNote).toBe(original.finalNote);
      expect(component.recordRows[0].latestGrade).toBe('D');
      expect(component.assessmentCache[0]).toEqual(latest);
      expect(component.dirty).toBeFalse();
    });
  });

  it('completes the plan by saving the new grade before changing status to Planed', async () => {
    const { component, assessmentSheets, router, events } = makeForm();
    component.updateRecordPlanGrade(component.records[0], 'A');

    await component.completePlan();

    expect(component.formError).toBe('');
    expect(events).toEqual(['update:Open', 'records:Open', 'update:Open', 'status:Planed']);
    expect(assessmentSheets.replaceRecords.calls.mostRecent().args[1].records[0].planGrade).toBe('A');
    expect(component.records[0].planGrade).toBe('A');
    expect(component.editor.status).toBe('Planed');
    expect(component.dirty).toBeFalse();
    expect(component.completingPlan).toBeFalse();
    expect(router.navigate).toHaveBeenCalledOnceWith(
      ['/assessment-sheets', 'sheet-1', 'plan-pdf-preview'], { queryParams: { auto: 1 } }
    );
  });

  it('keeps the edited grade for retry and does not complete the plan when records fail to save', async () => {
    const { component, assessmentSheets, router } = makeForm();
    assessmentSheets.replaceRecords.and.returnValue(throwError(() => new Error('Không lưu được')));
    component.updateRecordPlanGrade(component.records[0], 'A');

    await component.completePlan();

    expect(component.formError).toBeTruthy();
    expect(component.records[0].planGrade).toBe('A');
    expect(component.dirty).toBeTrue();
    expect(component.editor.status).toBe('Open');
    expect(component.completingPlan).toBeFalse();
    expect(assessmentSheets.updateStatus).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
