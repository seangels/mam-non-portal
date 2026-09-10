import { of, ReplaySubject, throwError } from 'rxjs';
import { ApiError } from '../../core/models/api-error';
import { SyncAssessmentFromGoogleSheetsResponse } from '../../core/models/api.models';
import { AssessmentResultsSnapshot } from '../../core/models/api.models.assessment-results';
import { AssessmentResultsService } from '../../core/services/assessment-results.service';
import { GoogleSheetsService } from '../../core/services/google-sheets.service';
import { StudentsService } from '../../core/services/students.service';
import { AssessmentResultsComponent } from './assessment-results.component';

describe('AssessmentResultsComponent', () => {
  let results: jasmine.SpyObj<AssessmentResultsService>;
  let students: jasmine.SpyObj<StudentsService>;
  let googleSheets: jasmine.SpyObj<GoogleSheetsService>;
  let component: AssessmentResultsComponent;

  beforeEach(() => {
    results = jasmine.createSpyObj<AssessmentResultsService>('AssessmentResultsService', ['get', 'update']);
    students = jasmine.createSpyObj<StudentsService>('StudentsService', ['list', 'get']);
    googleSheets = jasmine.createSpyObj<GoogleSheetsService>('GoogleSheetsService', ['syncFromGoogleSheets']);
    students.list.and.returnValue(of({
      items: [],
      pagination: { page: 1, pageSize: 20, totalItems: 0, totalPages: 0 }
    }));
    component = new AssessmentResultsComponent(results, students, googleSheets);
  });

  it('adds a remote student selector to the grid toolbar and does not filter out inactive students', async () => {
    students.list.and.returnValue(of({
      items: [{
        id: 'student-old', studentCode: 'HS-CU', fullName: 'Học sinh cũ', nickName: 'Bé Cũ',
        dateOfBirth: '2020-01-01', status: 'Inactive', studySchedule: { mode: 'FullDay', weekdays: ['Monday'] },
        createdAt: '', updatedAt: '', version: 1
      }],
      pagination: { page: 1, pageSize: 20, totalItems: 1, totalPages: 1 }
    }));
    const event = { toolbarOptions: { items: [] as Array<Record<string, unknown>> } };

    component.onToolbarPreparing(event);
    const loaded = await (component.studentDataSource as any).load({ skip: 0, take: 20, searchValue: 'cũ' });

    expect(event.toolbarOptions.items[0]['name']).toBe('assessmentStudentSelector');
    expect(event.toolbarOptions.items[1]['name']).toBe('assessmentResultsSync');
    expect(event.toolbarOptions.items[1]['options']).toEqual(jasmine.objectContaining({
      text: 'Đồng bộ GGSheet', icon: 'refresh', disabled: false
    }));
    expect(loaded.data[0].status).toBe('Inactive');
    expect(students.list.calls.mostRecent().args[0]).toEqual(jasmine.objectContaining({
      page: 1, pageSize: 20, search: 'cũ'
    }));
    expect(students.list.calls.mostRecent().args[0].status).toBeUndefined();
  });

  it('opens the shared sync dialog from the toolbar without requiring a selected student', () => {
    const event = { toolbarOptions: { items: [] as Array<Record<string, unknown>> } };
    component.onToolbarPreparing(event);
    const syncOptions = event.toolbarOptions.items[1]['options'] as { onClick(): void };

    syncOptions.onClick();

    expect(component.selectedStudentId).toBeNull();
    expect(component.syncDialogVisible).toBeTrue();
  });

  it('ignores a stale response when the student changes quickly', async () => {
    const first = new ReplaySubject<AssessmentResultsSnapshot>(1);
    const second = new ReplaySubject<AssessmentResultsSnapshot>(1);
    results.get.and.callFake(id => id === 'student-1' ? first.asObservable() : second.asObservable());

    const firstChange = component.changeStudent('student-1');
    const secondChange = component.changeStudent('student-2');
    second.next(snapshot('student-2'));
    second.complete();
    await secondChange;
    first.next(snapshot('student-1'));
    first.complete();
    await firstChange;

    expect(component.selectedStudentId).toBe('student-2');
    expect(component.student?.studentCode).toBe('HS-student-2');
  });

  it('keeps edits local and PATCHes only changed rows after Save', async () => {
    applySnapshot(component, snapshot('student-1'));
    component.drafts[0].grade = 'A';
    component.drafts[0].note = '  Ghi chú mới  ';
    results.update.and.returnValue(of(snapshot('student-1', 'A', 'Ghi chú mới')));

    expect(results.update).not.toHaveBeenCalled();
    expect(component.dirtyCount).toBe(1);
    await component.save();

    const request = results.update.calls.mostRecent().args[1];
    expect(request.items).toEqual([{
      assessmentId: 'assessment-1', expectedVersion: 'version-1', grade: 'A', note: 'Ghi chú mới'
    }]);
    expect(component.dirtyCount).toBe(0);
  });

  it('shows the student birth date and age in years and months in the page subtitle', () => {
    const now = new Date();
    const birthDate = new Date(now.getFullYear() - 6, now.getMonth() - 8, 1);
    const value = snapshot('student-1');
    value.student.dateOfBirth = [
      birthDate.getFullYear(),
      String(birthDate.getMonth() + 1).padStart(2, '0'),
      '01'
    ].join('-');

    applySnapshot(component, value);

    expect(component.studentSummary).toContain(`Ngày sinh 01/${String(birthDate.getMonth() + 1).padStart(2, '0')}/${birthDate.getFullYear()}`);
    expect(component.studentSummary).toContain('6 tuổi, 8 tháng');
  });

  it('supports clearing both the grade and note', async () => {
    applySnapshot(component, snapshot('student-1', 'B', 'Ghi chú cũ'));
    component.drafts[0].grade = null;
    component.drafts[0].note = '';
    results.update.and.returnValue(of(snapshot('student-1')));

    await component.save();

    expect(results.update.calls.mostRecent().args[1].items[0]).toEqual({
      assessmentId: 'assessment-1', expectedVersion: 'version-1', grade: null, note: null
    });
  });

  it('reuses the assessment-sheet grade colors for the grid editor options', () => {
    const supportPlus = component.gradeOptions.find(option => option.value === 'B');

    expect(component.gradeOptions.map(option => option.text)).toEqual([
      'Chưa có kết quả', 'Đạt +', 'Hỗ trợ +', 'Hỗ trợ -', 'Chưa đạt -'
    ]);
    expect(supportPlus).toBeDefined();
    expect(component.gradeColor('B')).toBe(supportPlus!.color);
    expect(component.gradeBackground('B')).toBe(supportPlus!.bgcolor);
  });

  it('keeps the draft, marks conflicts and requires an explicit reload after a 409', async () => {
    applySnapshot(component, snapshot('student-1'));
    component.drafts[0].grade = 'D';
    results.update.and.returnValue(throwError(() => new ApiError(
      'Kết quả đã thay đổi.', 409, {}, undefined, 'AssessmentResultsVersionConflict', undefined,
      [{ assessmentId: 'assessment-1', currentVersion: 'version-2', currentGrade: 'B', currentNote: null }]
    )));

    await component.save();

    expect(component.drafts[0].grade).toBe('D');
    expect(component.dirtyCount).toBe(1);
    expect(component.reloadRequired).toBeTrue();
    expect(component.conflictMessage).toContain('1 dòng đang xung đột');
    expect(component.saveDisabled).toBeTrue();
  });

  it('offers sync instead of DB reload when Google Sheet and portal are out of sync', async () => {
    applySnapshot(component, snapshot('student-1'));
    component.drafts[0].grade = 'D';
    results.update.and.returnValue(throwError(() => new ApiError(
      'Dữ liệu nguồn đang lệch.', 409, {}, undefined, 'AssessmentResultsSourceOutOfSync', undefined, [], false,
      [{ assessmentId: 'assessment-1', databaseGrade: null, sourceGrade: 'B' }]
    )));

    await component.save();

    expect(component.drafts[0].grade).toBe('D');
    expect(component.reloadRequired).toBeFalse();
    expect(component.syncRequired).toBeTrue();
    expect(component.conflictMessage).toContain('1 dòng đang lệch nguồn');
    expect(component.saveDisabled).toBeTrue();
  });

  it('does not offer another save when Google wrote successfully but readback failed', async () => {
    applySnapshot(component, snapshot('student-1'));
    component.drafts[0].grade = 'C';
    results.update.and.returnValue(throwError(() => new ApiError(
      'Google Sheet đã được ghi.', 500, {}, undefined, 'AssessmentResultsPostWriteFailed', undefined, [], true
    )));

    await component.save();

    expect(component.reloadRequired).toBeFalse();
    expect(component.syncRequired).toBeTrue();
    expect(component.saveDisabled).toBeTrue();
    expect(component.dirtyCount).toBe(1);
  });

  it('asks immediately before syncing a dirty draft and keeps it when declined or sync fails', async () => {
    applySnapshot(component, snapshot('student-1'));
    component.drafts[0].grade = 'A';
    const confirmSync = spyOn<any>(component, 'confirmSyncWithDraft').and.returnValue(Promise.resolve(false));
    googleSheets.syncFromGoogleSheets.and.returnValue(throwError(() => new ApiError('Đồng bộ thất bại.', 500)));

    component.openSyncDialog();
    expect(component.syncDialogVisible).toBeTrue();
    await component.onSyncConfirmed({});
    expect(googleSheets.syncFromGoogleSheets).not.toHaveBeenCalled();
    expect(component.dirtyCount).toBe(1);

    confirmSync.and.returnValue(Promise.resolve(true));
    await component.onSyncConfirmed({});

    expect(component.drafts[0].grade).toBe('A');
    expect(component.dirtyCount).toBe(1);
  });

  it('locks editing and reloads the selected portal snapshot after sync succeeds', async () => {
    applySnapshot(component, snapshot('student-1'));
    component.drafts[0].grade = 'D';
    spyOn<any>(component, 'confirmSyncWithDraft').and.returnValue(Promise.resolve(true));
    const toolbar = { toolbarOptions: { items: [] as Array<Record<string, unknown>> } };
    component.onToolbarPreparing(toolbar);
    const studentWidget = { option: jasmine.createSpy('studentOption') };
    const syncWidget = { option: jasmine.createSpy('syncOption') };
    (toolbar.toolbarOptions.items[0]['options'] as any).onInitialized({ component: studentWidget });
    (toolbar.toolbarOptions.items[1]['options'] as any).onInitialized({ component: syncWidget });
    const syncResponse = new ReplaySubject<SyncAssessmentFromGoogleSheetsResponse>(1);
    googleSheets.syncFromGoogleSheets.and.returnValue(syncResponse.asObservable());
    results.get.and.returnValue(of(snapshot('student-1', 'B', 'Từ portal')));

    const syncing = component.onSyncConfirmed({});
    await Promise.resolve();
    expect(component.syncing).toBeTrue();
    expect(component.canEdit).toBeFalse();
    expect(component.saveDisabled).toBeTrue();
    expect(studentWidget.option).toHaveBeenCalledWith({ disabled: true });
    expect(syncWidget.option).toHaveBeenCalledWith({ text: 'Đang đồng bộ…', disabled: true });

    syncResponse.next(syncResult());
    syncResponse.complete();
    await syncing;

    expect(results.get).toHaveBeenCalledOnceWith('student-1');
    expect(component.drafts[0].grade).toBe('B');
    expect(component.dirtyCount).toBe(0);
    expect(component.syncing).toBeFalse();
    expect(studentWidget.option).toHaveBeenCalledWith({ disabled: false });
    expect(syncWidget.option).toHaveBeenCalledWith({ text: 'Đồng bộ GGSheet', disabled: false });
  });

  it('discards the stale draft and requires a DB reload when sync succeeds but snapshot reload fails', async () => {
    applySnapshot(component, snapshot('student-1'));
    component.drafts[0].grade = 'D';
    spyOn<any>(component, 'confirmSyncWithDraft').and.returnValue(Promise.resolve(true));
    googleSheets.syncFromGoogleSheets.and.returnValue(of(syncResult()));
    results.get.and.returnValue(throwError(() => new ApiError('Không tải được dữ liệu portal.', 500)));

    await component.onSyncConfirmed({});

    expect(component.drafts).toEqual([]);
    expect(component.dirtyCount).toBe(0);
    expect(component.reloadRequired).toBeTrue();
    expect(component.syncRequired).toBeFalse();
    expect(component.canEdit).toBeFalse();
    expect(component.saveDisabled).toBeTrue();
    expect(component.conflictMessage).toContain('Cần tải lại dữ liệu học sinh');
  });

  it('preserves dirty rows across client-side filter and page operations', () => {
    applySnapshot(component, snapshot('student-1'));
    component.drafts[1].note = 'Chưa lưu';

    component.onRowUpdated({ rowIndex: 1 });

    expect(component.dirtyCount).toBe(1);
    expect(component.drafts[1].note).toBe('Chưa lưu');
  });

  it('registers the before-unload warning only while a draft is dirty', () => {
    applySnapshot(component, snapshot('student-1'));
    const event = { preventDefault: jasmine.createSpy('preventDefault') } as unknown as BeforeUnloadEvent;

    component.beforeUnload(event);
    expect(event.preventDefault).not.toHaveBeenCalled();
    component.drafts[0].grade = 'A';
    component.beforeUnload(event);
    expect(event.preventDefault).toHaveBeenCalled();
    expect(event.returnValue).toBe('');
  });

  it('scrolls the sticky-bar arrow actions to the start and end of the page', () => {
    const title = document.createElement('h1');
    title.id = 'assessment-results-title';
    const footer = document.createElement('app-footer');
    document.body.append(title, footer);
    const scrollTitleIntoView = spyOn(title, 'scrollIntoView');
    const scrollFooterIntoView = spyOn(footer, 'scrollIntoView');

    try {
      component.scrollToTop();
      component.scrollToBottom();

      expect(scrollTitleIntoView).toHaveBeenCalledOnceWith({ behavior: 'smooth', block: 'start' });
      expect(scrollFooterIntoView).toHaveBeenCalledOnceWith({ behavior: 'smooth', block: 'end' });
    } finally {
      title.remove();
      footer.remove();
    }
  });
});

