import { provideHttpClient, withInterceptors, HttpBackend } from '@angular/common/http';
import { APP_INITIALIZER, ErrorHandler, NgModule, provideBrowserGlobalErrorListeners } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { TranslateModule, TranslateLoader, TranslationObject } from '@ngx-translate/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AppRoutingModule } from './app-routing-module';
import { App } from './app';
import { SharedModule } from './shared/shared.module';
import { LayoutsModule } from './layouts/layouts.module';
import { languageInterceptor } from './shared/interceptors/language.interceptor';
import { csrfInterceptor } from './shared/interceptors/csrf.interceptor';
import { LanguageService } from './shared/services/languages/language.service';
import { ApplicationinsightsAngularpluginErrorService } from '@microsoft/applicationinsights-angularplugin-js';
import { LoggingService } from './shared/logging.service';
import { AreasService } from './core/services/areas/areas.service';
import { AreaMapLayerService } from './core/services/areas/area-map-layer.service';

class CustomTranslateLoader implements TranslateLoader {
  private http: HttpClient;

  constructor(handler: HttpBackend) {
    // HttpClient created from HttpBackend bypasses all interceptors,
    // avoiding circular dependency with languageInterceptor
    this.http = new HttpClient(handler);
  }

  getTranslation(lang: string): Observable<TranslationObject> {
    return this.http.get<TranslationObject>(`/assets/languages/${lang}.json`);
  }
}

export function HttpLoaderFactory(handler: HttpBackend): TranslateLoader {
  return new CustomTranslateLoader(handler);
}

export function initializeLanguageFactory(languageService: LanguageService) {
  return () => languageService.initialize();
}

@NgModule({
  declarations: [
    App
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    SharedModule,
    LayoutsModule,
    TranslateModule.forRoot({
      fallbackLang: 'no',
      loader: {
        provide: TranslateLoader,
        useFactory: HttpLoaderFactory,
        deps: [HttpBackend]
      }
    })
  ],
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withInterceptors([languageInterceptor, csrfInterceptor])),
    AreasService,
    AreaMapLayerService,
    {
      provide: APP_INITIALIZER,
      useFactory: initializeLanguageFactory,
      deps: [LanguageService],
      multi: true
    },
    LoggingService,
    {
      provide: ErrorHandler,
      useClass: ApplicationinsightsAngularpluginErrorService
    }
  ],
  bootstrap: [App]
})
export class AppModule { }
