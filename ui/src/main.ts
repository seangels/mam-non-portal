import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';
import { loadMessages, locale } from 'devextreme/localization';
import viMessages from 'devextreme/localization/messages/vi.json';

import { AppModule } from './app/app.module';
import { bootstrapWithBundledDevExtremeTheme } from './app/core/bootstrap/devextreme-bundled-theme-bootstrap';


loadMessages(viMessages);
locale('vi');

void bootstrapWithBundledDevExtremeTheme(() => platformBrowserDynamic().bootstrapModule(AppModule))
  .catch(err => console.error(err));
