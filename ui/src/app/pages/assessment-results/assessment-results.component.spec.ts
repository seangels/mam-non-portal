import { of, ReplaySubject, throwError } from 'rxjs';
import { ApiError } from '../../core/models/api-error';
import { AssessmentResultsSnapshot } from '../../core/models/api.models.assessment-results';
import { AssessmentResultsService } from '../../core/services/assessment-results.service';
import { StudentsService } from '../../core/services/students.service';
import { AssessmentResultsComponent } from './assessment-results.component';

describe('AssessmentResultsComponent', () => {
  let results: jasmine.SpyObj<AssessmentResultsService>;
  let students: jasmine.SpyObj<StudentsService>;
  let component: AssessmentResultsComponent;

  beforeEach(() => {
    results = jasmine.createSpyObj<AssessmentResultsService>('AssessmentResultsService', ['get', 'update']);
    students = jasmine.createSpyObj<StudentsService>('StudentsService', ['list', 'get']);
    students.list.and.returnValue(of({
      items: [],
      pagination: { page: 1, pageSize: 20, totalItems: 0, totalPages: 0 }
    }));
    component = new AssessmentResultsComponent(results, students);
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
    expect(loaded.data[0].status).toBe('Inactive');
    expect(students.list.calls.mostRecent().args[0]).toEqual(jasmine.objectContaining({
      page: 1, pageSize: 20, search: 'cũ'
    }));
    expect(students.list.calls.mostRecent().args[0].status).toBeUndefined();
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

  it('does not offer another save when Google wrote successfully but readback failed', async () => {
    applySnapshot(component, snapshot('student-1'));
    component.drafts[0].grade = 'C';
    results.update.and.returnValue(throwError(() => new ApiError(
      'Google Sheet đã được ghi.', 500, {}, undefined, 'AssessmentResultsPostWriteFailed', undefined, [], true
    )));

    await component.save();

    expect(component.reloadRequired).toBeTrue();
    expect(component.saveDisabled).toBeTrue();
    expect(component.dirtyCount).toBe(1);
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
    const scroll = spyOn(window, 'scrollTo');
    const documentHeight = document.documentElement.scrollHeight;

    component.scrollToTop();
    component.scrollToBottom();

    const topOptions = scroll.calls.argsFor(0)[0] as unknown as ScrollToOptions;
    const bottomOptions = scroll.calls.argsFor(1)[0] as unknown as ScrollToOptions;
    expect(topOptions).toEqual({ top: 0, behavior: 'smooth' });
    expect(bottomOptions).toEqual({ top: documentHeight, behavior: 'smooth' });
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
