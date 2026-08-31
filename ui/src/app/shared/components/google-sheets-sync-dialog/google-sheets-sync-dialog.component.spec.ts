import { GoogleSheetsSyncDialogComponent } from './google-sheets-sync-dialog.component';

describe('GoogleSheetsSyncDialogComponent', () => {
  function make(role: 'SuperAdmin' | 'Admin' | 'Teacher') {
    const auth = { user: { role } } as any;
    return new GoogleSheetsSyncDialogComponent(auth);
  }

  it('emits an empty body for the default sync mode', () => {
    const component = make('Admin');
    component.onShowing();
    const emitted: unknown[] = [];
    component.confirmSync.subscribe(v => emitted.push(v));

    component.confirm();

    expect(emitted).toEqual([{}]);
    expect(component.visible).toBeFalse();
  });

  it('builds replaceRecordSnapshots with only the checked fields and statuses for an admin', () => {
    const component = make('Admin');
    component.onShowing();
    component.syncMode = 'replace';
    component.replaceFields = {
      name: true, groupLv1Name: false, groupLv2Name: true, groupLv3Name: false, rowIndex: true
    };
    component.replaceStatuses = { Open: true, Planed: false, Done: true, Canceled: false };

    expect(component.buildRequest()).toEqual({
      replaceRecordSnapshots: {
        name: true,
        groupLv1Name: false,
        groupLv2Name: true,
        groupLv3Name: false,
        rowIndex: true,
        sheetStatuses: ['Open', 'Done']
      }
    });
  });

  it('disables option 2 for a teacher and still emits the default body', () => {
    const component = make('Teacher');
    component.onShowing();
    component.syncMode = 'replace';
    const emitted: unknown[] = [];
    component.confirmSync.subscribe(v => emitted.push(v));

    expect(component.canReplaceSnapshot).toBeFalse();
    expect(component.replaceSelected).toBeFalse();

    component.confirm();
    expect(emitted).toEqual([{}]);
  });

  it('blocks confirm when replace is selected but nothing is checked', () => {
    const component = make('SuperAdmin');
    component.onShowing();
    component.syncMode = 'replace';

    component.replaceFields = {
      name: false, groupLv1Name: false, groupLv2Name: false, groupLv3Name: false, rowIndex: false
    };
    expect(component.confirmDisabled).toBeTrue();

    component.replaceFields = { ...component.replaceFields, name: true };
    component.replaceStatuses = { Open: false, Planed: false, Done: false, Canceled: false };
    expect(component.confirmDisabled).toBeTrue();

    component.replaceStatuses = { Open: false, Planed: false, Done: false, Canceled: true };
    expect(component.confirmDisabled).toBeFalse();
  });

  it('resets to defaults each time the popup shows', () => {
    const component = make('Admin');
    component.syncMode = 'replace';
    component.replaceFields.name = false;
    component.replaceFields.groupLv2Name = true;
    component.replaceStatuses.Done = false;

    component.onShowing();

    expect(component.syncMode).toBe('default');
    // Default replace scope is intentionally narrow: only the free-text name, no group levels / rowIndex.
    expect(component.replaceFields).toEqual({
      name: true, groupLv1Name: false, groupLv2Name: false, groupLv3Name: false, rowIndex: false
    });
    expect(component.replaceStatuses).toEqual({ Open: true, Planed: true, Done: true, Canceled: true });
  });
});
