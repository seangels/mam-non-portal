import themes from 'devextreme/ui/themes';
import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';
import { loadMessages, locale } from 'devextreme/localization';
import viMessages from 'devextreme/localization/messages/vi.json';

import { AppModule } from './app/app.module';


loadMessages(viMessages);
locale('vi');

themes.ready(() => {
  platformBrowserDynamic().bootstrapModule(AppModule)
    .catch(err => console.error(err));
});