function applySnapshot(component: AssessmentResultsComponent, value: AssessmentResultsSnapshot): void {
  (component as any).applySnapshot(value);
}

function snapshot(
  studentId: string,
  grade: 'A' | 'B' | 'C' | 'D' | null = null,
  note: string | null = null
): AssessmentResultsSnapshot {
  return {
    student: {
      id: studentId,
      studentCode: `HS-${studentId}`,
      fullName: 'Nguyễn An',
      nickName: 'Bé An',
      dateOfBirth: '2020-01-01',
      status: 'Active'
    },
    items: [
      {
        assessmentId: 'assessment-1', code: 'A-01', name: 'Nội dung 1', rowIndex: 10,
        groupLv1Name: '4-5 tuổi', groupLv2Name: 'Phát triển thể chất', groupLv3Name: 'Vận động',
        grade, note, version: 'version-1'
      },
      {
        assessmentId: 'assessment-2', code: 'A-02', name: 'Nội dung 2', rowIndex: 11,
        groupLv1Name: '4-5 tuổi', groupLv2Name: 'Phát triển thể chất', groupLv3Name: 'Vận động',
        grade: null, note: null, version: 'version-2'
      }
    ]
  };
}

function syncResult(): SyncAssessmentFromGoogleSheetsResponse {
  return {
    sheetsTotalRows: 2,
    databaseTotalRows: 2,
    insertedRows: 0,
    updatedRows: 1,
    deletedRows: 0,
    replacedRecordSnapshots: 0
  };
}
