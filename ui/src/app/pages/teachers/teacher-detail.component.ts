import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription, firstValueFrom } from 'rxjs';
import { confirm } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';
import { ApiError } from '../../core/models/api-error';
import { StudentGroupStatus, TeacherDetail, UserStatus } from '../../core/models/api.models';
import { USER_STATUS_LABELS } from '../../core/i18n/ui-labels';
import { TeachersService } from '../../core/services/teachers.service';

@Component({
  selector: 'app-teacher-detail',
  templateUrl: './teacher-detail.component.html',
  styleUrls: ['./teacher-detail.component.scss']
})
export class TeacherDetailComponent implements OnInit, OnDestroy {
  teacher: TeacherDetail | null = null;
  loading = true;
  loadError = '';
  passwordVisible = false;
  deleting = false;
  private routeSubscription?: Subscription;

  constructor(
    private readonly teachers: TeachersService,
    private readonly route: ActivatedRoute,
    public readonly router: Router
  ) {}

  ngOnInit(): void {
    this.routeSubscription = this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        void this.load(id);
      }
    });
  }

  ngOnDestroy(): void {
    this.routeSubscription?.unsubscribe();
  }

  async reload(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      await this.load(id);
    }
  }

  edit(): void {
    if (this.teacher) {
      void this.router.navigate(['/teachers', this.teacher.id, 'edit']);
    }
  }

  openGroups(policy = false): void {
    void this.router.navigate(['/student-groups'], {
      queryParams: policy && this.teacher ? { tab: 'policy', search: this.teacher.teacherCode } : undefined
    });
  }

  async remove(): Promise<void> {
    if (!this.teacher || this.deleting) {
      return;
    }
    const accepted = await confirm(
      `Xóa giáo viên “${this.teacher.fullName}” (${this.teacher.teacherCode})? Tài khoản sẽ bị vô hiệu hóa và các phiên đăng nhập sẽ bị thu hồi.`,
      'Xác nhận xóa giáo viên'
    );
    if (!accepted) {
      return;
    }

    this.deleting = true;
    try {
      await firstValueFrom(this.teachers.delete(this.teacher.id, this.teacher.version));
      notify('Đã xóa giáo viên và thu hồi các phiên đăng nhập.', 'success', 2400);
      await this.router.navigate(['/teachers']);
    } catch (error) {
      const apiError = ApiError.from(error);
      this.notifyError(apiError);
      if (apiError.code === 'TeacherVersionConflict') {
        await this.reload();
      }
    } finally {
      this.deleting = false;
    }
  }

  statusText(status: UserStatus): string {
    return USER_STATUS_LABELS[status];
  }

  groupStatusText(status: StudentGroupStatus): string {
    return status === 'Active' ? 'Đang hoạt động' : 'Ngừng hoạt động';
  }

  private async load(id: string): Promise<void> {
    this.loading = true;
    this.loadError = '';
    try {
      this.teacher = await firstValueFrom(this.teachers.get(id));
    } catch (error) {
      this.teacher = null;
      this.loadError = this.withTrace(ApiError.from(error));
    } finally {
      this.loading = false;
    }
  }

  private notifyError(error: ApiError): void {
    notify(this.withTrace(error), 'error', 3500);
  }

  private withTrace(error: ApiError): string {
    return error.traceId ? `${error.message} Mã tra cứu: ${error.traceId}` : error.message;
  }
}
