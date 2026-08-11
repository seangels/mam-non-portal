import { HttpErrorResponse } from '@angular/common/http';
import { ApiError } from './api-error';

describe('ApiError', () => {
  it('maps ProblemDetails including validation and trace id', () => {
    const result = ApiError.from(new HttpErrorResponse({
      status: 400,
      error: {
        title: 'Dữ liệu không hợp lệ',
        detail: 'Email không hợp lệ',
        errors: { email: ['Email không đúng định dạng'] },
        traceId: 'trace-123'
      }
    }));

    expect(result.status).toBe(400);
    expect(result.message).toBe('Email không hợp lệ');
    expect(result.fieldErrors['email']).toEqual(['Email không đúng định dạng']);
    expect(result.traceId).toBe('trace-123');
  });

  it('uses a useful conflict fallback', () => {
    const result = ApiError.from(new HttpErrorResponse({ status: 409 }));
    expect(result.message).toContain('xung đột');
  });
});
