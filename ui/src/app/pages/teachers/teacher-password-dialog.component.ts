import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import notify from 'devextreme/ui/notify';
import { ApiError } from '../../core/models/api-error';
import { Teacher } from '../../core/models/api.models';
import { TeachersService } from '../../core/services/teachers.service';

@Component({
  selector: 'app-teacher-password-dialog',
  template: `
    <dx-popup
      [(visible)]="visible"
      title="Đổi mật khẩu giáo viên"
      [width]="460"
      [height]="'auto'"
      [showCloseButton]="true"
      [closeOnOutsideClick]="!saving"
      (onHidden)="close()">
      <form *dxTemplate="let _ of 'content'" (submit)="save($event)">
        <p *ngIf="teacher">Giáo viên: <strong>{{ teacher.fullName }}</strong></p>
        <dx-form [formData]="editor" [disabled]="saving" labelLocation="top">
          <dxi-item dataField="password" editorType="dxTextBox" [editorOptions]="{ mode: 'password', valueChangeEvent: 'input' }">
            <dxo-label text="Mật khẩu mới"></dxo-label>
            <dxi-validation-rule type="required" message="Vui lòng nhập mật khẩu mới"></dxi-validation-rule>
            <dxi-validation-rule type="pattern" [pattern]="passwordPattern" [message]="passwordRuleMessage"></dxi-validation-rule>
          </dxi-item>
          <dxi-item dataField="confirmPassword" editorType="dxTextBox" [editorOptions]="{ mode: 'password', valueChangeEvent: 'input' }">
            <dxo-label text="Xác nhận mật khẩu"></dxo-label>
            <dxi-validation-rule type="required" message="Vui lòng xác nhận mật khẩu"></dxi-validation-rule>
          </dxi-item>
          <dxi-item itemType="button" horizontalAlignment="right">
            <dxo-button-options text="Đổi mật khẩu" type="default" [useSubmitBehavior]="true" [disabled]="saving"></dxo-button-options>
          </dxi-item>
        </dx-form>
      </form>
    </dx-popup>
  `
})
export class TeacherPasswordDialogComponent {
  @Input() teacher: Teacher | null = null;
  @Input() visible = false;
  @Output() readonly visibleChange = new EventEmitter<boolean>();
  @Output() readonly completed = new EventEmitter<void>();

  readonly passwordPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{12,128}$/;
  readonly passwordRuleMessage = 'Mật khẩu phải dài 12–128 ký tự và có chữ hoa, chữ thường, số, ký tự đặc biệt.';
  editor = { password: '', confirmPassword: '' };
  saving = false;

  constructor(private readonly teachers: TeachersService) {}

  async save(event: Event): Promise<void> {
    event.preventDefault();
    if (!this.teacher || this.saving) {
      return;
    }
    if (this.editor.password !== this.editor.confirmPassword) {
      notify('Mật khẩu xác nhận không khớp.', 'error', 2400);
      return;
    }

    this.saving = true;
    try {
      await firstValueFrom(this.teachers.changePassword(this.teacher.userId, { password: this.editor.password }));
      notify('Đã đổi mật khẩu và thu hồi các phiên đăng nhập cũ.', 'success', 2400);
      this.completed.emit();
      this.setVisible(false);
    } catch (error) {
      const apiError = ApiError.from(error);
      notify(this.withTrace(apiError), 'error', 3500);
    } finally {
      this.saving = false;
    }
  }

  close(): void {
    this.editor = { password: '', confirmPassword: '' };
    this.setVisible(false);
  }

  private setVisible(value: boolean): void {
    if (this.visible !== value) {
      this.visible = value;
      this.visibleChange.emit(value);
    }
  }

  private withTrace(error: ApiError): string {
    return error.traceId ? `${error.message} Mã tra cứu: ${error.traceId}` : error.message;
  }
}
