import { Injectable } from '@angular/core';
import {
  NavigationCancel,
  NavigationError,
  NavigationStart,
  Router
} from '@angular/router';
import { filter } from 'rxjs/operators';

/**
 * Khôi phục điều hướng sau khi nuốt lỗi teardown lưới DevExtreme (xem `AppErrorHandler`).
 *
 * Khi lỗi đó xảy ra lúc rời màn, đôi khi lượt render đích bị dở dang (màn trắng / kẹt). Trick:
 * bật về `/home` cho sạch trạng thái rồi tự điều hướng lại vào đúng route người dùng vừa bấm.
 *
 * Có log `[NavRecovery]` cho mọi biến cố điều hướng để soi luồng khi debug.
 */
@Injectable({ providedIn: 'root' })
export class NavigationRecoveryService {
  private lastNavigationUrl: string | null = null;
  private recovering = false;

  constructor(private readonly router: Router) {
    this.router.events
      .pipe(filter((event): event is NavigationStart | NavigationCancel | NavigationError =>
        event instanceof NavigationStart
        || event instanceof NavigationCancel
        || event instanceof NavigationError))
      .subscribe(event => this.handleNavigationEvent(event));
  }

  recoverAfterGridCrash(): void {
    if (this.recovering) {
      // eslint-disable-next-line no-console
      console.debug('[NavRecovery] bỏ qua: đang trong lượt khôi phục (debounce)');
      return;
    }
    this.recovering = true;

    const target = this.lastNavigationUrl;
    const shouldReplay = !!target && target !== '/home' && target !== '/';
    // eslint-disable-next-line no-console
    console.debug('[NavRecovery] BẮT ĐẦU khôi phục — route cũ =', target, '| sẽ replay =', shouldReplay);

    void this.router.navigateByUrl('/home')
      .then(ok => {
        // eslint-disable-next-line no-console
        console.debug('[NavRecovery] về /home xong (ok =', ok, ')');
        if (shouldReplay) {
          // eslint-disable-next-line no-console
          console.debug('[NavRecovery] replay route cũ:', target);
          return this.router.navigateByUrl(target as string);
        }
        return true;
      })
      .then(ok => {
        // eslint-disable-next-line no-console
        console.debug('[NavRecovery] HOÀN TẤT khôi phục (ok =', ok, ')');
      })
      .catch(error => {
        // eslint-disable-next-line no-console
        console.debug('[NavRecovery] LỖI khi khôi phục:', error);
      })
      .finally(() => {
        // Chống lặp vô hạn nếu chính lượt khôi phục lại kích hoạt cùng lỗi.
        setTimeout(() => {
          this.recovering = false;
          // eslint-disable-next-line no-console
          console.debug('[NavRecovery] mở lại khoá debounce');
        }, 1500);
      });
  }

  private handleNavigationEvent(event: NavigationStart | NavigationCancel | NavigationError): void {
    if (event instanceof NavigationStart) {
      this.lastNavigationUrl = event.url;
      return;
    }
    // eslint-disable-next-line no-console
    console.debug(
      event instanceof NavigationCancel ? '[NavRecovery] NavigationCancel →' : '[NavRecovery] NavigationError →',
      event.url,
      event instanceof NavigationCancel ? `| reason: ${event.reason}` : (event as NavigationError).error
    );
  }
}
