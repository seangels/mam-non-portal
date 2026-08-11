import { HttpErrorResponse } from '@angular/common/http';
import { ApiError } from './api-error';

describe('ApiError', () => {
  it('maps ProblemDetails including validation and trace id', () => {
    const result = ApiError.from(new HttpErrorResponse({
      status: 400,
      error: {
        title: 'Dữ liệu không hợp lệ',
        detail: 'Nội dung kỹ thuật không được hiển thị',
        code: 'ValidationFailed',
        errors: { email: ['Email không đúng định dạng'] },
        traceId: 'trace-123'
      }
    }));

    expect(result.status).toBe(400);
    expect(result.message).toBe('Một hoặc nhiều thông tin chưa hợp lệ.');
    expect(result.fieldErrors['email']).toEqual(['Email không đúng định dạng']);
    expect(result.traceId).toBe('trace-123');
  });

  it('uses a useful conflict fallback', () => {
    const result = ApiError.from(new HttpErrorResponse({ status: 409 }));
    expect(result.message).toContain('xung đột');
  });

  it('maps attendance conflict code and preserves the current version', () => {
    const result = ApiError.from(new HttpErrorResponse({
      status: 409,
      error: { code: 'SheetVersionConflict', currentVersion: 4, detail: 'Do not expose' }
    }));

    expect(result.message).toContain('người khác cập nhật');
    expect(result.currentVersion).toBe(4);
    expect(result.message).not.toContain('Do not expose');
  });

  it('maps Teacher concurrency without exposing raw ProblemDetails text', () => {
    const result = ApiError.from(new HttpErrorResponse({
      status: 409,
      error: { code: 'TeacherVersionConflict', currentVersion: 8, detail: 'Raw backend detail' }
    }));

    expect(result.message).toContain('giáo viên');
    expect(result.currentVersion).toBe(8);
    expect(result.message).not.toContain('Raw backend detail');
  });

  it('maps Student schedule and concurrency states without exposing raw text', () => {
    const conflict = ApiError.from(new HttpErrorResponse({
      status: 409,
      error: { code: 'StudentVersionConflict', currentVersion: 9, detail: 'Raw backend detail' }
    }));
    const emptyRoster = ApiError.from(new HttpErrorResponse({
      status: 409,
      error: { code: 'NoScheduledStudents', detail: 'Raw backend detail' }
    }));

    expect(conflict.message).toContain('học sinh');
    expect(conflict.currentVersion).toBe(9);
    expect(conflict.message).not.toContain('Raw backend detail');
    expect(emptyRoster.message).toBe('Không có học sinh có lịch học trong ngày này.');
  });
});
