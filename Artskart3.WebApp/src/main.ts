import { platformBrowser } from '@angular/platform-browser';
import { AppModule } from './app/app-module';
import '@artsdatabanken/components';
import '@artsdatabanken/icons/icon';

platformBrowser().bootstrapModule(AppModule, {
  
})
  .catch(err => console.error(err));
