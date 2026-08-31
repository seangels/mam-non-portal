import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, NgModule, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxCheckBoxModule } from 'devextreme-angular/ui/check-box';
import { DxPopupModule } from 'devextreme-angular/ui/popup';
import { DxRadioGroupModule } from 'devextreme-angular/ui/radio-group';
import { SyncAssessmentFromGoogleSheetsRequest } from '../../../core/models/api.models';
import { AssessmentSheetStatus, ASSESSMENT_SHEET_STATUS_OPTIONS } from '../../../core/models/api.models.assessment-sheets';
import { AuthService } from '../../services/auth.service';

type SyncMode = 'default' | 'replace';

interface ReplaceFields {
  name: boolean;
  groupLv1Name: boolean;
  groupLv2Name: boolean;
  groupLv3Name: boolean;
  rowIndex: boolean;
}

/**
 * Popup xác nhận đồng bộ Google Sheets dùng chung cho màn Quản lý Đánh giá và picker tạo bảng đánh giá.
 * Component chỉ thu thập tùy chọn và phát ra request; host tự gọi API và xử lý kết quả.
 */
@Component({
  selector: 'app-google-sheets-sync-dialog',
  templateUrl: './google-sheets-sync-dialog.component.html',
  styleUrls: ['./google-sheets-sync-dialog.component.scss']
})
export class GoogleSheetsSyncDialogComponent {
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  /** Phát khi người dùng bấm "Đồng bộ" với tùy chọn hợp lệ. Popup tự đóng ngay sau đó. */
  @Output() confirmSync = new EventEmitter<SyncAssessmentFromGoogleSheetsRequest>();

  syncMode: SyncMode = 'default';
  readonly syncModeOptions: { value: SyncMode; text: string }[] = [
    { value: 'default', text: 'Đồng bộ mặc định' },
    { value: 'replace', text: 'Đồng bộ + thay thế snapshot trong bảng đánh giá' }
  ];
  readonly sheetStatusOptions = ASSESSMENT_SHEET_STATUS_OPTIONS;
  replaceFields: ReplaceFields = defaultReplaceFields();
  replaceStatuses: Record<AssessmentSheetStatus, boolean> = defaultReplaceStatuses();

  constructor(private readonly auth: AuthService) {}

  /** Tùy chọn 2 chỉ dành cho quản trị (Admin, Super Admin); teacher chỉ đồng bộ mặc định. */
  get canReplaceSnapshot(): boolean {
    const role = this.auth.user?.role;
    return role === 'SuperAdmin' || role === 'Admin';
  }

  get replaceSelected(): boolean {
    return this.syncMode === 'replace' && this.canReplaceSnapshot;
  }

  get anyReplaceFieldChecked(): boolean {
    const f = this.replaceFields;
    return f.name || f.groupLv1Name || f.groupLv2Name || f.groupLv3Name || f.rowIndex;
  }

  get anyReplaceStatusChecked(): boolean {
    return this.sheetStatusOptions.some(option => this.replaceStatuses[option.value]);
  }

  get confirmDisabled(): boolean {
    if (this.replaceSelected) return !this.anyReplaceFieldChecked || !this.anyReplaceStatusChecked;
    return false;
  }

  onShowing(): void {
    this.syncMode = 'default';
    this.replaceFields = defaultReplaceFields();
    this.replaceStatuses = defaultReplaceStatuses();
  }

  setVisible(value: boolean): void {
    if (this.visible === value) return;
    this.visible = value;
    this.visibleChange.emit(value);
  }

  buildRequest(): SyncAssessmentFromGoogleSheetsRequest {
    if (!this.replaceSelected) return {};
    const sheetStatuses = this.sheetStatusOptions
      .map(option => option.value)
      .filter(status => this.replaceStatuses[status]);
    return {
      replaceRecordSnapshots: {
        name: this.replaceFields.name,
        groupLv1Name: this.replaceFields.groupLv1Name,
        groupLv2Name: this.replaceFields.groupLv2Name,
        groupLv3Name: this.replaceFields.groupLv3Name,
        rowIndex: this.replaceFields.rowIndex,
        sheetStatuses
      }
    };
  }

  cancel(): void {
    this.setVisible(false);
  }

  confirm(): void {
    if (this.confirmDisabled) return;
    const request = this.buildRequest();
    this.setVisible(false);
    this.confirmSync.emit(request);
  }
}

function defaultReplaceFields(): ReplaceFields {
  return { name: true, groupLv1Name: false, groupLv2Name: false, groupLv3Name: false, rowIndex: false };
}

function defaultReplaceStatuses(): Record<AssessmentSheetStatus, boolean> {
  return { Open: true, Planed: true, Done: true, Canceled: true };
}

@NgModule({
  declarations: [GoogleSheetsSyncDialogComponent],
  imports: [CommonModule, FormsModule, DxButtonModule, DxCheckBoxModule, DxPopupModule, DxRadioGroupModule],
  exports: [GoogleSheetsSyncDialogComponent]
})
export class GoogleSheetsSyncDialogModule {}
