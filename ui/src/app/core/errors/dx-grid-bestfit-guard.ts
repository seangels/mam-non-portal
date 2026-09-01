/**
 * Vá tại chỗ cho lỗi DevExtreme 19.2.5 (bị pin):
 *
 *   TypeError: Cannot read properties of null (reading 'css')
 *     at _toggleBestFitMode (ui.grid_core.grid_view.js)
 *     at _synchronizeColumns (ui.grid_core.grid_view.js)   ← qua deferRender / promise .always
 *
 * Trong `ui.grid_core.grid_view.js`, `_synchronizeColumns` / `_toggleBestFitMode` / `updateDimensions`
 * nằm trên **`ResizingController`** (`grid._controllers.resizing`), KHÔNG phải view `gridView`.
 * `updateDimensions` lên lịch `_synchronizeColumns` qua `deferRender`; nếu người dùng rời màn trước khi
 * callback chạy, widget đã dispose → `_rowsView._getTableElement()` trả `null` → ném lỗi (Uncaught in
 * promise), làm hỏng lượt điều hướng.
 *
 * `AppErrorHandler` là chốt chặn cuối; hàm này ghi đè own-property trên đúng controller `resizing` của
 * lưới để bỏ qua khi bảng đã detach/disposed, chặn lỗi từ gốc (không cần bật/replay điều hướng).
 *
 * Trả về `true` nếu đã vá được (để caller không thử lại).
 */
export function patchGridBestFit(component: unknown, logPrefix: string): boolean {
  const grid = component as { _controllers?: Record<string, unknown> } | undefined;
  const resizing = grid?._controllers?.['resizing'] as
    | (Record<string, unknown> & {
        _rowsView?: { _getTableElement?: () => unknown } | null;
        component?: { _disposed?: boolean };
        __bestFitGuarded?: boolean;
      })
    | undefined;

  if (!resizing) {
    // eslint-disable-next-line no-console
    console.debug(`${logPrefix} patchGridBestFit — chưa có _controllers.resizing, thử lại sau (onContentReady)`);
    return false;
  }
  if (resizing.__bestFitGuarded) {
    return true;
  }

  for (const name of ['_synchronizeColumns', '_toggleBestFitMode']) {
    const original = resizing[name];
    if (typeof original !== 'function') {
      continue;
    }
    const originalFn = original as (...args: unknown[]) => unknown;
    resizing[name] = function (this: unknown, ...args: unknown[]): unknown {
      const rowsView = resizing._rowsView;
      const table = rowsView && typeof rowsView._getTableElement === 'function'
        ? rowsView._getTableElement()
        : null;
      if (!rowsView || table === null || table === undefined || (resizing.component && resizing.component._disposed)) {
        // eslint-disable-next-line no-console
        console.debug(`${logPrefix} bestFitGuard — bỏ qua ${name}() vì lưới đã detach/disposed`);
        return undefined;
      }
      try {
        return originalFn.apply(this, args);
      } catch (error) {
        if (error instanceof TypeError && /null|undefined/.test((error as Error).message)) {
          // eslint-disable-next-line no-console
          console.debug(`${logPrefix} bestFitGuard — nuốt TypeError null trong ${name}()`);
          return undefined;
        }
        throw error;
      }
    };
  }

  resizing.__bestFitGuarded = true;
  // eslint-disable-next-line no-console
  console.debug(`${logPrefix} patchGridBestFit — đã bọc _synchronizeColumns + _toggleBestFitMode trên ResizingController`);
  return true;
}
