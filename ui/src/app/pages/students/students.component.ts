import { CommonModule } from '@angular/common';
import { Component, NgModule, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import CustomStore from 'devextreme/data/custom_store';
import { confirm } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxDataGridComponent, DxDataGridModule } from 'devextreme-angular/ui/data-grid';
import { DxDateBoxModule } from 'devextreme-angular/ui/date-box';
import { DxFormModule } from 'devextreme-angular/ui/form';
import { DxPopupModule } from 'devextreme-angular/ui/popup';
import { DxSelectBoxModule } from 'devextreme-angular/ui/select-box';
import { DxTextBoxModule } from 'devextreme-angular/ui/text-box';
import { ApiError } from '../../core/models/api-error';
import { CreateStudentRequest, Gender, Student, StudentStatus } from '../../core/models/api.models';
import { StudentsService } from '../../core/services/students.service';
import { GENDER_LABELS, STUDENT_STATUS_LABELS } from '../../core/i18n/ui-labels';
import { fromDateOnly, toDateOnly } from '../../core/utils/date-only';

interface StudentEditor {
  id?: string;
  studentCode: string;
  fullName: string;
  nickName: string;
  dateOfBirth: Date | null;
  gender: Gender | null;
  status: StudentStatus;
  guardianName: string;
  guardianPhone: string;
  note: string;
}

@Component({
  selector: 'app-students',
  templateUrl: './students.component.html',
  styleUrls: ['./students.component.scss']
})
export class StudentsComponent {
  @ViewChild(DxDataGridComponent) grid?: DxDataGridComponent;

  readonly genders = [
    { value: 'Male', text: GENDER_LABELS.Male },
    { value: 'Female', text: GENDER_LABELS.Female },
    { value: 'Other', text: GENDER_LABELS.Other }
  ];
  readonly statuses = [
    { value: 'Active', text: STUDENT_STATUS_LABELS.Active },
    { value: 'Inactive', text: STUDENT_STATUS_LABELS.Inactive }
  ];
  readonly rowButtons = [
    { hint: 'Chỉnh sửa', icon: 'edit', onClick: (event: any) => this.openEdit(event.row.data as Student) },
    { hint: 'Xóa', icon: 'trash', onClick: (event: any) => this.remove(event.row.data as Student) }
  ];

  search = '';
  genderFilter: Gender | null = null;
  statusFilter: StudentStatus | null = null;
  dateOfBirthFrom: Date | string | number = '';
  dateOfBirthTo: Date | string | number = '';
  editorVisible = false;
  saving = false;
  isEditing = false;
  editor: StudentEditor = this.emptyEditor();
  readonly today = new Date();

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

      return firstValueFrom(this.students.list({
        page: Math.floor((loadOptions.skip ?? 0) / pageSize) + 1,
        pageSize,
        search: this.search.trim() || undefined,
        gender: this.genderFilter ?? undefined,
        status: this.statusFilter ?? undefined,
        dateOfBirthFrom: toDateOnly(this.dateOfBirthFrom),
        dateOfBirthTo: toDateOnly(this.dateOfBirthTo),
        sortBy,
        sortOrder
      })).then(response => ({ data: response.items, totalCount: response.pagination.totalItems }))
        .catch(error => this.rejectLoad(error));
    }
  });

  get editorTitle(): string {
    return this.isEditing ? 'Cập nhật học sinh' : 'Thêm học sinh';
  }

  constructor(private readonly students: StudentsService) {}

  applyFilters(): void {
    if (this.dateOfBirthFrom && this.dateOfBirthTo && new Date(this.dateOfBirthFrom) > new Date(this.dateOfBirthTo)) {
      notify('Khoảng ngày sinh không hợp lệ.', 'error', 2200);
      return;
    }
    this.grid?.instance.pageIndex(0);
    this.grid?.instance.refresh();
  }

  resetFilters(): void {
    this.search = '';
    this.genderFilter = null;
    this.statusFilter = null;
    this.dateOfBirthFrom = '';
    this.dateOfBirthTo = '';
    this.applyFilters();
  }

  openCreate(): void {
    this.isEditing = false;
    this.editor = this.emptyEditor();
    this.editorVisible = true;
  }

  openEdit(student: Student): void {
    this.isEditing = true;
    this.editor = {
      id: student.id,
      studentCode: student.studentCode,
      fullName: student.fullName,
      nickName: student.nickName,
      dateOfBirth: fromDateOnly(student.dateOfBirth),
      gender: student.gender ?? null,
      status: student.status,
      guardianName: student.guardianName ?? '',
      guardianPhone: student.guardianPhone ?? '',
      note: student.note ?? ''
    };
    this.editorVisible = true;
  }

  async save(event: Event): Promise<void> {
    event.preventDefault();
    const dateOfBirth = toDateOnly(this.editor.dateOfBirth);
    if (!dateOfBirth) {
      notify('Vui lòng nhập ngày sinh.', 'error', 2200);
      return;
    }

    const request: CreateStudentRequest = {
      studentCode: this.editor.studentCode.trim(),
      fullName: this.editor.fullName.trim(),
      nickName: this.editor.nickName.trim(),
      dateOfBirth,
      gender: this.editor.gender,
      status: this.editor.status,
      guardianName: this.editor.guardianName.trim() || null,
      guardianPhone: this.editor.guardianPhone.trim() || null,
      note: this.editor.note.trim() || null
    };

    this.saving = true;
    try {
      if (this.isEditing && this.editor.id) {
        await firstValueFrom(this.students.update(this.editor.id, request));
        notify('Đã cập nhật học sinh.', 'success', 1800);
      } else {
        await firstValueFrom(this.students.create(request));
        notify('Đã thêm học sinh.', 'success', 1800);
      }
      this.editorVisible = false;
      await this.grid?.instance.refresh();
    } catch (error) {
      this.showError(error);
    } finally {
      this.saving = false;
    }
  }

  async remove(student: Student): Promise<void> {
    const accepted = await confirm(`Xóa học sinh “${student.fullName}” (${student.studentCode})?`, 'Xác nhận xóa');
    if (!accepted) {
      return;
    }

    try {
      await firstValueFrom(this.students.delete(student.id));
      notify('Đã xóa học sinh.', 'success', 1800);
      await this.grid?.instance.refresh();
    } catch (error) {
      this.showError(error);
    }
  }

  statusText(status: StudentStatus): string {
    return this.statuses.find(item => item.value === status)?.text ?? status;
  }

  genderText(gender: Gender | null): string {
    return this.genders.find(item => item.value === gender)?.text ?? '—';
  }

  private emptyEditor(): StudentEditor {
    return {
      studentCode: '',
      fullName: '',
      nickName: '',
      dateOfBirth: null,
      gender: null,
      status: 'Active',
      guardianName: '',
      guardianPhone: '',
      note: ''
    };
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
  declarations: [StudentsComponent],
  imports: [
    CommonModule,
    FormsModule,
    DxButtonModule,
    DxDataGridModule,
    DxDateBoxModule,
    DxFormModule,
    DxPopupModule,
    DxSelectBoxModule,
    DxTextBoxModule
  ],
  exports: [StudentsComponent]
})
export class StudentsModule {}
