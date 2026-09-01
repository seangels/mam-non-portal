import { Component, HostBinding } from '@angular/core';
import { AuthService, ScreenService, AppInfoService } from './shared/services';
import { NavigationRecoveryService } from './core/errors/navigation-recovery.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent  {
  @HostBinding('class') get getClass() {
    return Object.keys(this.screen.sizes).filter(cl => this.screen.sizes[cl]).join(' ');
  }

  constructor(
    private authService: AuthService,
    private screen: ScreenService,
    public appInfo: AppInfoService,
    // Khởi tạo sớm để bắt đầu log điều hướng ngay từ lúc bootstrap (không đợi tới lần crash đầu tiên).
    _navigationRecovery: NavigationRecoveryService
  ) { }

  isAuthenticated() {
    return this.authService.loggedIn;
  }
}
