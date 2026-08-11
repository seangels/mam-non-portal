import { Component, HostListener, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { confirm } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';
import { DxFormComponent } from 'devextreme-angular/ui/form';
import { ApiError } from '../../core/models/api-error';
import {
  CreateTeacherRequest,
  TeacherDetail,
  UpdateTeacherRequest,
  UserStatus
} from '../../core/models/api.models';
import { USER_STATUS_LABELS } from '../../core/i18n/ui-labels';
import { TeachersService } from '../../core/services/teachers.service';

export interface TeacherEditor {
  teacherCode: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  status: UserStatus;
  note: string;
  password: string;
  confirmPassword: string;
}

export function buildCreateTeacherRequest(editor: TeacherEditor): CreateTeacherRequest {
  return {
    teacherCode: editor.teacherCode.trim().toUpperCase(),
    fullName: editor.fullName.trim(),
    email: editor.email.trim(),
    phoneNumber: editor.phoneNumber.trim() || null,
    status: editor.status,
    password: editor.password,
    note: editor.note.trim() || null
  };
}

export function buildUpdateTeacherRequest(editor: TeacherEditor, expectedVersion: number): UpdateTeacherRequest {
  const request = buildCreateTeacherRequest(editor);
  return {
    teacherCode: request.teacherCode,
    fullName: request.fullName,
    email: request.email,
    phoneNumber: request.phoneNumber,
    status: request.status,
    note: request.note,
    expectedVersion
  };
}

@Component({
  selector: 'app-teacher-form',
  templateUrl: './teacher-form.component.html',
  styleUrls: ['./teacher-form.component.scss']
})
export class TeacherFormComponent implements OnInit {
  @ViewChild(DxFormComponent) form?: DxFormComponent;

  readonly passwordPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{12,128}$/;
  readonly passwordRuleMessage = 'Mật khẩu phải dài 12–128 ký tự và có chữ hoa, chữ thường, số, ký tự đặc biệt.';
  readonly statuses = [
    { value: 'Active', text: USER_STATUS_LABELS.Active },
    { value: 'Inactive', text: USER_STATUS_LABELS.Inactive },
    { value: 'Locked', text: USER_STATUS_LABELS.Locked }
  ];
  readonly normalizeCodeOnBlur = () => {
    this.editor.teacherCode = this.editor.teacherCode.trim().toUpperCase();
  };

  isCreate = true;
  teacherId = '';
  expectedVersion = 0;
  editor: TeacherEditor = this.emptyEditor();
  loading = false;
  saving = false;
  loadError = '';
  formError = '';
  conflict = false;
  private baseline = this.serialize(this.editor);

  get title(): string {
    return this.isCreate ? 'Thêm giáo viên' : 'Chỉnh sửa giáo viên';
  }

  get subtitle(): string {
    return this.isCreate
      ? 'Tạo đồng thời hồ sơ giáo viên và tài khoản đăng nhập.'
      : 'Cập nhật hồ sơ và thông tin tài khoản giáo viên.';
  }

  get dirty(): boolean {
    return !this.loading && this.serialize(this.editor) !== this.baseline;
  }

  constructor(
    private readonly teachers: TeachersService,
    private readonly route: ActivatedRoute,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.isCreate = this.route.snapshot.data['mode'] === 'create';
    if (this.isCreate) {
      this.editor = this.emptyEditor();
      this.baseline = this.serialize(this.editor);
      return;
    }

    this.teacherId = this.route.snapshot.paramMap.get('id') ?? '';
    if (this.teacherId) {
      void this.load(this.teacherId);
    } else {
      this.loadError = 'Không tìm thấy mã giáo viên trong đường dẫn.';
    }
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.dirty && !this.saving) {
      event.preventDefault();
      event.returnValue = '';
    }
  }

  async canLeave(): Promise<boolean> {
    if (!this.dirty || this.saving) {
      return true;
    }
    return confirm('Bạn có thay đổi chưa lưu. Rời trang và bỏ các thay đổi này?', 'Xác nhận rời trang');
  }

  async save(event: Event): Promise<void> {
    event.preventDefault();
    if (this.saving || this.loading) {
      return;
    }

    this.normalizeCodeOnBlur();
    const validation = this.form?.instance.validate();
    if (validation && !validation.isValid) {
      this.formError = 'Vui lòng kiểm tra các trường được đánh dấu.';
      const firstRule = validation.brokenRules?.[0] as unknown as { validator?: { focus?: () => void } } | undefined;
      firstRule?.validator?.focus?.();
      return;
    }
    if (this.isCreate && this.editor.password !== this.editor.confirmPassword) {
      this.formError = 'Mật khẩu xác nhận không khớp.';
      this.form?.instance.getEditor('confirmPassword')?.focus();
      return;
    }

    this.saving = true;
    this.formError = '';
    this.conflict = false;
    try {
      const saved = this.isCreate
        ? await firstValueFrom(this.teachers.create(buildCreateTeacherRequest(this.editor)))
        : await firstValueFrom(this.teachers.update(
          this.teacherId,
          buildUpdateTeacherRequest(this.editor, this.expectedVersion)
        ));
      this.applyTeacher(saved);
      notify(this.isCreate ? 'Đã tạo giáo viên.' : 'Đã cập nhật giáo viên.', 'success', 2000);
      await this.router.navigate(['/teachers', saved.id]);
    } catch (error) {
      const apiError = ApiError.from(error);
      this.formError = this.withTrace(apiError);
      this.conflict = apiError.code === 'TeacherVersionConflict';
      this.focusFirstServerField(apiError);
    } finally {
      this.saving = false;
    }
  }

  async reloadLatest(): Promise<void> {
    if (!this.teacherId) {
      return;
    }
    if (this.dirty) {
      const accepted = await confirm('Tải dữ liệu mới nhất và bỏ toàn bộ thay đổi đang nhập?', 'Xác nhận tải lại');
      if (!accepted) {
        return;
      }
    }
    await this.load(this.teacherId);
  }

  cancel(): void {
    void this.router.navigate(this.isCreate ? ['/teachers'] : ['/teachers', this.teacherId]);
  }

  private async load(id: string): Promise<void> {
    this.loading = true;
    this.loadError = '';
    this.formError = '';
    this.conflict = false;
    try {
      this.applyTeacher(await firstValueFrom(this.teachers.get(id)));
    } catch (error) {
      this.loadError = this.withTrace(ApiError.from(error));
    } finally {
      this.loading = false;
    }
  }

  private applyTeacher(teacher: TeacherDetail): void {
    this.teacherId = teacher.id;
    this.expectedVersion = teacher.version;
    this.editor = {
      teacherCode: teacher.teacherCode,
      fullName: teacher.fullName,
      email: teacher.email,
      phoneNumber: teacher.phoneNumber ?? '',
      status: teacher.status,
      note: teacher.note ?? '',
      password: '',
      confirmPassword: ''
    };
    this.baseline = this.serialize(this.editor);
  }

  private focusFirstServerField(error: ApiError): void {
    const key = Object.keys(error.fieldErrors)[0];
    if (!key) {
      return;
    }
    const field = key.charAt(0).toLowerCase() + key.slice(1);
    this.form?.instance.getEditor(field)?.focus();
  }

  private emptyEditor(): TeacherEditor {
    return {
      teacherCode: '',
      fullName: '',
      email: '',
      phoneNumber: '',
      status: 'Active',
      note: '',
      password: '',
      confirmPassword: ''
    };
  }

  private serialize(editor: TeacherEditor): string {
    return JSON.stringify(editor);
  }

  private withTrace(error: ApiError): string {
    return error.traceId ? `${error.message} Mã tra cứu: ${error.traceId}` : error.message;
  }
}
