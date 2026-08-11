import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './api.models';
import { apiErrorCodeLabel } from '../i18n/ui-labels';

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly fieldErrors: Record<string, string[]> = {},
    public readonly traceId?: string,
    public readonly code?: string,
    public readonly currentVersion?: number
  ) {
    super(message);
    this.name = 'ApiError';
  }

  static from(error: unknown): ApiError {
    if (error instanceof ApiError) {
      return error;
    }

    if (!(error instanceof HttpErrorResponse)) {
      return new ApiError('Không thể kết nối đến máy chủ. Vui lòng thử lại.', 0);
    }

    const problem = (error.error ?? {}) as ProblemDetails;
    const fallback = error.status === 0
      ? 'Không thể kết nối đến máy chủ.'
      : error.status === 401
        ? 'Phiên đăng nhập đã hết hạn.'
        : error.status === 403
          ? 'Bạn không có quyền thực hiện thao tác này.'
          : error.status === 409
            ? 'Dữ liệu đã tồn tại hoặc đang có xung đột.'
            : error.status === 404
              ? 'Không tìm thấy dữ liệu yêu cầu.'
              : 'Yêu cầu không thể thực hiện.';

    return new ApiError(
      apiErrorCodeLabel(problem.code) || fallback,
      error.status,
      problem.errors || {},
      problem.traceId,
      problem.code,
      problem.currentVersion
    );
  }
}
