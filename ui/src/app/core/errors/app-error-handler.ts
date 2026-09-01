import { ErrorHandler, Injectable, Injector, NgZone } from '@angular/core';
import { NavigationRecoveryService } from './navigation-recovery.service';

/**
 * Nuốt đúng một lỗi vô hại đã biết của DevExtreme 19.2.5 (bị pin theo yêu cầu sản phẩm), còn lại
 * giữ nguyên hành vi mặc định của Angular.
 *
 *   TypeError: Cannot read properties of null (reading 'css')
 *     at _toggleBestFitMode (ui.grid_core.grid_view.js)
 *     at _synchronizeColumns (ui.grid_core.grid_view.js)   ← deferRender / promise .always
 *
 * dxDataGrid/dxTreeList lên lịch `_synchronizeColumns` qua `deferRender`. Nếu người dùng rời màn
 * (bấm "Chỉnh sửa" / "Thêm mới") trước khi callback chạy, Angular đã destroy component → widget bị
 * dispose → `_rowsView._getTableElement()` trả `null`. Callback chạy nốt và ném lỗi này dưới dạng
 * "Uncaught (in promise)". Widget đã biến mất nên lỗi không có tác dụng phụ, chỉ làm bẩn console và
 * làm hỏng microtask hiện tại. Không có API công khai để huỷ deferRender và không thể nâng phiên bản.
 */
@Injectable()
export class AppErrorHandler extends ErrorHandler {
  // Dùng Injector (luôn resolve được) để lấy Router/service lười, tránh vòng phụ thuộc DI lúc khởi tạo.
  constructor(private readonly injector: Injector) {
    super();
  }

  override handleError(error: unknown): void {
    if (AppErrorHandler.isBenignDevExtremeGridTeardownError(error)) {
      // eslint-disable-next-line no-console
      console.debug('[AppErrorHandler] bỏ qua lỗi teardown lưới DevExtreme đã biết:', error);
      this.recoverNavigation();
      return;
    }
    super.handleError(error);
  }

  private recoverNavigation(): void {
    try {
      const zone = this.injector.get(NgZone);
      const recovery = this.injector.get(NavigationRecoveryService);
      zone.run(() => recovery.recoverAfterGridCrash());
    } catch {
      // Khôi phục là best-effort; nuốt lỗi vẫn có tác dụng chính.
    }
  }

  private static isBenignDevExtremeGridTeardownError(error: unknown): boolean {
    // Angular/zone bọc promise rejection: lỗi thật có thể nằm ở `error` hoặc `error.rejection`.
    const wrapped = error as { rejection?: unknown } | null;
    const real = (wrapped && typeof wrapped === 'object' && 'rejection' in wrapped)
      ? wrapped.rejection
      : error;

    if (!(real instanceof TypeError)) {
      return false;
    }
    const message = real.message || '';
    const stack = real.stack || '';
    return /reading '?css'?/.test(message)
      && /_toggleBestFitMode|_synchronizeColumns|grid_core[./]ui\.grid_core\.grid_view|ui\.grid_core\.grid_view/.test(stack);
  }
}
