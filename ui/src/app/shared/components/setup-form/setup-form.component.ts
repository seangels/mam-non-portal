import { CommonModule } from '@angular/common';
import { Component, NgModule } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { DxFormModule } from 'devextreme-angular/ui/form';
import { DxLoadIndicatorModule } from 'devextreme-angular/ui/load-indicator';
import notify from 'devextreme/ui/notify';
import { ApiError } from '../../../core/models/api-error';
import { SetupService } from '../../../core/services/setup.service';

interface SetupFormData {
  email: string;
  fullName: string;
  password: string;
  confirmPassword: string;
}

@Component({
  selector: 'app-setup-form',
  templateUrl: './setup-form.component.html',
  styleUrls: ['./setup-form.component.scss']
})
export class SetupFormComponent {
  readonly passwordPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{12,128}$/;
  readonly passwordComparison = () => this.formData.password;
  loading = false;
  formData: SetupFormData = {
    email: '',
    fullName: '',
    password: '',
    confirmPassword: ''
  };

  get statusError(): string | null {
    return this.setup.state === 'error'
      ? this.setup.error?.message ?? 'Không thể kiểm tra trạng thái hệ thống.'
      : null;
  }

  constructor(private readonly setup: SetupService, private readonly router: Router) {}

  async retryStatus(): Promise<void> {
    this.loading = true;
    const state = await this.setup.loadStatus();
    this.loading = false;
    if (state === 'complete') {
      await this.router.navigate(['/login-form']);
    }
  }

  async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.formData.password !== this.formData.confirmPassword) {
      notify('Mật khẩu xác nhận không khớp.', 'error', 2500);
      return;
    }

    this.loading = true;
    try {
      await this.setup.createSuperAdmin({
        email: this.formData.email.trim(),
        fullName: this.formData.fullName.trim(),
        password: this.formData.password
      });
      notify('Đã khởi tạo siêu quản trị viên. Vui lòng đăng nhập.', 'success', 2500);
      await this.router.navigate(['/login-form']);
    } catch (error) {
      const apiError = ApiError.from(error);
      if (apiError.status === 409) {
        await this.setup.loadStatus();
        notify('Hệ thống đã được khởi tạo.', 'info', 2000);
        await this.router.navigate(['/login-form']);
      } else {
        notify(apiError.message, 'error', 3000);
      }
    } finally {
      this.loading = false;
    }
  }
}

@NgModule({
  imports: [CommonModule, RouterModule, DxFormModule, DxLoadIndicatorModule],
  declarations: [SetupFormComponent],
  exports: [SetupFormComponent]
})
export class SetupFormModule {}
