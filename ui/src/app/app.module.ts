import { APP_INITIALIZER, LOCALE_ID, NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { registerLocaleData } from '@angular/common';
import localeVi from '@angular/common/locales/vi';
import { HTTP_INTERCEPTORS, HttpClientModule } from '@angular/common/http';

import { AppComponent } from './app.component';
import { SideNavOuterToolbarModule, SideNavInnerToolbarModule, SingleCardModule } from './layouts';
import { FooterModule, LoginFormModule } from './shared/components';
import { AuthService, ScreenService, AppInfoService } from './shared/services';
import { UnauthenticatedContentModule } from './unauthenticated-content';
import { AppRoutingModule } from './app-routing.module';
import { AuthInterceptor } from './core/interceptors/auth.interceptor';
import { SetupService } from './core/services/setup.service';

registerLocaleData(localeVi);

function initializeApplication(setupService: SetupService, authService: AuthService): () => Promise<void> {
  return async () => {
    const setupState = await setupService.loadStatus();
    if (setupState === 'complete') {
      await authService.restoreSession();
    }
  };
}

@NgModule({
  declarations: [
    AppComponent
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    SideNavOuterToolbarModule,
    SideNavInnerToolbarModule,
    SingleCardModule,
    FooterModule,
    LoginFormModule,
    UnauthenticatedContentModule,
    AppRoutingModule
  ],
  providers: [
    AuthService,
    ScreenService,
    AppInfoService,
    { provide: LOCALE_ID, useValue: 'vi-VN' },
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
    {
      provide: APP_INITIALIZER,
      useFactory: initializeApplication,
      deps: [SetupService, AuthService],
      multi: true
    }
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
