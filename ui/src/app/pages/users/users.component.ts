import { Component, NgModule, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import CustomStore from 'devextreme/data/custom_store';
import { confirm } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxDataGridComponent, DxDataGridModule } from 'devextreme-angular/ui/data-grid';
import { DxFormModule } from 'devextreme-angular/ui/form';
import { DxPopupModule } from 'devextreme-angular/ui/popup';
import { DxSelectBoxModule } from 'devextreme-angular/ui/select-box';
import { DxTextBoxModule } from 'devextreme-angular/ui/text-box';
import { ApiError } from '../../core/models/api-error';
import { CreateUserRequest, User, UserStatus } from '../../core/models/api.models';
import { UsersService } from '../../core/services/users.service';
import { USER_STATUS_LABELS } from '../../core/i18n/ui-labels';

interface UserEditor {
  id?: string;
  email: string;
  fullName: string;
  phoneNumber: string;
  status: UserStatus;
  password?: string;
}

@Component({
  selector: 'app-users',
  templateUrl: './users.component.html',
  styleUrls: ['./users.component.scss']
})
export class UsersComponent {
  @ViewChild(DxDataGridComponent) grid?: DxDataGridComponent;

  readonly passwordPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{12,128}$/;
  readonly passwordRuleMessage = 'Mật khẩu phải dài 12–128 ký tự và có chữ hoa, chữ thường, số, ký tự đặc biệt.';
  readonly userStatuses = [
    { value: 'Active', text: USER_STATUS_LABELS.Active },
    { value: 'Inactive', text: USER_STATUS_LABELS.Inactive },
    { value: 'Locked', text: USER_STATUS_LABELS.Locked }
  ];
  readonly rowButtons = [
    { hint: 'Chỉnh sửa', icon: 'edit', onClick: (event: any) => this.openEdit(event.row.data as User) },
    { hint: 'Đổi mật khẩu', icon: 'key', onClick: (event: any) => this.openPassword(event.row.data as User) },
    { hint: 'Xóa', icon: 'trash', onClick: (event: any) => this.remove(event.row.data as User) }
  ];

  search = '';
  statusFilter: UserStatus | null = null;
  editorVisible = false;
  passwordVisible = false;
  saving = false;
  isEditing = false;
  editor: UserEditor = this.emptyEditor();
  passwordEditor = { password: '', confirmPassword: '' };

  readonly dataSource = new CustomStore({
    key: 'id',
    load: loadOptions => {
      const pageSize = loadOptions.take ?? 20;
      const sort = Array.isArray(loadOptions.sort) ? loadOptions.sort[0] : loadOptions.sort;
      const sortConfig = sort && typeof sort === 'object' ? sort : undefined;
      const sortBy = sortConfig && typeof sortConfig.selector === 'string'
        ? sortConfig.selector
        : 'createdAt';
      const sortOrder = sortConfig?.desc ? 'desc' : 'asc';

      return firstValueFrom(this.users.list({
        page: Math.floor((loadOptions.skip ?? 0) / pageSize) + 1,
        pageSize,
        search: this.search.trim() || undefined,
        role: 'Admin',
        status: this.statusFilter ?? undefined,
        sortBy,
        sortOrder
      })).then(response => ({ data: response.items, totalCount: response.pagination.totalItems }))
        .catch(error => this.rejectLoad(error));
    }
  });

  get editorTitle(): string {
    return this.isEditing ? 'Cập nhật tài khoản quản trị' : 'Thêm quản trị viên';
  }

  constructor(private readonly users: UsersService) {}

  applyFilters(): void {
    this.grid?.instance.pageIndex(0);
    this.grid?.instance.refresh();
  }

  resetFilters(): void {
    this.search = '';
    this.statusFilter = null;
    this.applyFilters();
  }

  openCreate(): void {
    this.isEditing = false;
    this.editor = this.emptyEditor();
    this.editorVisible = true;
  }

  openEdit(user: User): void {
    this.isEditing = true;
    this.editor = {
      id: user.id,
      email: user.email,
      fullName: user.fullName,
      phoneNumber: user.phoneNumber ?? '',
      status: user.status
    };
    this.editorVisible = true;
  }

  async save(event: Event): Promise<void> {
    event.preventDefault();
    this.saving = true;
    try {
      if (this.isEditing && this.editor.id) {
        await firstValueFrom(this.users.update(this.editor.id, {
          email: this.editor.email.trim(),
          fullName: this.editor.fullName.trim(),
          phoneNumber: this.editor.phoneNumber.trim() || null,
          role: 'Admin',
          status: this.editor.status
        }));
        notify('Đã cập nhật tài khoản.', 'success', 1800);
      } else {
        const request: CreateUserRequest = {
          email: this.editor.email.trim(),
          fullName: this.editor.fullName.trim(),
          phoneNumber: this.editor.phoneNumber.trim() || null,
          role: 'Admin',
          status: this.editor.status,
          password: this.editor.password ?? ''
        };
        await firstValueFrom(this.users.create(request));
        notify('Đã tạo tài khoản.', 'success', 1800);
      }
      this.editorVisible = false;
      await this.grid?.instance.refresh();
    } catch (error) {
      this.showError(error);
    } finally {
      this.saving = false;
    }
  }

  openPassword(user: User): void {
    this.editor = { ...this.editor, id: user.id };
    this.passwordEditor = { password: '', confirmPassword: '' };
    this.passwordVisible = true;
  }

  async changePassword(event: Event): Promise<void> {
    event.preventDefault();
    if (this.passwordEditor.password !== this.passwordEditor.confirmPassword) {
      notify('Mật khẩu xác nhận không khớp.', 'error', 2200);
      return;
    }

    if (!this.editor.id) {
      return;
    }

    this.saving = true;
    try {
      await firstValueFrom(this.users.changePassword(this.editor.id, { password: this.passwordEditor.password }));
      this.passwordVisible = false;
      notify('Đã đổi mật khẩu và thu hồi các phiên đăng nhập cũ.', 'success', 2200);
    } catch (error) {
      this.showError(error);
    } finally {
      this.saving = false;
    }
  }

  async remove(user: User): Promise<void> {
    const accepted = await confirm(`Xóa tài khoản “${user.fullName}”?`, 'Xác nhận xóa');
    if (!accepted) {
      return;
    }

    try {
      await firstValueFrom(this.users.delete(user.id));
      notify('Đã xóa tài khoản.', 'success', 1800);
      await this.grid?.instance.refresh();
    } catch (error) {
      this.showError(error);
    }
  }

  statusText(status: UserStatus): string {
    return this.userStatuses.find(item => item.value === status)?.text ?? status;
  }

  private emptyEditor(): UserEditor {
    return { email: '', fullName: '', phoneNumber: '', status: 'Active', password: '' };
  }

  private rejectLoad(error: unknown): Promise<never> {
    const apiError = ApiError.from(error);
    notify(apiError.message, 'error', 2500);
    return Promise.reject(apiError);
  }

  private showError(error: unknown): void {
    const apiError = ApiError.from(error);
    notify(apiError.message, 'error', 2800);
  }
}

@NgModule({
  declarations: [UsersComponent],
  imports: [
    CommonModule,
    FormsModule,
    DxButtonModule,
    DxDataGridModule,
    DxFormModule,
    DxPopupModule,
    DxSelectBoxModule,
    DxTextBoxModule
  ],
  exports: [UsersComponent]
})
export class UsersModule {}
