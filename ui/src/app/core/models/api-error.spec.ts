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
});
